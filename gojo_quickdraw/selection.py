"""Read existing datasets recursively; keep browsing choices out of acquired evidence."""
from __future__ import annotations

from datetime import datetime
import hashlib
import heapq
import json
import os
from pathlib import Path

ORDERS = ("category", "recent", "random")
_INTERNAL = {"records", "native", "errors", "_sampling", "_source-index", "review", "qa"}


def datasets(path):
    from .dataset import SCHEMA
    root = Path(path).resolve()
    if not root.is_dir():
        raise ValueError("Choose a generated dataset or collection folder")
    found = []
    for directory, folders, _ in os.walk(root, followlinks=False):
        folders[:] = [name for name in folders if name not in _INTERNAL and not name.startswith(".") and not name.endswith("-runs")]
        manifest = Path(directory) / "manifest.json"
        if manifest.is_file():
            data = json.loads(manifest.read_text(encoding="utf-8"))
            if data.get("schema") == SCHEMA:
                found.append((Path(directory), data))
    if not found:
        raise ValueError("No generated datasets found in this folder or its subfolders")
    return sorted(found, key=lambda entry: entry[0].relative_to(root).as_posix().casefold())


def selected_rows(path, key_id=None, limit=None, *, order="category", seed=0, offset=0):
    from .dataset import _index, _safe_path
    if limit is not None and (isinstance(limit, bool) or not isinstance(limit, int) or limit < 1):
        raise ValueError("Limit must be a positive integer")
    if order not in ORDERS:
        raise ValueError("Unknown inspection order")
    if isinstance(offset, bool) or not isinstance(offset, int) or offset < 0:
        raise ValueError("Offset must be a nonnegative integer")
    if key_id is not None:
        # An explicit identity is a lookup, independent of the browser's sampling order.
        # Keep every match so callers can still detect duplicate/ambiguous identities.
        order = "category"
    if limit is None and order != "category":
        raise ValueError("Recent/random selection requires a finite limit")
    root = Path(path).resolve()

    def all_rows():
        for folder, _ in datasets(root):
            with _index(folder) as rows:
                for stored in rows.values():
                    if stored.get("status") != "ok" or key_id is not None and stored.get("keyId") != key_id:
                        continue
                    relative = folder.relative_to(root).as_posix()
                    yield folder, dict(stored, selectionId=relative + "::" + stored["itemId"])

    def rank(entry):
        folder, row = entry
        tie = row["selectionId"]
        if order == "random":
            # Hash ranking gives a stable prefix when review/catalog limits differ.
            return hashlib.blake2b(f"{seed}:{tie}".encode(), digest_size=16).digest(), tie
        time = row.get("convertedUtc")
        timestamp = (datetime.fromisoformat(time.replace("Z", "+00:00")).timestamp() if time
                     else _safe_path(folder, row["evidencePath"]).stat().st_mtime)
        return -timestamp, tie

    if order == "category":
        iterator = all_rows()
        try:
            for index, row in enumerate(iterator):
                if index < offset:
                    continue
                if limit is not None and index >= offset + limit:
                    break
                yield row
        finally:
            iterator.close()
    else:
        yield from heapq.nsmallest(offset + limit, all_rows(), key=rank)[offset:]


def catalog(path, limit=1000, *, order="category", seed=0, offset=0):
    root = Path(path).resolve()
    folders = datasets(root)
    fields = ("itemId", "keyId", "category", "counts", "evidencePath", "nativePath", "convertedUtc", "selectionId")
    records = [{**{key: row[key] for key in fields if key in row}, "datasetPath": str(folder)}
               for folder, row in selected_rows(root, limit=limit, order=order, seed=seed, offset=offset)]
    # Journals, rather than last-run summaries, determine the actual page count.
    from .dataset import _index
    totals = {"ok": 0, "error": 0}
    for folder, _ in folders:
        with _index(folder) as rows:
            for row in rows.values():
                if row.get("status") in totals:
                    totals[row["status"]] += 1
    return {"records": records, "folders": [{"path": str(folder), "label": folder.relative_to(root).as_posix()} for folder, _ in folders],
            "storedRecords": totals["ok"], "storedErrors": totals["error"],
            "offset": offset, "pageSize": limit, "pageCount": (totals["ok"] + limit - 1) // limit,
            "order": order, "seed": seed, "recursive": True,
            "qualification": "Catalog metadata and cached previews are observations; archive integrity is checked by verify."}
