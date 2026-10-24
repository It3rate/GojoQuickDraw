"""Bounded, source-supported contextual proposals for the native Gojo bridge.

These dictionaries are adapters, not the canonical meaning. The bridge connects
their actual profile selections, geometric expectation witnesses and open slots.
No inferred boundary, shared site, view or physical scale becomes observed ink.
"""
from __future__ import annotations

import itertools
import math
from copy import deepcopy


POLICY = {
    "basis": "normalized observer page; positive y points down on this page only",
    "learned": False,
    "maximumWallSpans": 24,
    "maximumBodyCandidates": 3,
    "maximumOpeningCandidates": 16,
    "maximumRoofCandidates": 8,
    "maximumPairCandidates": 12,
    "maximumViewWitnesses": 32,
    "uprightSlopeRatio": .48,
    "minimumWallFraction": .18,
    "closureGapFraction": .22,
    "interpretation": "authored conditional hypotheses, not probabilities or physical proof",
}


def _bounds(points):
    return [min(p[0] for p in points), min(p[1] for p in points),
            max(p[0] for p in points), max(p[1] for p in points)]


def _center(box):
    return [(box[0] + box[2]) / 2, (box[1] + box[3]) / 2]


def _size(box):
    return [box[2] - box[0], box[3] - box[1]]


def _rectangle(box):
    left, top, right, bottom = box
    return {"points": [[left, top], [right, top], [right, bottom], [left, bottom]],
            "segments": [[0, 1], [1, 2], [2, 3], [3, 0]]}


def _outside(inner, outer):
    """Four directional read remainders, not a fill/contact proof."""
    return [max(0., outer[0] - inner[0]), max(0., outer[1] - inner[1]),
            max(0., inner[2] - outer[2]), max(0., inner[3] - outer[3])]


def _span_bounds(span):
    return _bounds(span["controls"])


def _endpoints(span):
    return [span["controls"][0], span["controls"][-1]]


def _extent(span):
    return math.dist(*_endpoints(span))


def _part(key, kind, spans, box, *, geometry=None, annotations=(), expected=None,
          measured=None, residue=()):
    return {"id": key, "kind": kind, "status": "seeded-candidate", "spans": list(dict.fromkeys(spans)),
            "annotationIds": list(annotations), "bounds": list(box),
            "geometry": deepcopy(geometry) if geometry else _rectangle(box), "expected": deepcopy(expected) if expected else {},
            "measured": deepcopy(measured) if measured else {}, "residue": list(residue),
            "qualification": "Observed profiles support a proposed role; candidate geometry remains an expectation, not acquired ink."}


def _bodies(observation, by_id):
    bounds = observation.get("bounds")
    if not bounds:
        return []
    width, height = _size(bounds)
    if width <= 0 or height <= 0:
        return []
    crosses = {s for row in observation.get("crosses", []) for s in row["spans"]}
    upright = []
    for span in by_id.values():
        a, b = _endpoints(span)
        dx, dy = abs(b[0] - a[0]), abs(b[1] - a[1])
        if (span["id"] not in crosses and dy >= POLICY["minimumWallFraction"] * height
                and dx <= POLICY["uprightSlopeRatio"] * dy
                and max(a[1], b[1]) >= bounds[1] + .64 * height):
            upright.append(span)
    upright.sort(key=lambda span: (-_extent(span), span["id"]))
    options = []
    for first, second in itertools.combinations(upright[:POLICY["maximumWallSpans"]], 2):
        left, right = sorted((first, second), key=lambda span: _center(_span_bounds(span))[0])
        lb, rb = _span_bounds(left), _span_bounds(right)
        x0, x1 = _center(lb)[0], _center(rb)[0]
        overlap = min(lb[3], rb[3]) - max(lb[1], rb[1])
        if x1 - x0 < .22 * width or overlap <= .25 * min(_size(lb)[1], _size(rb)[1]):
            continue
        box = [x0, (lb[1] + rb[1]) / 2, x1, (lb[3] + rb[3]) / 2]
        support = [left["id"], right["id"]]
        for span in by_id.values():
            a, b = _endpoints(span)
            if abs(b[1] - a[1]) > .40 * abs(b[0] - a[0]):
                continue
            sb = _span_bounds(span)
            horizontal_overlap = min(sb[2], x1) - max(sb[0], x0)
            if (horizontal_overlap >= .35 * (x1 - x0)
                    and min(abs(_center(sb)[1] - box[1]), abs(_center(sb)[1] - box[3])) < .14 * height):
                support.append(span["id"])
        corner_reads = []
        for xy in _rectangle(box)["points"]:
            candidates = [(math.dist(xy, p), sid, endpoint) for sid in support
                          for endpoint, p in enumerate(_endpoints(by_id[sid]))]
            distance, sid, endpoint = min(candidates)
            corner_reads.append({"point": xy, "nearestSpanId": sid, "endpoint": endpoint,
                                 "distance": distance, "status": "weak" if distance > .06 * max(width, height) else "near"})
        area = (x1 - x0) * max(0, box[3] - box[1])
        options.append((area, support, box, corner_reads))
    options.sort(key=lambda entry: (-entry[0], entry[1]))
    result = []
    for _, support, box, corner_reads in options:
        if any(sum(abs(a-b) for a, b in zip(box, item["bounds"])) < .1 * (width + height) for item in result):
            continue
        result.append(_part(f"part-body-{len(result):03}", "body", support, box,
            expected={"construction": "four-corner facade candidate", "boundaryRole": "possible containing wall",
                      "alternatives": ["projected quadrilateral", "open outline", "unrelated upright spans"]},
            measured={"cornerReads": corner_reads, "supportedWallSpanIds": support[:2]},
            residue=["Four rectangle corners are a seeded expectation; proximity does not establish shared sites.",
                     "A missing edge remains open; a triangular or perspective interpretation is not disproved."]))
        if len(result) == POLICY["maximumBodyCandidates"]:
            break
    return result


def _openings(observation, by_id, body, excluded):
    if body is None:
        return []
    wall = body["bounds"]
    width, height = _size(wall)
    result = []
    for stroke in observation.get("strokes", []):
        support = [sid for sid in stroke.get("spans", []) if sid in by_id and sid not in excluded]
        if not support or len(support) != len(stroke.get("spans", [])):
            continue
        points = [p for sid in support for p in by_id[sid]["controls"]]
        box = _bounds(points)
        w, h = _size(box)
        if w < .025 * width or h < .04 * height or w > .55 * width or h > 1.35 * height:
            continue
        if sum(_outside(box, wall)) > .18 * (width + height):
            continue
        center = _center(box)
        if not (wall[0] - .1 * width <= center[0] <= wall[2] + .1 * width
                and wall[1] - .1 * height <= center[1] <= wall[3] + .1 * height):
            continue
        first = by_id[support[0]]["controls"][0]
        last = by_id[support[-1]]["controls"][-1]
        gap = math.dist(first, last)
        close = gap <= POLICY["closureGapFraction"] * max(w, h)
        lower_terminals = min(first[1], last[1]) >= box[1] + .65 * h
        verticals = [sid for sid in support if abs(_endpoints(by_id[sid])[1][1] - _endpoints(by_id[sid])[0][1])
                     >= 1.5 * abs(_endpoints(by_id[sid])[1][0] - _endpoints(by_id[sid])[0][0])]
        curved = any(len(by_id[sid]["controls"]) > 2 for sid in support)
        open_arch = lower_terminals and (len(verticals) >= 2 or curved)
        if not close and not open_arch:
            continue
        low_center = abs(center[0] - _center(wall)[0]) <= .25 * width and box[3] >= wall[1] + .78 * height
        kind = "door" if low_center else "window"
        result.append(_part(f"part-opening-{len(result):03}", kind, support, box,
            expected={"construction": "opening candidate", "container": body["id"],
                      "alternatives": ["window", "door", "decorative closed contour"],
                      "roleCondition": "lower central placement" if low_center else "small interior contour"},
            measured={"endpointGap": gap, "closureScale": max(w, h), "closureSupported": close,
                      "openAtBottom": open_arch and not close},
            residue=["Door/window meaning is seeded and may be wrong; this contour can be decoration.",
                     "Bounds are an expected scaffold; the actual contour can be curved or irregular."]))
        if len(result) == POLICY["maximumOpeningCandidates"]:
            break
    return result


def _relation(key, kind, participants, spans, expected, measured, residue=()):
    return {"id": key, "kind": kind, "status": "seeded-candidate", "participants": list(participants),
            "spans": list(dict.fromkeys(spans)), "expected": expected, "measured": measured,
            "residue": list(residue)}


def build_context(observation, annotations, category="church"):
    """Return deterministic bounded proposals without changing either input.

    The church vocabulary is an explicit seed. Non-church records retain only
    open frame/scale assumptions, avoiding borrowed category interpretations.
    """
    annotations = annotations if annotations is not None else {"sourceId": observation.get("sourceId")}
    by_id = {span["id"]: span for span in observation.get("spans", [])}
    if len(by_id) != len(observation.get("spans", [])):
        raise ValueError("Duplicate source interval identities")
    if annotations.get("sourceId") != observation.get("sourceId"):
        raise ValueError("Context and annotation source identities differ")
    parts, relations, assumptions, views = [], [], [], []
    source_id = observation.get("sourceId")
    if category.casefold() == "church":
        parts.extend(_bodies(observation, by_id))
        body = parts[0] if parts else None
        roof_reads = [r for r in annotations.get("relations", []) if r["kind"] == "roof-slopes"]
        for row in roof_reads[:POLICY["maximumRoofCandidates"]]:
            support = [sid for sid in row["spans"] if sid in by_id]
            if len(support) < 2:
                continue
            apex = row["point"]
            ends = [max(_endpoints(by_id[sid]), key=lambda p: math.dist(p, apex)) for sid in support[:2]]
            geometry = {"points": [ends[0], list(apex), ends[1]], "segments": [[0, 1], [1, 2]]}
            box = _bounds(geometry["points"])
            scene = observation.get("bounds")
            if scene and (apex[1] > scene[1] + .52 * _size(scene)[1] or _size(box)[0] < .08 * _size(scene)[0]):
                continue
            parts.append(_part(f"part-roof-{len([p for p in parts if p['kind'] == 'roof']):03}", "roof", support, box,
                geometry=geometry, annotations=[row["id"]],
                expected={"construction": "upper converging pair", "alternatives": ["roof", "peak", "unrelated angled strokes"]},
                measured=dict(row.get("measured", {})), residue=["The lower triangle edge is not asserted or added."]))
            if body and _size(box)[0] < .65 * _size(body["bounds"])[0]:
                parts.append(_part(f"part-steeple-{len([p for p in parts if p['kind'] == 'steeple']):03}", "steeple", support, box,
                    geometry=geometry, annotations=[row["id"]], expected={"construction": "narrow upper peak", "parent": body["id"]},
                    residue=["Steeple is an alternative interpretation of the same roof evidence, not an extra counted object."]))
        for index, cross in enumerate(observation.get("crosses", [])[:8]):
            support = [sid for sid in cross["spans"] if sid in by_id]
            if len(support) != 2:
                continue
            points = [point for sid in support for point in _endpoints(by_id[sid])]
            parts.append(_part(f"part-cross-{index:03}", "cross", support, _bounds(points),
                geometry={"points": points, "segments": [[0, 1], [2, 3]]},
                expected={"construction": "two crossing arms", "alternatives": ["cross", "incidental intersection"]},
                measured={"intersection": cross["point"], "rightAngleResidualDegrees": cross.get("rightAngleResidualDegrees")},
                residue=["Crossing profiles do not establish shared acquisition identity or semantic purpose."]))
        # Alternative body hypotheses may include a door upright; they cannot
        # consume that interval and prevent a competing opening interpretation.
        excluded = {sid for part in parts if part["kind"] != "body" or part is body for sid in part["spans"]}
        openings = _openings(observation, by_id, body, excluded)
        parts.extend(openings)
        if body:
            body_id = body["id"]
            for opening in openings:
                outside = _outside(opening["bounds"], body["bounds"])
                relations.append(_relation(f"containment-{opening['id']}", "containment", [body_id, opening["id"]],
                    body["spans"] + opening["spans"], {"order": ["container", "contained"], "permittedOutside": 0,
                     "scope": "candidate wall bounds", "adjustableParticipants": [body_id, opening["id"]]},
                    {"outsideBySide": outside, "outsideSum": sum(outside)},
                    ["Bounding-frame inclusion is conditional; actual occupied wall fill remains unproven.",
                     "Which participant should move depends on evidence and resistance; neither is fixed by drawing order."]))
                relations.append(_relation(f"closure-{opening['id']}", "closure", [opening["id"]], opening["spans"],
                    {"endpointCoincidence": "possible", "optionalClosure": True},
                    {"endpointGap": opening["measured"]["endpointGap"], "supported": opening["measured"]["closureSupported"]},
                    ["Closing a contour and containing it in a wall are independent claims."]))
            for first, second in itertools.combinations(openings, 2):
                if len([r for r in relations if r["kind"] == "paired-placement"]) >= POLICY["maximumPairCandidates"]:
                    break
                wa, ha = _size(first["bounds"])
                wb, hb = _size(second["bounds"])
                if min(wa, wb) / max(wa, wb) < .5 or min(ha, hb) / max(ha, hb) < .5:
                    continue
                a, b = sorted((first, second), key=lambda p: _center(p["bounds"])[0])
                ca, cb = _center(a["bounds"]), _center(b["bounds"])
                width, height = _size(body["bounds"])
                if cb[0] - ca[0] < .05 * width or abs(ca[1] - cb[1]) > .25 * height:
                    continue
                axis = _center(body["bounds"])[0]
                relations.append(_relation(f"pair-{a['id']}-{b['id']}", "paired-placement", [a["id"], b["id"], body_id],
                    a["spans"] + b["spans"] + body["spans"],
                    {"alternatives": ["mirror", "anchored translation", "centered translation", "unrelated similar parts"],
                     "axisX": axis, "scope": body_id, "count": 2, "equalHeight": "seeded candidate"},
                    {"midpointOffset": (ca[0] + cb[0]) / 2 - axis, "heightDifference": cb[1] - ca[1],
                     "shapeExtentDifference": [wb-wa, hb-ha]},
                    ["The controller is unresolved; a centered repeat and a mirror may predict the same intervention.",
                     "A seeded symmetry test is not independent empirical support for its own prior."]))
            left, top, right, bottom = body["bounds"]
            assumptions.append({"id": "assumption-ground", "kind": "support-ground", "status": "seeded-hidden",
                "participants": [body_id], "spans": body["measured"]["supportedWallSpanIds"],
                "geometry": {"points": [[left, bottom], [right, bottom]], "segments": [[0, 1]]},
                "expected": {"role": "possible support at lower wall extent", "drawn": False},
                "residue": ["No ground stroke is asserted. Bottom support is a church-context seed and can be absent or misplaced."]})
    else:
        body = None

    # A page basis does not identify a camera or a gravitational frame.
    for kind in ("front", "oblique-left", "oblique-right", "top", "unknown"):
        support, spans, annotation_ids = [], [], []
        if category.casefold() == "church" and kind == "front":
            for row in annotations.get("symmetries", []):
                axis = row.get("axis", {})
                if axis.get("from", [0, 0])[0] == axis.get("to", [1, 1])[0] and row["measured"].get("scope") == "whole drawing":
                    support.append({"read": "vertical-reflection-coverage", "value": row["measured"].get("coverage", 0),
                                    "qualification": "Partial symmetry supports an alternative; it does not establish frontal view."})
                    spans.extend(row["spans"])
                    annotation_ids.append(row["id"])
        elif body and kind.startswith("oblique"):
            side = -1 if kind.endswith("left") else 1
            wall = body["bounds"]
            for span in by_id.values():
                if len(support) >= POLICY["maximumViewWitnesses"]:
                    break
                a, b = _endpoints(span)
                dx, dy = abs(b[0]-a[0]), abs(b[1]-a[1])
                center = _center(_span_bounds(span))
                outside = center[0] < wall[0] if side < 0 else center[0] > wall[2]
                if outside and .2*dx < dy < 2*dx and _extent(span) > .12*max(_size(wall)):
                    support.append({"read": "external-sloping-profile", "spanId": span["id"], "value": _extent(span),
                                    "qualification": "Could be depth, roof or decoration; no camera identification."})
                    spans.append(span["id"])
            for joint in annotations.get("joints", []):
                if len(support) >= POLICY["maximumViewWitnesses"]:
                    break
                if joint.get("rayCount") == 3 and joint.get("point"):
                    x = joint["point"][0]
                    if abs(x - (wall[0] if side < 0 else wall[2])) < .12 * _size(wall)[0]:
                        support.append({"read": "three-ray-boundary-junction", "value": 3,
                                        "qualification": "A Y/T-like junction is compatible with several explanations."})
                        spans.extend(joint["spans"])
                        annotation_ids.append(joint["id"])
        views.append({"id": f"view-{kind}", "kind": kind,
                      "status": "supported-candidate" if support else "open", "participants": [body["id"]] if body else [],
                      "spans": list(dict.fromkeys(spans)), "annotationIds": annotation_ids, "support": support,
                      "selection": {"maximumWitnesses": POLICY["maximumViewWitnesses"],
                                    "capacityReached": len(support) >= POLICY["maximumViewWitnesses"],
                                    "complement": "All original annotations remain available outside this bounded context read."},
                      "residue": ["No calibrated camera, perspective or physical upright frame is supplied.",
                                  "Top view remains open without discriminating evidence." if kind == "top" else "Alternatives are retained; support is not a probability."]})
    for kind in ("physical-scale", "gravity-direction", "support-material", "camera-elevation"):
        assumptions.append({"id": "assumption-" + kind, "kind": kind, "status": "open",
                            "participants": [body["id"]] if body else [], "spans": [],
                            "openSlots": [kind], "expected": {},
                            "residue": ["Not recoverable from normalized page geometry alone; no physical simulation is claimed."]})
    used = {sid for part in parts for sid in part["spans"]}
    return {"schema": "gojo.quickdraw-context.v1", "sourceId": source_id, "category": category,
            "parts": parts, "relations": relations, "assumptions": assumptions, "views": views,
            "unassignedSpanIds": [sid for sid in by_id if sid not in used], "policy": dict(POLICY),
            "qualification": "Exploratory source-linked candidates and explicit seeds. No learning, physical-scale inference, source mutation, shared-site merge or automatic feedback settlement."}


# Short name for callers treating extraction as a pure pipeline stage.
build = build_context
