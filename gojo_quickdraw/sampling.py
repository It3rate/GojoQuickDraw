"""Category-balanced random source selection and category-folder collections.

Indexes contain byte offsets and line numbers, never reconstructed drawings.
Uniform record selection does not favor long records or the start of a file.
"""
from __future__ import annotations

from collections import Counter
from datetime import datetime, timezone
import gzip
import hashlib
import heapq
import itertools
import json
import os
from pathlib import Path
import random
import secrets
import struct
import sys
import uuid

from . import dataset, recognition as recognition_read

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


def index_source(path, cache=None, recognition="all"):
    recognition_read.validate(recognition)
    path = Path(path).resolve()
    stamp = _stamp(path)
    key = hashlib.sha256(dataset._canonical([str(path), stamp, 1] + ([recognition] if recognition != "all" else [])).encode()).hexdigest()
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
        outcomes = Counter()
        digest = hashlib.sha256()
        opener = gzip.open if path.suffix.lower() == ".gz" else open
        try:
            with opener(path, "rb") as source, temporary.open("wb") as output:
                for line_number, line in enumerate(source, 1):
                    if line.strip():
                        value = recognition_read.from_original(line) if recognition != "all" else None
                        if recognition != "all":
                            outcomes["recognized" if value is True else "unrecognized" if value is False else "unknown"] += 1
                        if recognition_read.accepts(value, recognition):
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
            if recognition != "all":
                info.update(recognition=recognition, recognitionCounts=dict(outcomes))
            dataset._atomic_json(meta, info)
            return {**info, "locations": str(locations)}
        finally:
            temporary.unlink(missing_ok=True)


def make_plan(files, count, seed, cache=None, recognition="all"):
    recognition_read.validate(recognition)
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
                indexes[name] = index_source(files[name], cache, recognition)
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
    return {"seed": seed, "requested": count, "selected": sum(quotas.values()), "groups": groups, "recognition": recognition,
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


def make_growth_plan(files, count, seed, saved, cache=None, recognition="all"):
    """Reach a total across selected categories, excluding retained identities.

    A bounded random permutation supplies candidates. Reading sorted chunks
    avoids one full decompression per random seek for compressed category files.
    The complete selection is saved before any conversion starts.
    """
    from .growth import identity
    known = set(saved)
    known_hashes = {(row['category'].casefold(), row['sourceSha256']) for row in saved.values()}
    recognition_read.validate(recognition)
    counts = Counter(key[0] for key, row in saved.items() if recognition_read.accepts(recognition_read.outcome(row), recognition))
    retained = sum(counts[name.casefold()] for name in files)
    needed = max(0, count - retained)
    groups = {}

    def candidates(name):
        info = index_source(files[name], cache, recognition)
        local = random.Random(hashlib.sha256(f"{seed}:{name}".encode()).digest())
        def positions():
            remaining, swaps = info['count'], {}
            while remaining:
                index = local.randrange(remaining)
                value = swaps.get(index, index)
                remaining -= 1
                swaps[index] = swaps.get(remaining, remaining)
                swaps.pop(remaining, None)
                yield value
        iterator = positions()
        with Path(info['locations']).open('rb') as offsets:
            while chunk := list(itertools.islice(iterator, min(256, max(1, needed)))):
                entries = []
                for rank, index in enumerate(chunk):
                    offsets.seek(index * 16)
                    offset, line = struct.unpack('<QQ', offsets.read(16))
                    entries.append({'offset': offset, 'line': line, 'rank': rank})
                entries.sort(key=lambda item: item['offset'])
                group = {'category': name, 'sourceFile': str(files[name]), 'sourceStamp': info['sourceStamp'], 'selected': entries}
                loaded = []
                for entry, (_, _, original, _) in zip(entries, read_group(group)):
                    digest = hashlib.sha256(original.encode()).hexdigest()
                    try:
                        key = json.loads(original).get('key_id')
                    except (ValueError, AttributeError):
                        key = None
                    source_id = identity({'category': name, 'keyId': key, 'sourceSha256': digest})
                    loaded.append((entry['rank'], source_id, {'offset': entry['offset'], 'line': entry['line'], 'sourceSha256': digest}))
                for _, source_id, entry in sorted(loaded):
                    if source_id in known or (name.casefold(), entry['sourceSha256']) in known_hashes:
                        continue
                    yield source_id, entry, info['sourceStamp']

    rng = random.Random(seed)
    queue = [(counts[name.casefold()], rng.random(), name, candidates(name)) for name in files] if needed else []
    heapq.heapify(queue)
    selected = 0
    try:
        while queue and selected < needed:
            size, _, name, iterator = heapq.heappop(queue)
            try:
                key, entry, stamp = next(iterator)
            except StopIteration:
                continue
            known.add(key)
            groups.setdefault(name, {'category': name, 'sourceFile': str(files[name]), 'sourceStamp': stamp, 'selected': []})['selected'].append(entry)
            selected += 1
            heapq.heappush(queue, (size + 1, rng.random(), name, iterator))
    finally:
        for _, _, _, iterator in queue:
            iterator.close()
    for group in groups.values():
        group['selected'].sort(key=lambda entry: entry['offset'])
    return {'seed': seed, 'requested': count, 'targetTotal': count, 'retained': retained, 'selected': selected, 'recognition': recognition,
            'shortfall': max(0, needed - selected), 'groups': [groups[name] for name in sorted(groups)],
            'basis': 'Grow selected categories to the requested total; retain existing source identities; balance new additions without replacement.'}


def convert_collection(input_path, output, gojo_root, *, categories=None, count=25, seed=None, recursive=False,
                       all_records=False, cache=None, fit_config=None, max_points=20000, max_spans=256,
                       retry_errors=False, encoder_factory=None, target_total=False, append=False, recognition="all"):
    recognition_read.validate(recognition)
    root, source = Path(output).resolve(), Path(input_path).resolve()
    library = source.parent if source.is_file() else source
    if root.is_relative_to(library):
        raise ValueError("Output must be outside the source category folder")
    files = source_files(source, categories, recursive)
    request = {"input": str(library), "categories": list(files), "count": None if all_records else count,
               "files": {name: {"path": str(file), **_stamp(file)} for name, file in files.items()}}
    if recognition != "all":
        request['recognition'] = recognition
    if target_total:
        if all_records:
            raise ValueError('A target total cannot process all records')
        request['targetTotal'] = True
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
        retry_totals = Counter(converted=0, resumed=0, errors=0)
        retried_hashes = set()
        if target_total and append and retry_errors:
            from .growth import existing
            failures = [row for row in existing(root).values() if row['status'] == 'error']
            for name, file in files.items():
                retry_records = []
                for row in failures:
                    if row['category'].casefold() != name.casefold():
                        continue
                    with gzip.open(dataset._safe_path(root / name, row['retainedSource']), 'rt', encoding='utf-8') as retained:
                        original = json.load(retained)['sourceOriginalJson']
                    if not recognition_read.accepts(recognition_read.from_original(original), recognition):
                        continue
                    if hashlib.sha256(original.encode()).hexdigest() != row['sourceSha256']:
                        raise ValueError('Retained failed source changed; cannot retry it')
                    retry_records.append((Path(row['sourceFile']), row['sourceLine'], original, name))
                    retried_hashes.add((name.casefold(), row['sourceSha256']))
                if retry_records:
                    retried = dataset.convert(file, root / name, gojo_root, records=retry_records, fit_config=fit_config,
                        max_points=max_points, max_spans=max_spans, retry_errors=True, encoder_factory=encoder_factory, append=True)
                    retry_totals.update({key: retried[key] for key in ('converted', 'resumed', 'errors')})
        previous = json.loads(active.read_text(encoding="utf-8")) if active.exists() else None
        reuse = previous and previous["request"] == request and (seed == previous["plan"]["seed"] if seed is not None else
            previous["status"] == "running" or retry_errors and previous["status"] == "retained-errors")
        if reuse:
            plan = previous["plan"]
            print(f"Resuming saved random selection (seed {plan['seed']})", file=sys.stderr, flush=True)
        elif target_total:
            from .growth import existing
            plan = make_growth_plan(files, count, seed if seed is not None else secrets.randbits(63),
                existing(root, resolve_recognition=recognition != "all"), cache, recognition)
        elif all_records:
            plan = {"seed": seed if seed is not None else secrets.randbits(63), "requested": None, "selected": None,
                    "basis": "All records in selected category files", "groups": [
                        {"category": name, "sourceFile": str(file), "sourceStamp": _stamp(file)} for name, file in files.items()]}
        else:
            plan = make_plan(files, count, seed if seed is not None else secrets.randbits(63), cache, recognition)
        state = {"request": request, "plan": plan, "status": "running"}
        if target_total:
            print(f"Target {count}: retain {plan.get('retained', 0)} selected drawings; {plan['selected']} new selections in this batch", file=sys.stderr, flush=True)
        dataset._atomic_json(active, state)
        archive_plan = active.parent / f"sample-{datetime.now(timezone.utc):%Y%m%dT%H%M%S}-{uuid.uuid4().hex[:8]}.json"
        dataset._atomic_json(archive_plan, state)
        manifest["activeSample"] = str(active.relative_to(root))
        dataset._atomic_json(manifest_path, manifest)
        totals = retry_totals
        for group in plan["groups"]:
            name, file = group["category"], Path(group["sourceFile"])
            print(f"Category {name}: " + ("all records" if all_records else f"{len(group['selected'])} randomly selected drawings"), file=sys.stderr, flush=True)
            records = ((f, line, original, name) for f, line, original, _ in dataset.source_records([file])) if all_records else read_group(group)
            if recognition != "all":
                records = (record for record in records if recognition_read.accepts(recognition_read.from_original(record[2]), recognition))
            if retried_hashes:
                records = (record for record in records if (record[3].casefold(), hashlib.sha256(record[2].encode()).hexdigest()) not in retried_hashes)
            result = dataset.convert(file, root / name, gojo_root, records=records, fit_config=fit_config,
                                     max_points=max_points, max_spans=max_spans, retry_errors=retry_errors, encoder_factory=encoder_factory, append=append)
            totals.update({key: result[key] for key in ("converted", "resumed", "errors")})
        from .selection import datasets
        try:
            folders = datasets(root)
        except ValueError as error:
            if not str(error).startswith('No generated datasets'):
                raise
            folders = []
        result = {"output": str(root), **totals, "storedRecords": sum(data.get("lastRun", {}).get("storedRecords", 0) for _, data in folders),
                  "storedErrors": sum(data.get("lastRun", {}).get("storedErrors", 0) for _, data in folders), "seed": plan["seed"],
                  "selected": plan["selected"], "categories": [group["category"] for group in plan["groups"]], "sourceCategories": list(files),
                  "samplingBasis": plan["basis"], "selectionPlan": str(archive_plan), "synthesisGenerated": False}
        result['recognition'] = recognition
        if recognition == 'all':
            result['matchingStoredRecords'] = result['storedRecords']
        else:
            from .growth import existing
            result['matchingStoredRecords'] = sum(row['status'] == 'ok' and row.get('category', '').casefold() in {name.casefold() for name in files}
                and recognition_read.accepts(recognition_read.outcome(row), recognition)
                for row in existing(root, resolve_recognition=True).values()) if folders else 0
        if not all_records and not target_total:
            result['selectionShortfall'] = max(0, count - plan['selected'])
        if target_total:
            result.update(targetTotal=count, selectionShortfall=plan.get('shortfall', 0))
        state["status"] = "retained-errors" if result["errors"] else "completed"
        dataset._atomic_json(active, state); dataset._atomic_json(archive_plan, state)
        manifest["lastRun"] = result | {"finishedUtc": datetime.now(timezone.utc).isoformat()}
        dataset._atomic_json(manifest_path, manifest)
        return result
