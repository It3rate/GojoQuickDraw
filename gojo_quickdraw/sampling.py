"""Category-balanced random source selection and category-folder collections.

Indexes contain byte offsets and line numbers, never reconstructed drawings.
Uniform record selection does not favor long records or the start of a file.
"""
from __future__ import annotations

from collections import Counter
from datetime import datetime, timezone
import gzip
import hashlib
import json
import os
from pathlib import Path
import random
import secrets
import struct
import sys
import uuid

from . import dataset

SCHEMA = "gojo.quickdraw-collection.v1"


def category_names():
    return json.loads(Path(__file__).with_name("categories.json").read_text(encoding="utf-8"))


def category_name(file):
    name = Path(file).name
    for suffix in (".ndjson.gz", ".jsonl.gz", ".ndjson", ".jsonl"):
        if name.lower().endswith(suffix):
            name = name[:-len(suffix)]
            break
    if name.startswith("full_raw_"):
        name = name[len("full_raw_"):]
    canonical = {name.casefold(): name for name in category_names()}
    return canonical.get(name.casefold(), name)


def source_files(input_path, categories=None, recursive=False):
    path = Path(input_path).resolve()
    root = path.parent if path.is_file() else path
    choices = {}
    # A selected file also supplies its containing category library for multi-select.
    for file in dataset._inputs(root, recursive):
        name = category_name(file)
        if not name or name.startswith(".") or any(c in name for c in '\\/:*?"<>|'):
            raise ValueError(f"Category filename cannot be used as a folder: {name!r}")
        choices.setdefault(name.casefold(), []).append((name, file))
    wanted = {name.casefold() for name in categories} if categories is not None else set(choices)
    if not wanted:
        raise ValueError("Select at least one category")
    missing = wanted - choices.keys()
    if missing:
        raise ValueError("Selected category files are not in the input folder: " + ", ".join(sorted(missing)))
    selected = {}
    for key in sorted(wanted):
        variants = sorted(choices[key], key=lambda item: (item[1].name.startswith("full_raw_"), item[1].suffix == ".gz", str(item[1]).casefold()))
        name, file = variants[0]
        selected[name] = file
    return selected


def _stamp(path):
    stat = path.stat()
    return {"size": stat.st_size, "modifiedNs": stat.st_mtime_ns}


def _cache_root():
    return Path(os.environ.get("LOCALAPPDATA", Path.home() / ".cache")) / "GojoQuickDraw" / "source-index"


def index_source(path, cache=None):
    path = Path(path).resolve()
    stamp = _stamp(path)
    key = hashlib.sha256(dataset._canonical([str(path), stamp, 1]).encode()).hexdigest()
    # Resolve Windows profile/package redirection before atomic rename operations.
    folder = (Path(cache or _cache_root()) / key).resolve()
    with dataset._writer_lock(folder):
        meta, locations = folder / "index.json", folder / "locations.bin"
        if meta.is_file() and locations.is_file():
            info = json.loads(meta.read_text(encoding="utf-8"))
            if locations.stat().st_size == info["count"] * 16 and dataset.sha256(locations) == info["sha256"]:
                return {**info, "locations": str(locations)}
        print(f"Indexing {path.name} for random record selection (saved for future runs)…", file=sys.stderr, flush=True)
        temporary = folder / ("locations-" + uuid.uuid4().hex + ".tmp")
        count, position, next_notice = 0, 0, 64 * 1024 * 1024
        digest = hashlib.sha256()
        opener = gzip.open if path.suffix.lower() == ".gz" else open
        try:
            with opener(path, "rb") as source, temporary.open("wb") as output:
                for line_number, line in enumerate(source, 1):
                    if line.strip():
                        entry = struct.pack("<QQ", position, line_number)
                        output.write(entry); digest.update(entry); count += 1
                    position += len(line)
                    if position >= next_notice:
                        print(f"Indexing {path.name}: {position // (1024*1024)} MB read", file=sys.stderr, flush=True)
                        next_notice = position + 64 * 1024 * 1024
            if _stamp(path) != stamp:
                raise ValueError(f"Source changed while indexing: {path}")
            os.replace(temporary, locations)
            info = {"sourceFile": str(path), "sourceStamp": stamp, "count": count, "sha256": digest.hexdigest()}
            dataset._atomic_json(meta, info)
            return {**info, "locations": str(locations)}
        finally:
            temporary.unlink(missing_ok=True)


def make_plan(files, count, seed, cache=None):
    if count < 1:
        raise ValueError("Sample count must be positive")
    rng, quotas, indexes = random.Random(seed), Counter(), {}
    available, total = list(files), 0
    # Randomized category rounds keep the total balanced without scanning unused files.
    while available and total < count:
        rng.shuffle(available)
        exhausted = []
        for name in available:
            if total >= count:
                break
            if name not in indexes:
                indexes[name] = index_source(files[name], cache)
            if quotas[name] >= indexes[name]["count"]:
                exhausted.append(name)
            else:
                quotas[name] += 1
                total += 1
        available = [name for name in available if name not in exhausted]
    groups = []
    for name in sorted((name for name in quotas if quotas[name]), key=str.casefold):
        info = indexes[name]
        # Per-category RNG keeps source sampling independent of folder traversal.
        local = random.Random(hashlib.sha256(f"{seed}:{name}".encode()).digest())
        positions = sorted(local.sample(range(info["count"]), quotas[name]))
        selected = []
        with Path(info["locations"]).open("rb") as offsets:
            for position in positions:
                offsets.seek(position * 16)
                offset, line = struct.unpack("<QQ", offsets.read(16))
                selected.append({"offset": offset, "line": line})
        group = {"category": name, "sourceFile": str(files[name]), "sourceStamp": info["sourceStamp"], "selected": selected}
        for entry, (_, _, original, _) in zip(selected, read_group(group)):
            entry["sourceSha256"] = hashlib.sha256(original.encode()).hexdigest()
        groups.append(group)
    return {"seed": seed, "requested": count, "selected": sum(quotas.values()), "groups": groups,
            "basis": "Randomized balanced category rounds; uniform record selection without replacement within each category."}


def read_group(group):
    path = Path(group["sourceFile"])
    if _stamp(path) != group["sourceStamp"]:
        raise ValueError(f"Source file changed after selection: {path}; start a new sample")
    opener = gzip.open if path.suffix.lower() == ".gz" else open
    with opener(path, "rb") as stream:
        for selected in group["selected"]:
            stream.seek(selected["offset"])
            original = stream.readline().decode("utf-8-sig").rstrip("\r\n")
            if selected.get("sourceSha256") and hashlib.sha256(original.encode()).hexdigest() != selected["sourceSha256"]:
                raise ValueError("A selected source record no longer matches its saved sample")
            yield path, selected["line"], original, group["category"]


def convert_collection(input_path, output, gojo_root, *, categories=None, count=25, seed=None, recursive=False,
                       all_records=False, cache=None, fit_config=None, max_points=20000, max_spans=256,
                       retry_errors=False, encoder_factory=None):
    root, source = Path(output).resolve(), Path(input_path).resolve()
    library = source.parent if source.is_file() else source
    if root.is_relative_to(library):
        raise ValueError("Output must be outside the source category folder")
    files = source_files(source, categories, recursive)
    request = {"input": str(library), "categories": list(files), "count": None if all_records else count,
               "files": {name: {"path": str(file), **_stamp(file)} for name, file in files.items()}}
    with dataset._writer_lock(root):
        manifest_path = root / "collection.json"
        if (root / "manifest.json").exists():
            raise ValueError("This is an existing single dataset. Choose New collection to generate category folders.")
        if manifest_path.exists():
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            if manifest.get("schema") != SCHEMA:
                raise ValueError("Unsupported collection format")
        else:
            if any(path.name != ".convert.lock" for path in root.iterdir()):
                raise ValueError("Choose a new collection folder, or an existing QuickDraw collection")
            manifest = {"schema": SCHEMA, "createdUtc": datetime.now(timezone.utc).isoformat(), "layout": "one native evidence dataset per category folder"}
        active = root / "_sampling" / "active.json"
        active.parent.mkdir(exist_ok=True)
        previous = json.loads(active.read_text(encoding="utf-8")) if active.exists() else None
        reuse = previous and previous["request"] == request and (seed == previous["plan"]["seed"] if seed is not None else
            previous["status"] == "running" or retry_errors and previous["status"] == "retained-errors")
        if reuse:
            plan = previous["plan"]
            print(f"Resuming saved random selection (seed {plan['seed']})", file=sys.stderr, flush=True)
        elif all_records:
            plan = {"seed": seed if seed is not None else secrets.randbits(63), "requested": None, "selected": None,
                    "basis": "All records in selected category files", "groups": [
                        {"category": name, "sourceFile": str(file), "sourceStamp": _stamp(file)} for name, file in files.items()]}
        else:
            plan = make_plan(files, count, seed if seed is not None else secrets.randbits(63), cache)
        state = {"request": request, "plan": plan, "status": "running"}
        dataset._atomic_json(active, state)
        archive_plan = active.parent / f"sample-{datetime.now(timezone.utc):%Y%m%dT%H%M%S}-{uuid.uuid4().hex[:8]}.json"
        dataset._atomic_json(archive_plan, state)
        manifest["activeSample"] = str(active.relative_to(root))
        dataset._atomic_json(manifest_path, manifest)
        totals = Counter(converted=0, resumed=0, errors=0)
        for group in plan["groups"]:
            name, file = group["category"], Path(group["sourceFile"])
            print(f"Category {name}: " + ("all records" if all_records else f"{len(group['selected'])} randomly selected drawings"), file=sys.stderr, flush=True)
            records = ((f, line, original, name) for f, line, original, _ in dataset.source_records([file])) if all_records else read_group(group)
            result = dataset.convert(file, root / name, gojo_root, records=records, fit_config=fit_config,
                                     max_points=max_points, max_spans=max_spans, retry_errors=retry_errors, encoder_factory=encoder_factory)
            totals.update({key: result[key] for key in ("converted", "resumed", "errors")})
        from .selection import datasets
        folders = datasets(root) if plan["groups"] else []
        result = {"output": str(root), **totals, "storedRecords": sum(data.get("lastRun", {}).get("storedRecords", 0) for _, data in folders),
                  "storedErrors": sum(data.get("lastRun", {}).get("storedErrors", 0) for _, data in folders), "seed": plan["seed"],
                  "selected": plan["selected"], "categories": [group["category"] for group in plan["groups"]],
                  "samplingBasis": plan["basis"], "selectionPlan": str(archive_plan), "synthesisGenerated": False}
        state["status"] = "retained-errors" if result["errors"] else "completed"
        dataset._atomic_json(active, state); dataset._atomic_json(archive_plan, state)
        manifest["lastRun"] = result | {"finishedUtc": datetime.now(timezone.utc).isoformat()}
        dataset._atomic_json(manifest_path, manifest)
        return result
