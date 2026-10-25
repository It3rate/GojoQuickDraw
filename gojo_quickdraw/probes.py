"""On-demand, non-mutating interventions over explicitly exposed candidates.

These tests compare conditional construction machines, not real object motion,
causal identification or learned likelihood. They retain ties: a centered repeat
can have exactly the same response as a mirror under a spacing intervention.
No hidden original drawing or private precise target is consulted.
"""
from __future__ import annotations

import math


def _center(box):
    return [(box[0] + box[2]) / 2, (box[1] + box[3]) / 2]


def _shift(box, dx=0., dy=0.):
    return [box[0] + dx, box[1] + dy, box[2] + dx, box[3] + dy]


def _outside(inner, outer):
    return [max(0., outer[0] - inner[0]), max(0., outer[1] - inner[1]),
            max(0., inner[2] - outer[2]), max(0., inner[3] - outer[3])]


def _constraint_reads(state, context, pair=None):
    reads = []
    if pair is not None:
        first, second, parent = pair["participants"]
        a, b = _center(state[first]), _center(state[second])
        # The independent candidate scope remains fixed during this local probe.
        axis = pair["expected"]["axisX"]
        reads.extend([
            {"id": pair["id"] + ":symmetry", "kind": "midpoint-on-axis",
             "participants": [first, second, parent], "residue": abs((a[0] + b[0])/2 - axis),
             "basis": "normalized page x", "origin": "explicit seeded placement expectation"},
            {"id": pair["id"] + ":level", "kind": "common-height", "participants": [first, second],
             "residue": abs(a[1] - b[1]), "basis": "normalized page y",
             "origin": "explicit seeded placement expectation"},
        ])
    for relation in context.get("relations", []):
        if relation["kind"] != "containment" or any(pid not in state for pid in relation["participants"]):
            continue
        parent, child = relation["participants"]
        outside = _outside(state[child], state[parent])
        reads.append({"id": relation["id"], "kind": "bounding-frame-containment", "participants": [parent, child],
                      "residue": sum(outside), "bySide": outside, "basis": "normalized page outward distances",
                      "origin": "explicit conditional enclosure expectation"})
    return reads


def _total(reads):
    # Deliberately only equal-unit local distances; never probabilities.
    return sum(read["residue"] for read in reads)


def _variant(name, before, after, reads_before, context, pair=None, *, controls, qualification):
    reads = _constraint_reads(after, context, pair)
    shifts = {key: [_center(after[key])[j] - _center(before[key])[j] for j in range(2)] for key in after
              if after[key] != before[key]}
    return {"machine": name, "controls": controls, "boundsAfter": after, "centerDisplacements": shifts,
            "reads": reads, "sumDistanceResidue": _total(reads),
            "changeInDistanceResidue": _total(reads) - _total(reads_before), "qualification": qualification}


def _pair_probe(pair, context, parts, delta):
    if len(pair.get("participants", [])) != 3:
        return None
    first, second, parent = pair["participants"]
    if any(pid not in parts or "bounds" not in parts[pid] for pid in pair["participants"]):
        return None
    before = {pid: list(parts[pid]["bounds"]) for pid in pair["participants"]}
    reads_before = _constraint_reads(before, context, pair)
    variants = []
    # Same target intervention for all machines: right occurrence moves +delta.
    # Each machine explicitly states which other control is held fixed.
    for machine, left_delta, controls in (
            ("mirror-around-fixed-axis", -delta, {"fixed": ["axis", "template size"], "changed": {"halfSpacing": delta}}),
            ("translation-from-fixed-first", 0., {"fixed": ["first occurrence", "template size"], "changed": {"pitch": delta}}),
            ("translation-around-fixed-center", -delta, {"fixed": ["repeat center", "template size"], "changed": {"pitch": 2*delta}})):
        after = {pid: list(box) for pid, box in before.items()}
        after[first] = _shift(after[first], left_delta)
        after[second] = _shift(after[second], delta)
        variants.append(_variant(machine, before, after, reads_before, context, pair,
            controls=controls, qualification="A local differential response preserves existing asymmetry; it does not first snap the source into an ideal machine."))
    groups = []
    for variant in variants:
        match = next((group for group in groups if math.isclose(group["sumDistanceResidue"], variant["sumDistanceResidue"], abs_tol=1e-9)), None)
        if match is None:
            match = {"sumDistanceResidue": variant["sumDistanceResidue"], "machines": []}
            groups.append(match)
        match["machines"].append(variant["machine"])
    return {"id": "probe-" + pair["id"], "kind": "paired-intervention", "relationId": pair["id"],
            "participants": pair["participants"], "sourceSpanIds": list(pair.get("spans", [])),
            "intervention": {"rightOccurrenceDeltaX": delta, "commonToAllMachines": True},
            "boundsBefore": before, "readsBefore": reads_before, "variants": variants,
            "ties": [group for group in groups if len(group["machines"]) > 1],
            "qualification": "Self-consistency under exposed controls only. Mirror and centered translation can be observationally equivalent. A worse anchored-translation result does not refute translation; the anchoring premise differs."}


def _containment_probe(relation, context, parts, delta):
    if len(relation.get("participants", [])) != 2:
        return None
    parent, child = relation["participants"]
    if any(pid not in parts or "bounds" not in parts[pid] for pid in (parent, child)):
        return None
    original = {pid: list(parts[pid]["bounds"]) for pid in (parent, child)}
    wall, opening = original[parent], original[child]
    # Request the same explicit child displacement once. The responses below all
    # start at this one perturbed state; nothing silently changes the request.
    requested = {parent: list(wall), child: _shift(opening, delta)}
    before_reads = _constraint_reads(requested, context)
    scenarios = []
    alternatives = [("retain-conflict", {pid: list(box) for pid, box in requested.items()},
                     {"fixed": [parent, child], "changed": {}}, "No response is selected; violated containment remains visible.")]
    wall_width, wall_height = wall[2]-wall[0], wall[3]-wall[1]
    child_width, child_height = opening[2]-opening[0], opening[3]-opening[1]
    if child_width <= wall_width and child_height <= wall_height:
        box = requested[child]
        dx = wall[0]-box[0] if box[0] < wall[0] else wall[2]-box[2] if box[2] > wall[2] else 0.
        dy = wall[1]-box[1] if box[1] < wall[1] else wall[3]-box[3] if box[3] > wall[3] else 0.
        alternatives.append(("move-child", {parent: list(wall), child: _shift(box, dx, dy)},
                             {"fixed": [parent], "changed": {child: [dx, dy]}},
                             "Child location is adjustable; the request is partly reversed if containment requires it."))
        alternatives.append(("move-container", {parent: _shift(wall, -dx, -dy), child: list(box)},
                             {"fixed": [child], "changed": {parent: [-dx, -dy]}, "couplingOverride": "child explicitly anchored"},
                             "Fit the parent frame to an anchored child, explicitly overriding the carry default. This is boundary interpretation, not placement of the whole parent group; other supports remain unknown."))
    expanded = [min(wall[0], requested[child][0]), min(wall[1], requested[child][1]),
                max(wall[2], requested[child][2]), max(wall[3], requested[child][3])]
    alternatives.append(("resize-container", {parent: expanded, child: list(requested[child])},
                         {"fixed": [child], "changed": {parent: "expand only the exceeded bounds"}},
                         "Container size is adjustable; no structural mechanics or material cost is inferred."))
    for name, state, controls, qualification in alternatives:
        scenarios.append(_variant(name, requested, state, before_reads, context, controls=controls, qualification=qualification))
    return {"id": "probe-" + relation["id"], "kind": "bidirectional-containment", "relationId": relation["id"],
            "participants": [parent, child], "sourceSpanIds": list(relation.get("spans", [])),
            "boundsBefore": original, "intervention": {"childDeltaX": delta}, "boundsRequested": requested,
            "readsRequested": before_reads, "variants": scenarios,
            "unavailableResponses": [] if child_width <= wall_width and child_height <= wall_height else ["Translation alone cannot fit a child larger than the container."],
            "qualification": "Explicit alternative response routes, not an automatic global solver. No preferred participant is chosen without resistance/reliability evidence. Other wall attachments and physical support remain unresolved."}


def run_probes(context, delta=12.):
    """Read only public context bounds; return bounded ephemeral witnesses."""
    if not isinstance(delta, (int, float)) or not math.isfinite(delta):
        raise ValueError("Intervention delta must be finite")
    parts = {part["id"]: part for part in context.get("parts", [])}
    results = []
    for relation in context.get("relations", []):
        if len(results) >= 24:
            break
        if relation["kind"] == "paired-placement":
            result = _pair_probe(relation, context, parts, delta)
        elif relation["kind"] == "containment":
            result = _containment_probe(relation, context, parts, delta)
        else:
            continue
        if result:
            results.append(result)
    movement = []
    for parent in dict.fromkeys(r["participants"][0] for r in context.get("relations", []) if r["kind"] == "containment" and len(r.get("participants", [])) == 2):
        if len(movement) >= 24:
            break
        child = next(r["participants"][1] for r in context["relations"] if r["kind"] == "containment" and r["participants"][0] == parent)
        movement.append({"container": parent, "child": child,
                         "moveChild": move_contained(context, child, delta, carry=False),
                         "moveContainer": move_contained(context, parent, delta, carry=True),
                         "qualification": "Revisable frame-coupling default; geometric containment alone does not prove physical transport."})
    return {"schema": "gojo.quickdraw-probes.v1", "sourceId": context.get("sourceId"), "probes": results,
            "containmentMovement": movement,
            "sourceContract": "Only the supplied candidate bounds, participants and constraints were read; no private drawing access.",
            "status": "exploratory conditional adapter computations; not trained probabilities or native dynamic settlement",
            "limitations": ["Shared outcomes under tested interventions do not identify a unique machine.",
                            "A distance residue is an observation, not automatically carrier tension.",
                            "Candidate feedback routes still require a selected native application to move drawing structure."]}


def move_contained(context, participant, dx, dy=0., *, carry=True):
    """Counterfactual placement through the proposed containment frame coupling.

    A descendant receives the displacement once even with nested/multiple routes.
    Input geometry is never changed. Alternative parents and cycles stay visible.
    """
    if any(isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v) for v in (dx, dy)):
        raise ValueError("Movement must be finite")
    state = {p["id"]: list(p["bounds"]) for p in context.get("parts", []) if "bounds" in p}
    if participant not in state:
        raise ValueError("Movement participant has no exposed bounds")
    children, parents = {}, {}
    for row in context.get("relations", []):
        if row["kind"] != "containment" or len(row.get("participants", [])) != 2:
            continue
        parent, child = row["participants"]
        children.setdefault(parent, set()).add(child); parents.setdefault(child, set()).add(parent)
    selected, pending, unresolved = set(), [participant], []
    while pending:
        current = pending.pop()
        if current in selected:
            continue
        if current not in state:
            unresolved.append("No exposed bounds for " + current); continue
        selected.add(current)
        if carry:
            for child in sorted(children.get(current, [])):
                if child == participant:
                    unresolved.append("Containment cycle returns to " + participant)
                if len(parents.get(child, [])) > 1:
                    unresolved.append("Alternative parent routes for " + child)
                if child not in selected:
                    pending.append(child)
    before = {key: list(value) for key, value in state.items()}
    for key in selected:
        state[key] = _shift(state[key], dx, dy)
    return {"participant": participant, "displacement": [dx, dy], "carryDescendants": carry,
            "boundsBefore": before, "boundsAfter": state, "movedParticipants": sorted(selected),
            "reads": _constraint_reads(state, context), "unresolved": list(dict.fromkeys(unresolved)),
            "qualification": "Authored conditional placement response, not observed motion or native dynamic settlement."}


def example_context():
    """A labelled fixture with two windows, a door and an exposed wall frame."""
    parts = [{"id": "wall", "kind": "body", "bounds": [0., 0., 100., 100.]},
             {"id": "left-window", "kind": "window", "bounds": [15., 25., 25., 45.]},
             {"id": "right-window", "kind": "window", "bounds": [75., 25., 85., 45.]},
             {"id": "door", "kind": "door", "bounds": [40., 65., 60., 100.]}]
    relations = [{"id": "window-pair", "kind": "paired-placement", "participants": ["left-window", "right-window", "wall"],
                  "spans": [], "expected": {"axisX": 50., "origin": "explicit fixture seed"}}]
    for child in ("left-window", "right-window", "door"):
        relations.append({"id": "inside-" + child, "kind": "containment", "participants": ["wall", child],
                          "spans": [], "expected": {"permittedOutside": 0}})
    return {"schema": "gojo.quickdraw-context.v1", "sourceId": "synthetic-window-machine-fixture",
            "parts": parts, "relations": relations, "qualification": "Hand-authored test fixture, not observed QuickDraw evidence."}
