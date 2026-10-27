"""Append bookkeeping over saved acquisition identities, never a new interpretation.

Completed artifacts remain untouched. Each newly processed record keeps its
implementation fingerprint; a collection may contain several recorded revisions.
"""
from . import dataset


def identity(row):
    key = row.get("keyId")
    return str(row.get("category", "unknown")).casefold(), (
        "key:" + str(key) if key is not None else "sha:" + row["sourceSha256"])


def by_source(rows):
    found = {}
    for row in rows:
        key = identity(row)
        if key not in found or row.get("status") == "ok":
            found[key] = row
    return found


def existing(path):
    from .selection import datasets
    if not path.is_dir():
        return {}
    try:
        folders = datasets(path)
    except ValueError as error:
        if str(error).startswith("No generated datasets"):
            return {}
        raise
    found = {}
    for folder, _ in folders:
        with dataset._index(folder) as rows:
            for key, row in by_source(rows.values()).items():
                if key not in found or row.get("status") == "ok":
                    found[key] = row
    return found


def remember_revision(manifest, signature, details, field="fingerprint"):
    versions = manifest.setdefault("processingVersions", {})
    original = manifest.get(field)
    if original and original not in versions:
        versions[original] = {key: manifest[key] for key in
            ("config", "upstream", "pipelineSourceSha256", "packageVersion", "implementation", "baseline") if key in manifest}
    versions.setdefault(signature, details)
