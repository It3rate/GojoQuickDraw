"""Resumable full analysis of already converted collections, never raw corpora.

Baseline archives remain immutable. Source-linked native opinion structures are
saved in a separate category collection. Seven authored proposal states are a
review cache; full native construction execution remains the explicit stages run.
"""
from __future__ import annotations

import atexit
from collections import Counter, deque
from concurrent.futures import ProcessPoolExecutor
from contextlib import ExitStack
from copy import deepcopy
from datetime import datetime, timezone
import hashlib
import json
import multiprocessing
from pathlib import Path
import sys

from . import bridge, dataset, upstream

_encoder = None
_gojo = None


def enrich(packet, category, gojo_root):
    upstream.activate(gojo_root)
    from quickdraw_annotations import annotate
    from .context import build_context
    from .opinions import build as opinions
    result = deepcopy(packet)
    observed = result["observation"]
    variants = {mode: annotate(observed, mode) for mode in ("observed-fit", "gentle-priors", "stronger-priors")} if observed.get("spans") else {}
    result["annotations"] = variants.get("observed-fit")
    result["context"] = build_context(observed, variants.get("stronger-priors"), category)
    result["context"]["annotationMode"] = "stronger-priors" if variants else None
    result.pop("objects", None)
    result.pop("layout", None)
    proposal = None
    if category.casefold() == "church" and observed.get("spans"):
        from .objects import enrich_context
        result["context"], result["objects"] = enrich_context(observed, result["context"], variants.get("stronger-priors"))
        from .layout import enrich_context as place_layout
        result["context"], result["layout"] = place_layout(observed, result["context"])
        from quickdraw_gestalt_geometry import build
        from quickdraw_gestalt_completion import extend
        proposal = extend(observed, build(observed))
    # Empty evidence still gets an empty, readable opinion envelope.
    result["opinions"] = opinions(result, proposal)
    from .stages import _curve_extent
    preview_warnings = []
    if proposal:
        for step in proposal["steps"]:
            extents = [_curve_extent(edge["controls"]) for edge in step["edges"]]
            if extents and any(b[0] < 0 or b[1] < 0 or b[2] > 512 or b[3] > 512 for b in extents):
                preview_warnings.append({"stepId": step["id"], "kind": "proposal-outside-observer-frame",
                                         "qualification": "Actual finite proposal curves can extend outside the preview; this is not a native execution diagnostic."})
    result["analysis"] = {"schema": "gojo.quickdraw-analysis.v1", "annotationModes": variants,
                          "proposal": proposal, "proposalNativeExecuted": False, "warnings": preview_warnings,
                          "qualification": "Authored finite analysis. Proposal previews are not executed native rungs; use Drawing steps for independent native construction and readback. No model is trained."}
    return result


def _initialize(root, executable):
    global _gojo
    _gojo = Path(root)
    upstream.activate(_gojo)
    bridge._built[str(_gojo.resolve())] = executable
    atexit.register(_close)


def _close():
    global _encoder
    if _encoder is not None:
        _encoder.close(); _encoder = None


def _one(task):
    global _encoder
    folder, row, packet = task
    folder = Path(folder)
    try:
        result = enrich(packet, row["category"], _gojo)
        result["itemKey"] = row["itemId"]
        result["provenance"] = {**packet.get("provenance", {}), "analysisBaseline": row["baseline"], "analysisFingerprint": row["analysisFingerprint"]}
        result["opinions"]["provenance"] = {"baseline": row["baseline"], "analysisFingerprint": row["analysisFingerprint"],
                                            "sourceSha256": row["sourceSha256"], "qualification": "These dependent opinions share the original acquisition cause."}
        # A worker can process several category folders; its encoder follows the
        # selected folder rather than mixing their archive paths.
        if _encoder is None or getattr(_encoder, "analysis_folder", None) != folder or _encoder.process.poll() is not None:
            _close(); _encoder = bridge.Encoder(_gojo, folder / "native"); _encoder.analysis_folder = folder
        native = _encoder.encode(result)
        if native["summary"]["sourceSha256"] != row["sourceSha256"] or native["summary"]["sourceId"] != row["keyId"]:
            raise ValueError("Analysis native archive changed acquisition identity")
        evidence_path = folder / "records" / (row["itemId"] + ".evidence.json.gz")
        native_path = folder / "native" / (row["itemId"] + ".gojo.json.gz")
        dataset._gzip_json(evidence_path, result)
        return {**row, "status": "ok", "evidencePath": evidence_path.relative_to(folder).as_posix(),
                "nativePath": native_path.relative_to(folder).as_posix(), "evidenceSha256": dataset.sha256(evidence_path),
                "nativeSha256": dataset.sha256(native_path), "counts": result["observation"]["counts"],
                "nativeSummary": native["summary"], "convertedUtc": datetime.now(timezone.utc).isoformat(),
                "analysisPasses": len(result["analysis"]["proposal"]["steps"]) if result["analysis"]["proposal"] else 0}
    except Exception as problem:
        path = folder / "errors" / (row["itemId"] + ".source.json.gz")
        dataset._gzip_json(path, {"sourceOriginalJson": packet["sourceOriginalJson"], "baseline": row["baseline"], "error": str(problem)})
        return {**row, "status": "error", "error": str(problem), "retainedSource": path.relative_to(folder).as_posix()}


def run(source, output, gojo_root, *, limit=None, workers=4, retry_errors=False, key_ids=None):
    from .selection import datasets, selected_rows
    source, output, gojo_root = (Path(p).resolve() for p in (source, output, gojo_root))
    if source == output or source in output.parents or output in source.parents:
        raise ValueError("Analysis output must be separate from the baseline collection")
    if isinstance(workers, bool) or not isinstance(workers, int) or not 1 <= workers <= 8:
        raise ValueError("Choose 1..8 analysis workers")
    if limit is not None and (isinstance(limit, bool) or not isinstance(limit, int) or limit < 1):
        raise ValueError("Limit must be a positive integer")
    if key_ids is not None:
        if isinstance(key_ids, (str, bytes)) or not key_ids or any(not isinstance(key, str) or not key for key in key_ids):
            raise ValueError("Key selection must contain nonempty source key strings")
        selected_keys = set(key_ids)
        available = {row["keyId"] for _, row in selected_rows(source) if row["keyId"] in selected_keys}
        if available != selected_keys:
            raise ValueError("Selected source keys are absent: " + ", ".join(sorted(selected_keys - available)))
    else:
        selected_keys = None
    folders = datasets(source)
    implementation = {"converter": dataset._pipeline_fingerprint(), "upstream": upstream.fingerprint(gojo_root)}
    signature = hashlib.sha256(dataset._canonical(implementation).encode()).hexdigest()
    # Build once before spawning so workers never race the compiler.
    executable = bridge.executable(gojo_root)
    output.mkdir(parents=True, exist_ok=True)
    counters = Counter()
    with dataset._writer_lock(output), ExitStack() as stack:
        collection_path = output / "collection.json"
        if collection_path.exists():
            existing = json.loads(collection_path.read_text(encoding="utf-8"))
            if existing.get("analysisFingerprint") != signature or existing.get("baseline") != str(source):
                raise ValueError("Analysis implementation or baseline changed; choose a new output folder")
        elif any(output.iterdir()):
            # The writer lock itself is the only expected entry in a new folder.
            if any(p.name != ".convert.lock" for p in output.iterdir()):
                raise ValueError("Choose an empty analysis output or resume an analysis collection")
        collection = {"schema": "gojo.quickdraw-collection.v1", "kind": "derived-analysis", "baseline": str(source),
                      "analysisFingerprint": signature, "implementation": implementation}
        dataset._atomic_json(collection_path, collection)
        indexes, journals, manifests = {}, {}, {}

        def prepare(category, baseline_folder):
            if not category or category in (".", "..") or category[-1:] in (".", " ") or any(c in category for c in '\\/:*?"<>|') or any(ord(c) < 32 for c in category):
                raise ValueError("Category cannot be used as an analysis folder name")
            folder = output / category
            if folder in indexes:
                return folder
            folder.mkdir(exist_ok=True)
            for name in ("records", "native", "errors"):
                (folder / name).mkdir(exist_ok=True)
            config = json.loads((baseline_folder / "manifest.json").read_text(encoding="utf-8")).get("config", {})
            manifest = {"schema": dataset.SCHEMA, "kind": "derived-analysis", "fingerprint": signature,
                        "config": config, "analysisFingerprint": signature, "baseline": str(source), "category": category}
            if (folder / "manifest.json").exists():
                prior = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
                if prior.get("fingerprint") != signature or prior.get("category") != category:
                    raise ValueError("Analysis category folder has incompatible provenance")
            indexes[folder] = stack.enter_context(dataset._index(folder, repair_tail=True))
            journals[folder] = stack.enter_context((folder / "index.ndjson").open("a", encoding="utf-8", newline="\n"))
            manifests[folder] = manifest
            dataset._atomic_json(folder / "manifest.json", manifest)
            return folder

        def tasks():
            for baseline_folder, stored in selected_rows(source, limit=limit if selected_keys is None else None):
                if selected_keys is not None and stored["keyId"] not in selected_keys:
                    continue
                if limit is not None and counters["matched"] >= limit:
                    break
                counters["matched"] += 1
                category = stored.get("category", "unknown")
                folder = prepare(category, baseline_folder)
                baseline = {"datasetPath": str(baseline_folder), "itemId": stored["itemId"],
                            "evidenceSha256": stored["evidenceSha256"], "nativeSha256": stored["nativeSha256"]}
                identity = hashlib.sha256(dataset._canonical([baseline, signature, category]).encode()).hexdigest()[:40]
                dataset.verify_row(baseline_folder, stored)
                prior = indexes[folder].get(identity)
                if prior and prior["status"] == "ok":
                    dataset.verify_row(folder, prior); counters["resumed"] += 1
                    continue
                if prior and prior["status"] == "error" and not retry_errors:
                    counters["errors"] += 1
                    continue
                import gzip
                with gzip.open(dataset._safe_path(baseline_folder, stored["evidencePath"]), "rt", encoding="utf-8") as stream:
                    packet = json.load(stream)
                if packet["itemKey"] != stored["itemId"] or packet["source"]["key_id"] != stored["keyId"]:
                    raise ValueError("Baseline source and selected index disagree")
                row = {"itemId": identity, "keyId": stored["keyId"], "category": category,
                       "sourceSha256": stored["sourceSha256"], "baseline": baseline, "analysisFingerprint": signature}
                yield str(folder), row, packet

        def publish(folder, row):
            folder = Path(folder)
            journals[folder].write(dataset._canonical(row) + "\n"); journals[folder].flush()
            indexes[folder][row["itemId"]] = row
            counters["converted" if row["status"] == "ok" else "errors"] += 1
            if row["status"] == "ok":
                counters["passes"] += row["analysisPasses"]
            done = counters["converted"] + counters["errors"] + counters["resumed"]
            print(f"Analysis {done}: {counters['converted']} completed, {counters['resumed']} reused, {counters['errors']} retained errors", file=sys.stderr, flush=True)

        pending = deque()
        with ProcessPoolExecutor(workers, mp_context=multiprocessing.get_context("spawn"), initializer=_initialize,
                                 initargs=(str(gojo_root), executable)) as pool:
            for task in tasks():
                pending.append((task[0], pool.submit(_one, task)))
                if len(pending) >= workers * 2:
                    folder, future = pending.popleft(); publish(folder, future.result())
            while pending:
                folder, future = pending.popleft(); publish(folder, future.result())
        total = Counter()
        for folder, index in indexes.items():
            counts = Counter(row["status"] for row in index.values())
            total.update(counts)
            manifests[folder]["lastRun"] = {"storedRecords": counts["ok"], "storedErrors": counts["error"],
                                           "finishedUtc": datetime.now(timezone.utc).isoformat()}
            dataset._atomic_json(folder / "manifest.json", manifests[folder])
        result = {"output": str(output), **dict(counters), "storedRecords": total["ok"], "storedErrors": total["error"],
                  "drawings": total["ok"], "workers": workers, "sourceUnchanged": True,
                  "qualification": "Full authored analysis and native source-linked opinions. Seven proposal states are cached; native drawing-step execution stays on demand. No learned priors or general feedback solver."}
        if selected_keys is not None:
            result["selectedKeyIds"] = sorted(selected_keys)
        collection["lastRun"] = result
        dataset._atomic_json(collection_path, collection)
        return result
