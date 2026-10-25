"""Presentation reads of provisional object memberships; no geometry mutation."""
from __future__ import annotations

COLORS = {"wall": "#277da8", "building": "#34536d", "roof": "#d07926",
          "steeple": "#8b5bb6", "cross": "#b9406a", "door": "#249578",
          "window": "#ad8319", "unknown": "#92999e"}


def read(packet):
    objects = packet.get("objects") or {}
    candidates = objects.get("candidates", [])
    unresolved = set(objects.get("unknownSpanIds", [])) | set(objects.get("ambiguousSpanIds", []))
    memberships = {s["id"]: [] for s in packet["observation"]["spans"]}
    for candidate in candidates:
        for span in candidate.get("spans", []):
            if span in memberships:
                memberships[span].append(candidate)
    spans = {}
    for span, groups in memberships.items():
        # Assemblies can share support with a wall without making the wall's
        # presentation ambiguous. Distinct competing direct memberships cannot.
        direct = [g for g in groups if g.get("role") != "building" and g.get("focusKind") != "assembly"]
        roles = {g.get("role", "unknown") for g in direct}
        role = next(iter(roles)) if len(roles) == 1 and span not in unresolved else "unknown"
        if role not in COLORS:
            role = "unknown"
        spans[span] = {"role": role, "color": COLORS[role], "candidates": [g["id"] for g in groups]}
    return {"available": bool(objects), "colors": COLORS, "spans": spans,
            "candidates": len(candidates), "unknown": len(objects.get("unknownSpanIds", [])),
            "ambiguous": len(objects.get("ambiguousSpanIds", []))}


def profile_color(view, span_ids):
    """A merged profile inherits a role only if all its support agrees."""
    values = [view["spans"].get(key, {"role": "unknown"})["role"] for key in span_ids]
    role = values[0] if values and len(set(values)) == 1 else "unknown"
    return COLORS.get(role, COLORS["unknown"])
