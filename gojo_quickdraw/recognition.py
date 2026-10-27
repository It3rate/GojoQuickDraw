"""Selection over the game's recorded recognition outcome, never human truth."""
import gzip
import json

MODES = ("all", "recognized", "unrecognized", "unknown")


def validate(mode):
    if mode not in MODES:
        raise ValueError("Recognition must be all, recognized, unrecognized or unknown")
    return mode


def outcome(source):
    value = source.get("recognized")
    return value if isinstance(value, bool) else None


def accepts(value, mode):
    validate(mode)
    return mode == "all" or (mode == "recognized" and value is True) or (mode == "unrecognized" and value is False) or (mode == "unknown" and value is None)


def from_original(original):
    from .dataset import _unique_pairs
    try:
        source = json.loads(original, object_pairs_hook=_unique_pairs)
        return outcome(source) if isinstance(source, dict) else None
    except ValueError:
        return None


def from_row(folder, row):
    """Legacy journals remain readable without rewriting any acquisition."""
    if "recognized" in row:
        return outcome(row)
    from .dataset import _safe_path
    relative = row.get("evidencePath") or row.get("retainedSource")
    if not relative:
        return None
    with gzip.open(_safe_path(folder, relative), "rt", encoding="utf-8") as stream:
        packet = json.load(stream)
    if isinstance(packet.get("source"), dict):
        return outcome(packet["source"])
    return from_original(packet.get("sourceOriginalJson", ""))
