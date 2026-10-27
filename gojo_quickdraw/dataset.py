"""Content-addressed, streaming acquisition with independently restorable Gojo records."""
from __future__ import annotations

from collections import Counter
from contextlib import contextmanager
from datetime import datetime, timezone
import gzip
import hashlib
import json
import math
import os
from pathlib import Path
import sqlite3
import sys

from . import __version__
from . import bridge
from .upstream import ROOT, activate, fingerprint

SCHEMA = "gojo.quickdraw-dataset.v1"
EXTRACTION_VERSION = 1


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False)


def _atomic_json(path, value):
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False, allow_nan=False) + "\n", encoding="utf-8")
    os.replace(temporary, path)


def _gzip_json(path, value):
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("wb") as output:
        with gzip.GzipFile(fileobj=output, mode="wb", mtime=0, filename="") as compressed:
            compressed.write(_canonical(value).encode("utf-8"))
    os.replace(temporary, path)


def _unique_pairs(pairs):
    value = {}
    for key, item in pairs:
        if key in value:
            raise ValueError(f"Duplicate JSON property: {key}")
        value[key] = item
    return value


def parse_source(original, max_points=20000):
    def nonfinite(value):
        raise ValueError(f"Non-finite JSON number: {value}")
    source = json.loads(original, object_pairs_hook=_unique_pairs, parse_constant=nonfinite)
    if not isinstance(source, dict) or not isinstance(source.get("key_id"), str) or not source["key_id"].strip():
        raise ValueError("key_id must be a nonempty string")
    if "word" in source and not isinstance(source["word"], str):
        raise ValueError("word must be text when present")
    if "recognized" in source and not isinstance(source["recognized"], bool):
        raise ValueError("recognized must be boolean when present")
    drawing = source.get("drawing")
    if not isinstance(drawing, list):
        raise ValueError("drawing must be a list")
    arity, count = None, 0
    for stroke in drawing:
        if not isinstance(stroke, list) or len(stroke) not in (2, 3):
            raise ValueError("Each stroke must supply x/y or x/y/time arrays")
        if arity is not None and arity != len(stroke):
            raise ValueError("Mixed raw and simplified stroke formats")
        arity = len(stroke)
        if any(not isinstance(axis, list) for axis in stroke) or len({len(axis) for axis in stroke}) != 1:
            raise ValueError("Stroke arrays have different lengths")
        count += len(stroke[0])
        if count > max_points:
            raise ValueError(f"Acquisition exceeds --max-points {max_points}; increase the explicit capacity to retry")
        for axis in stroke:
            if any(isinstance(number, bool) or not isinstance(number, (int, float)) or not math.isfinite(float(number)) for number in axis):
                raise ValueError("Coordinates and timestamps must be finite JSON numbers")
    return source


def _pipeline_fingerprint():
    digest = hashlib.sha256()
    for directory in (ROOT / "gojo_quickdraw", ROOT / "native", ROOT / "shared"):
        for file in sorted(directory.rglob("*")):
            if file.suffix in (".py", ".cs", ".csproj") and not {"bin", "obj", "__pycache__"}.intersection(file.parts):
                digest.update(file.relative_to(ROOT).as_posix().encode())
                digest.update(file.read_bytes())
    return digest.hexdigest()


def _inputs(path, recursive):
    path = Path(path).resolve()
    if path.is_file():
        yield path
        return
    if not path.is_dir():
        raise ValueError(f"Input does not exist: {path}")
    paths = path.rglob("*") if recursive else path.glob("*")
    for file in sorted(paths):
        if file.is_file() and file.name.lower().endswith((".ndjson", ".ndjson.gz", ".jsonl", ".jsonl.gz")):
            yield file


class _index:
    """Disk-backed, disposable projection of the append-only index journal.

    Latest successful/error transaction per identity wins; memory is bounded
    independently of corpus size. The NDJSON remains the portable source.
    """
    def __init__(self, root, repair_tail=False):
        self.path = root / "index.ndjson"
        self.connection = sqlite3.connect(root / ".index-cache.sqlite3")
        self.connection.execute("PRAGMA journal_mode=WAL")
        self.connection.execute("CREATE TABLE IF NOT EXISTS records (id TEXT PRIMARY KEY, value TEXT NOT NULL)")
        self.connection.execute("CREATE TABLE IF NOT EXISTS metadata (id INTEGER PRIMARY KEY, size INTEGER, modified INTEGER)")
        stamp = self.connection.execute("SELECT size,modified FROM metadata WHERE id=1").fetchone()
        current = (self.path.stat().st_size, self.path.stat().st_mtime_ns) if self.path.exists() else (0, 0)
        if stamp != current:
            self.connection.execute("DELETE FROM records")
            if self.path.exists():
                with self.path.open("rb") as stream:
                    while True:
                        start = stream.tell()
                        line = stream.readline()
                        if not line:
                            break
                        if not line.strip():
                            continue
                        if not line.endswith(b"\n"):
                            if not repair_tail:
                                raise ValueError("Uncommitted final index transaction; resume conversion to preserve and repair its tail")
                            (root / "index-interrupted-tail.bin").write_bytes(line)
                            with self.path.open("r+b") as repair:
                                repair.truncate(start)
                            break
                        try:
                            row = json.loads(line)
                        except ValueError as error:
                            raise ValueError(f"Corrupt committed index transaction at byte {start}") from error
                        self.connection.execute("INSERT INTO records VALUES (?,?) ON CONFLICT(id) DO UPDATE SET value=excluded.value",
                                                (row["itemId"], _canonical(row)))
            self._stamp()

    def _stamp(self):
        current = (self.path.stat().st_size, self.path.stat().st_mtime_ns) if self.path.exists() else (0, 0)
        self.connection.execute("INSERT OR REPLACE INTO metadata VALUES (1,?,?)", current)
        self.connection.commit()

    def get(self, key):
        row = self.connection.execute("SELECT value FROM records WHERE id=?", (key,)).fetchone()
        return json.loads(row[0]) if row else None

    def __setitem__(self, key, value):
        self.connection.execute("INSERT INTO records VALUES (?,?) ON CONFLICT(id) DO UPDATE SET value=excluded.value",
                                (key, _canonical(value)))
        self._stamp()

    def values(self):
        for row in self.connection.execute("SELECT value FROM records ORDER BY rowid"):
            yield json.loads(row[0])

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.connection.close()


def _safe_path(root, relative):
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError("Dataset archive path escapes the dataset")
    return path


def verify_row(root, row):
    artifacts = [("evidencePath", "evidenceSha256"), ("nativePath", "nativeSha256")]
    if "stagesPath" in row:
        artifacts.append(("stagesPath", "stagesSha256"))
    for field, digest in artifacts:
        path = _safe_path(root, row[field])
        if not path.is_file() or sha256(path) != row[digest]:
            raise ValueError(f"Missing or changed dataset artifact: {row[field]}")


def load_records(dataset, key_id=None, limit=None, *, order="category", seed=0, offset=0):
    from .selection import selected_rows
    for root, stored in selected_rows(dataset, key_id=key_id, limit=limit, order=order, seed=seed, offset=offset):
        row = dict(stored, datasetPath=str(root))
        verify_row(root, row)
        with gzip.open(_safe_path(root, row["evidencePath"]), "rt", encoding="utf-8") as stream:
            packet = json.load(stream)
        if packet["itemKey"] != row["itemId"] or packet["source"]["key_id"] != row["keyId"]:
            raise ValueError("Dataset index and source identity disagree")
        yield row, packet


def source_records(files):
    for file in files:
        opener = gzip.open if file.suffix.lower() == ".gz" else open
        with opener(file, "rt", encoding="utf-8-sig") as stream:
            for line_number, line in enumerate(stream, 1):
                yield file, line_number, line.rstrip("\r\n"), None


def build_packet(original, gojo_root, item_id, category=None, fit_config=None, max_points=20000, max_spans=256):
    activate(gojo_root)
    from quickdraw_structure_observations import observe_record
    from quickdraw_interpretation import construct
    from quickdraw_annotations import annotate
    from .context import build_context
    source = parse_source(original, max_points)
    observation = observe_record(source, fit_config)
    if len(observation["spans"]) > max_spans:
        raise ValueError(f"Fitted drawing exceeds --max-spans {max_spans}; no spans silently discarded")
    packet = {"schema": "gojo.quickdraw-evidence-packet.v1", "itemKey": item_id,
              "sourceOriginalJson": original, "source": source, "observation": observation,
              "categoryContext": {"value": category or source.get("word"),
                                  "basis": "explicit-context" if category else "source-prompt",
                                  "qualification": "Weak context; neither intended parts nor learned category truth."}}
    if observation["emptyRead"]:
        packet.update(program=None, annotations=None)
    else:
        program = construct(observation, 1, "observed-fit", "Fitted source", 1, 0)
        program["title"] = "Fitted source " + source["key_id"]
        program["description"] = "Acquisition-ordered fitted source; calibration guides are not semantic walls. No synthesis stages."
        packet.update(program=program, annotations=annotate(observation, "observed-fit"))
    packet["context"] = build_context(observation, packet["annotations"], category=category or source.get("word", "unknown"))
    return packet


@contextmanager
def _writer_lock(root):
    root.mkdir(parents=True, exist_ok=True)
    with (root / ".convert.lock").open("a+b") as stream:
        if stream.tell() == 0:
            stream.write(b"0")
            stream.flush()
        stream.seek(0)
        try:
            if os.name == "nt":
                import msvcrt
                msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError as error:
            raise ValueError("Another converter is writing this dataset; use a separate output or wait") from error
        try:
            yield
        finally:
            stream.seek(0)
            if os.name == "nt":
                msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(stream.fileno(), fcntl.LOCK_UN)


def convert(input_path, output, gojo_root, **kwargs):
    root = Path(output).resolve()
    source = Path(input_path).resolve()
    if source.is_dir() and root.is_relative_to(source):
        raise ValueError("Output must be outside the scanned input directory")
    with _writer_lock(root):
        return _convert(input_path, root, gojo_root, **kwargs)


def _convert(input_path, output, gojo_root, *, limit=None, categories=None, recursive=False,
            fit_config=None, max_points=20000, max_spans=256, retry_errors=False, encoder_factory=None, records=None):
    """Append one verified source evidence record at a time. Resume by content."""
    gojo_root = activate(gojo_root)
    root = Path(output).resolve()
    source_path = Path(input_path).resolve()
    if source_path.is_dir() and root.is_relative_to(source_path):
        raise ValueError("Output must be outside the scanned input directory")
    if limit is not None and limit < 1 or max_points < 1 or max_spans < 1:
        raise ValueError("Limits must be positive")
    files = list(_inputs(source_path, recursive))
    if not files:
        raise ValueError("No NDJSON input files found")
    config = {"version": EXTRACTION_VERSION, "fit": fit_config or {}, "maxPoints": max_points,
              "maxSpans": max_spans, "annotationMode": "observed-fit", "hypotheses": "seeded-v1",
              "synthesisSaved": False}
    upstream = fingerprint(gojo_root)
    implementation = _pipeline_fingerprint()
    signature = hashlib.sha256(_canonical({"config": config, "upstream": upstream["sourceSha256"],
                                          "implementation": implementation}).encode()).hexdigest()
    manifest_path = root / "manifest.json"
    if root.exists() and any(p.name != ".convert.lock" for p in root.iterdir()) and not manifest_path.exists():
        raise ValueError("A nonempty output without a dataset manifest is not a converter destination")
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        if manifest.get("schema") != SCHEMA or manifest.get("fingerprint") != signature:
            raise ValueError("Dataset extraction version/configuration changed; choose a new output to retain the old interpretation")
    else:
        root.mkdir(parents=True, exist_ok=True)
        manifest = {"schema": SCHEMA, "createdUtc": datetime.now(timezone.utc).isoformat(),
                    "packageVersion": __version__, "fingerprint": signature,
                    "pipelineSourceSha256": implementation, "upstream": upstream, "config": config,
                    "nativeMeaning": "Restorable Gojo acquisition, fitted profiles, finite annotations and separately qualified hypotheses.",
                    "adapters": "Evidence JSON is a review/proposal cache. Native archives carry connected structural evidence.",
                    "laterStages": "Generated only by the explicit stages command; not training truths.",
                    "learned": False}
        _atomic_json(manifest_path, manifest)
    for folder in ("records", "native", "errors"):
        (root / folder).mkdir(exist_ok=True)
    counters = Counter(scanned=0, matched=0, converted=0, resumed=0, errors=0)
    wanted = {name.casefold() for name in (categories or [])}
    factory = encoder_factory or bridge.Encoder
    with _index(root, repair_tail=True) as rows, factory(gojo_root, root / "native") as encoder, (root / "index.ndjson").open("a", encoding="utf-8", newline="\n") as index:
        for file, line_number, original, file_category in (records if records is not None else source_records(files)):
            if not original.strip():
                continue
            counters["scanned"] += 1
            raw_hash = hashlib.sha256(original.encode("utf-8")).hexdigest()
            error = None
            try:
                source = parse_source(original, max_points)
                category = file_category or source.get("word") or file.name.split(".")[0]
            except (ValueError, TypeError, OverflowError) as problem:
                source, category, error = {}, file_category or file.name.split(".")[0], problem
            if wanted and category.casefold() not in wanted:
                continue
            item_id = hashlib.sha256(_canonical([signature, raw_hash, category]).encode()).hexdigest()[:32]
            counters["matched"] += 1
            if limit is not None and counters["matched"] > limit:
                counters["matched"] -= 1
                break
            prior = rows.get(item_id)
            if prior and prior["status"] == "ok":
                verify_row(root, prior)
                counters["resumed"] += 1
                continue
            if prior and prior["status"] == "error" and not retry_errors:
                counters["errors"] += 1
                continue
            row = {"itemId": item_id, "keyId": source.get("key_id"), "category": category,
                   "sourceFile": str(file), "sourceLine": line_number, "sourceSha256": raw_hash}
            try:
                if error:
                    raise error
                filename_basis = file_category is not None or not source.get("word")
                packet = build_packet(original, gojo_root, item_id, category=category if filename_basis else None, fit_config=fit_config,
                                      max_points=max_points, max_spans=max_spans)
                if filename_basis:
                    packet["categoryContext"]["basis"] = "input-filename"
                packet["provenance"] = {"datasetFingerprint": signature, "sourceSha256": raw_hash,
                                        "upstreamRevision": upstream["revision"], "sourceFile": str(file), "sourceLine": line_number}
                native = encoder.encode(packet)
                if native.get('jointComparison') is not None: packet['jointComparison'] = native['jointComparison']
                summary = native.get("summary", {})
                if summary.get("sourceId") != source["key_id"] or summary.get("sourceSha256") != raw_hash:
                    raise ValueError("Native record identity or exact source digest differs from the submitted acquisition")
                native_path = root / "native" / (item_id + ".gojo.json.gz")
                if not native_path.is_file():
                    raise RuntimeError("Native encoder did not create the expected content-addressed archive")
                evidence_path = root / "records" / (item_id + ".evidence.json.gz")
                _gzip_json(evidence_path, packet)
                row.update(status="ok", evidencePath=evidence_path.relative_to(root).as_posix(),
                           nativePath=native_path.relative_to(root).as_posix(),
                           evidenceSha256=sha256(evidence_path), nativeSha256=sha256(native_path),
                           counts=packet["observation"]["counts"], nativeSummary=native.get("summary", {}),
                           convertedUtc=datetime.now(timezone.utc).isoformat())
                counters["converted"] += 1
            except (ValueError, TypeError, OverflowError, KeyError, RuntimeError, ArithmeticError) as problem:
                error_path = root / "errors" / (item_id + ".source.json.gz")
                _gzip_json(error_path, {"sourceOriginalJson": original, "error": str(problem)})
                row.update(status="error", error=str(problem), retainedSource=error_path.relative_to(root).as_posix())
                counters["errors"] += 1
            index.write(_canonical(row) + "\n")
            index.flush()
            rows[item_id] = row
            if (counters["converted"] + counters["errors"]) % 10 == 0:
                print(f"{counters['matched']} selected: {counters['converted']} converted, {counters['resumed']} reused, {counters['errors']} retained errors", file=sys.stderr)
        totals = Counter(row["status"] for row in rows.values())
    result = {"output": str(root), **dict(counters), "storedRecords": totals.get("ok", 0),
              "storedErrors": totals.get("error", 0), "synthesisGenerated": False}
    manifest["lastRun"] = result | {"finishedUtc": datetime.now(timezone.utc).isoformat()}
    _atomic_json(manifest_path, manifest)
    return result


def verify(dataset, gojo_root, limit=None, *, order="category", seed=0):
    count = 0
    for row, _ in load_records(dataset, limit=limit, order=order if limit else "category", seed=seed):
        restored = bridge.inspect(gojo_root, Path(row["datasetPath"]) / row["nativePath"])
        summary = restored["summary"]
        if summary["sourceId"] != row["keyId"] or summary["sourceSha256"] != row["sourceSha256"]:
            raise ValueError("Native archive belongs to another acquisition")
        if summary["sourceSamples"] != row["counts"]["samples"] or summary["sourceStrokes"] != row["counts"]["strokes"]:
            raise ValueError("Native archive and fitted evidence disagree on acquisition coverage")
        count += 1
    return {"records": count, "nativeRestoreVerified": True, "artifactHashesVerified": True}
