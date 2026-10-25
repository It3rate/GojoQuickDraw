"""Replaceable finite opinion providers over explicit, source-linked requests.

Names are adapter vocabulary. Native import connects actual source profiles,
expected sites/edges, earlier claims and the existing support/opposition ladder.
No inferred joint edits acquisition and no hypothesis becomes independent ink.
"""
from __future__ import annotations

from dataclasses import dataclass, asdict
from copy import deepcopy
import itertools
import math
from types import MappingProxyType
from typing import Mapping, Protocol

GRADES = ("none", "weak", "medium", "strong")


@dataclass(frozen=True)
class Comparison:
    actual: float
    expected: float
    allowance: float
    basis: str


@dataclass(frozen=True)
class Request:
    id: str
    question: str
    spans: tuple[str, ...]
    dependencies: tuple[str, ...]
    comparisons: tuple[Comparison, ...]
    context: str
    profiles: tuple[dict, ...]
    expected: dict
    earlier: tuple[dict, ...]
    frame: dict
    context_parts: tuple[str, ...] = ()
    parts: tuple[Mapping, ...] = ()


@dataclass(frozen=True)
class Response:
    support: str
    opposition: str
    provider: str
    qualification: str


class Provider(Protocol):
    def __call__(self, request: Request) -> Response: ...


def heuristic(request: Request) -> Response:
    """Ordinal authored opinion, deliberately not a calibrated probability."""
    if request.context == "defaults":
        return Response("weak", "none", "authored-default.v1", "Revisable logical expectation, not an observation.")
    if not request.comparisons:
        response = Response("weak", "none", "authored-structure.v1", "Source-supported candidate; semantic role remains open.")
    else:
        within = [abs(c.actual-c.expected) <= c.allowance for c in request.comparisons]
        response = Response("medium" if all(within) else "weak" if any(within) else "none",
                            "none" if all(within) else "strong", "finite-comparison.v1",
                            "Independent comparison faces retain disagreement; grades are ordinal and unlearned.")
    if request.question == "object-role-candidate" and request.parts:
        part = request.parts[0]
        unresolved = part.get("role", part.get("kind")) == "unknown" or part.get("status") == "unresolved"
        ambiguous = part.get("status") == "ambiguous-candidate" or bool(part.get("conflicts"))
        if unresolved or ambiguous:
            opposition = max((response.opposition, "weak"), key=GRADES.index)
            return Response("none" if unresolved else response.support, opposition, "authored-object-role.v1",
                            "The grouping is retained. Unassigned or competing roles oppose committing to one role; this is an authored restraint, not measured negative ink or a probability.")
    return response


def rectangle(box):
    l, t, r, b = box
    return {"points": [[l, t], [r, t], [r, b], [l, b]], "segments": [[0, 1], [1, 2], [2, 3], [3, 0]]}


def _box(points):
    return [min(p[0] for p in points), min(p[1] for p in points), max(p[0] for p in points), max(p[1] for p in points)]


def _readonly(value):
    """Freeze an already copied adapter read; providers cannot edit the source."""
    if isinstance(value, dict):
        return MappingProxyType({key: _readonly(item) for key, item in value.items()})
    if isinstance(value, (list, tuple)):
        return tuple(_readonly(item) for item in value)
    return value


def _comparisons(item):
    return tuple(Comparison(value["actual"], value["expected"], value["allowance"], value["basis"])
                 for value in item.get("comparisons", []))


def build(packet, proposal=None, providers=None, maximum=1024):
    """Publish candidate requests and responses with explicit dependency order.

    An injected provider can replace any question's heuristic through the same
    contract. It cannot invent source IDs, erase contrary reads or mutate ink.
    """
    providers = providers or {}
    if isinstance(maximum, bool) or not isinstance(maximum, int) or maximum < 1:
        raise ValueError("Opinion capacity must be a positive integer")
    observation = packet["observation"]
    spans = {s["id"]: s for s in observation.get("spans", [])}
    context = packet.get("context", {})
    parts_by_id = {part["id"]: part for part in context.get("parts", [])}
    if len(parts_by_id) != len(context.get("parts", [])):
        raise ValueError("Context candidates must have distinct identities")
    rows, skipped = [], []
    known = set()

    def emit(key, question, support, *, dependencies=(), geometry=None, comparisons=(),
             mode="hypotheses", meaning="", annotations=(), context_parts=()):
        if key in known:
            return key
        if len(rows) >= maximum:
            skipped.append(key)
            return None
        support = tuple(dict.fromkeys(support))
        dependencies = tuple(dict.fromkeys(dependencies))
        selected_parts = tuple(dict.fromkeys(context_parts))
        if any(s not in spans for s in support) or any(d not in known for d in dependencies):
            raise ValueError("An opinion references missing or forward support")
        if any(part not in parts_by_id for part in selected_parts):
            raise ValueError("An opinion references a missing context part")
        expected_geometry = geometry or {"points": [], "segments": []}
        earlier = tuple({name: deepcopy(row[name]) for name in ("id", "kind", "spans", "contextParts", "geometry", "support", "opposition", "context")}
                        for row in rows if row["id"] in dependencies)
        request = Request(key, question, support, dependencies, tuple(comparisons), mode,
                          tuple(deepcopy(spans[s]) for s in support), deepcopy(expected_geometry), earlier,
                          deepcopy(observation.get("normalization", {})), selected_parts,
                          tuple(_readonly(deepcopy(parts_by_id[part])) for part in selected_parts))
        if any(not all(math.isfinite(v) for v in (c.actual, c.expected, c.allowance)) or c.allowance < 0 for c in comparisons):
            raise ValueError("Comparison values must be finite with a nonnegative allowance")
        response = providers.get(question, heuristic)(request)
        if response.support not in GRADES or response.opposition not in GRADES:
            raise ValueError("An opinion provider must use the declared ordinal evidence ladder")
        row = {"id": key, "kind": question, "context": mode, "spans": list(support),
               "contextParts": list(selected_parts),
               "dependencies": list(dependencies), "geometry": expected_geometry,
               "comparisons": [asdict(c) for c in comparisons], **asdict(response),
               "meaning": meaning, "annotationIds": list(annotations),
               "status": "conflicted" if response.support != "none" and response.opposition != "none" else
                         "opposed" if response.opposition != "none" else "supported-candidate" if response.support != "none" else "open",
               "qualification": response.qualification + " Source causes remain shared; this is not an extra independent observation."}
        rows.append(row); known.add(key)
        return key

    for span in spans.values():
        controls = span["controls"]
        line = span["lineError"]["max"] <= 6
        emit("trace-"+span["id"], "straight-trace" if line else "curved-trace", [span["id"]],
             geometry={"points": [controls[0], controls[-1]], "segments": [[0, 1]]} if line else None,
             comparisons=[Comparison(span["lineError"]["max"], 0, 6, "normalized page distance")] if line else (),
             mode="observations", meaning="Finite source straightness; curved support retains its actual native source profile.")

    for stroke in observation.get("strokes", []):
        support = stroke.get("spans", [])
        if not support:
            continue
        trace = emit("traversal-"+str(stroke["index"]), "connected-acquisition-traversal", support,
                     mode="observations", meaning="One recorded traversal, which can still contribute to several conceptual parts.")
        first, last = spans[support[0]]["controls"][0], spans[support[-1]]["controls"][-1]
        box = _box([p for s in support for p in spans[s]["controls"]]); extent = max(box[2]-box[0], box[3]-box[1])
        travel = sum(spans[s].get("pathLength", math.dist(spans[s]["controls"][0], spans[s]["controls"][-1])) for s in support)
        if trace and extent > 12 and travel > 2*extent and math.dist(first, last) <= .15*extent:
            grouped = emit("contour-part-"+str(stroke["index"]), "contour-membership-candidate", support, dependencies=[trace],
                           geometry=rectangle(box), comparisons=[Comparison(math.dist(first, last), 0, .15*extent, "normalized page distance")],
                           meaning="Near-closed contour may form one part. The rectangular envelope is a common frame, not a claim that the contour is rectangular.")
            if grouped:
                emit("contour-motion-"+str(stroke["index"]), "part-members-move-together", support, dependencies=[grouped], mode="defaults",
                     meaning="A proposed common part frame carries its participating lines; arbitrary line deformation is a separate edit.")

    joint_rows = []
    for joint in (packet.get("annotations") or {}).get("joints", []):
        if joint.get("kind") != "joint" or joint.get("rayCount", 0) < 2:
            continue
        centre = joint["point"]
        points = [centre]
        for ray in joint.get("rays", []):
            v = ray["vector"]; length = math.hypot(*v)
            if length:
                points.append([centre[j] + 20*v[j]/length for j in (0, 1)])
        key = emit("encounter-"+joint["id"], "join-candidate", joint["spans"],
                   geometry={"points": points, "segments": [[0, i] for i in range(1, len(points))]},
                   comparisons=[Comparison(joint["gap"], 0, joint["thresholds"]["proximity"], "normalized page distance")],
                   annotations=[joint["id"]],
                   meaning="Possible common target site. Actual shared acquired sample, contact and intended joining remain distinct.")
        if key:
            joint_rows.append((joint, key))

    shape_rows = []
    # Three/four straight spans in one traversal can support an incomplete or
    # near-closed polygon. Do not equate the pen traversal with a semantic object.
    for stroke in observation.get("strokes", []):
        selected = [spans[s] for s in stroke["spans"]]
        if len(selected) not in (3, 4) or any(s["lineError"]["max"] > 8 for s in selected):
            continue
        points = [selected[0]["controls"][0]] + [s["controls"][-1] for s in selected]
        box = _box(points); extent = max(box[2]-box[0], box[3]-box[1])
        if extent <= 8:
            continue
        gap = math.dist(points[0], points[-1])
        directions = [[s["controls"][-1][j]-s["controls"][0][j] for j in (0, 1)] for s in selected]
        turns = []
        for a, b in zip(directions, directions[1:]):
            norm = math.hypot(*a)*math.hypot(*b)
            if norm:
                angle = math.degrees(math.acos(max(-1, min(1, sum(x*y for x, y in zip(a, b))/norm))))
                turns.append(abs(angle-90))
        rectangular = len(turns) >= 2 and max(turns) <= 32
        if rectangular:
            geometry, kind = rectangle(box), "rectangle-candidate"
            checks = [Comparison(t, 0, 20, "page angular degrees") for t in turns]
        elif len(selected) == 3 and gap <= .25*extent:
            geometry, kind = {"points": points[:3], "segments": [[0, 1], [1, 2], [2, 0]]}, "triangle-candidate"
            checks = [Comparison(gap, 0, .25*extent, "normalized page distance")]
        else:
            continue
        key = emit("shape-stroke-"+str(stroke["index"]), kind, stroke["spans"], geometry=geometry, comparisons=checks,
                   meaning="Expected polygon over source-supported runs; missing boundaries remain proposed.")
        if key:
            shape_rows.append((key, kind, geometry, list(stroke["spans"])))

    # Reuse all existing church interpretation machinery; these groups name
    # actual selected source profiles even when regularization worsens ink fit.
    seen_shapes = {(kind, tuple(sorted(support))) for _, kind, _, support in shape_rows}
    if proposal:
        for step in proposal["steps"]:
            for group in step.get("groups", []):
                support = [s for s in group.get("spans", []) if s in spans]
                kind = group.get("kind", "shape") + "-candidate"
                signature = kind, tuple(sorted(support))
                if not support or signature in seen_shapes:
                    continue
                seen_shapes.add(signature)
                points = group.get("vertices")
                box = group.get("bounds")
                if points and all(isinstance(p, (list, tuple)) and len(p) == 2 for p in points):
                    geometry = {"points": points, "segments": [[i, (i+1) % len(points)] for i in range(len(points))]}
                elif box:
                    geometry = rectangle(box)
                else:
                    continue
                key = emit(f"shape-pass-{len(shape_rows):04}", kind, support, geometry=geometry,
                           meaning="Shape proposed by an authored interpretation pass; source fit can deliberately worsen.")
                if key:
                    shape_rows.append((key, kind, geometry, support))

    # A higher-level shape exposes local corner expectations. The links are
    # hypotheses dependent on the shape and, where offered, the encounter claim.
    for shape, kind, geometry, support in shape_rows:
        if kind not in ("rectangle-candidate", "triangle-candidate"):
            continue
        points = geometry["points"]
        if len(points) not in (3, 4):
            continue
        for i, corner in enumerate(points):
            nearby = [(math.dist(j["point"], corner), j, key) for j, key in joint_rows if set(j["spans"]) & set(support)]
            distance, joint, encounter = min(nearby, key=lambda item: item[0]) if nearby else (None, None, None)
            deps = [shape] + ([encounter] if encounter else [])
            emit(f"expected-corner-{shape}-{i}", "expected-L-corner" if len(points) == 4 else "expected-angled-corner", support,
                 dependencies=deps, geometry={"points": [corner, points[(i-1) % len(points)], points[(i+1) % len(points)]], "segments": [[0, 1], [0, 2]],
                                              "pointSources": [{"claim": shape, "index": index} for index in (i, (i-1) % len(points), (i+1) % len(points))]},
                 comparisons=[Comparison(distance, 0, 24, "normalized page distance")] if distance is not None else (),
                 meaning="Top-down corner expectation; can support joining despite weaker local fit, without deciding it or erasing gap evidence.")

    part_keys = {}; earlier_parts = set()
    for part in context.get("parts", []):
        participants = part.get("participants", [])
        if any(parent not in earlier_parts for parent in participants):
            raise ValueError("A context part references missing or forward participants")
        key = emit("part-scope-"+part["id"], "part-membership-candidate", part.get("spans", []),
                   dependencies=[part_keys[parent] for parent in participants if parent in part_keys],
                   context_parts=[part["id"]],
                   geometry=part.get("geometry"), meaning="These source profiles may participate in one part; identity and common response are hypotheses.")
        if key:
            part_keys[part["id"]] = key
        earlier_parts.add(part["id"])
    # Roles are separate opinions over the actual candidate and any competing
    # candidates. These reads never feed their own scores back as new evidence.
    for part in context.get("parts", []):
        if "role" not in part or part["id"] not in part_keys:
            continue
        alternatives = part.get("conflicts", [])
        if any(other not in parts_by_id for other in alternatives):
            raise ValueError("An object role references a missing competing candidate")
        selected = [part["id"]] + alternatives
        emit("part-role-"+part["id"], "object-role-candidate", part.get("spans", []),
             dependencies=[part_keys[p] for p in selected if p in part_keys], context_parts=selected,
             geometry=part.get("geometry"), comparisons=_comparisons(part),
             meaning="Local shape and placement reads may support this role. Distinct alternatives and unknown membership remain visible; no category label proves the interpretation.")
    for relation in context.get("relations", []):
        participants = relation.get("participants", [])
        if not all(p in part_keys for p in participants):
            continue
        deps = [part_keys[p] for p in participants]
        key = emit("context-"+relation["id"], relation["kind"]+"-candidate", relation.get("spans", []), dependencies=deps,
                   context_parts=participants, comparisons=_comparisons(relation),
                   meaning="Conditional relation over previously introduced candidate parts.")
        if key and relation["kind"] == "containment":
            emit("carry-"+relation["id"], "container-carries-contained", relation.get("spans", []), dependencies=[key], mode="defaults",
                 context_parts=participants,
                 meaning="Child placement changes only the child; parent-frame displacement carries contained descendants once. Revisable under anchors or alternative groupings.")
    openings = [p for p in context.get("parts", []) if p["kind"] in ("window", "door") and p["id"] in part_keys]
    # Prefer the current object read when a legacy candidate selects exactly
    # the same ink. Rewording a candidate is not another separation witness.
    openings.sort(key=lambda part: (not part["id"].startswith("object-"), part["id"]))
    separation_scopes = set()
    for first, second in itertools.combinations(openings, 2):
        if (first.get("instanceId") is not None and first.get("instanceId") == second.get("instanceId") or
                set(first["spans"]) & set(second["spans"])):
            continue
        signature = tuple(sorted((tuple(sorted(first["spans"])), tuple(sorted(second["spans"])))))
        if signature in separation_scopes:
            continue
        a, b = first["bounds"], second["bounds"]
        if a[2] <= b[0] or b[2] <= a[0] or a[3] <= b[1] or b[3] <= a[1]:
            separation_scopes.add(signature)
            emit("separate-"+first["id"]+"-"+second["id"], "separate-part-instances", first["spans"]+second["spans"],
                 dependencies=[part_keys[first["id"]], part_keys[second["id"]]],
                 context_parts=[first["id"], second["id"]],
                 meaning="Distinct candidate instances may share a parent/template while retaining separate identities and child edits.")
    # Roof/wall attachment is supplied by the explicit context relations above.
    # A roof label alone does not attach it to the first building in the file.
    return {"schema": "gojo.quickdraw-opinions.v1", "sourceId": observation["sourceId"], "claims": rows,
            "ladder": list(GRADES), "learned": False, "maximumClaims": maximum,
            "capacityReached": bool(skipped), "withheldClaimIds": skipped,
            "qualification": "Replaceable provider opinions with source support, actual context-part selections, expected geometry and earlier dependencies. No learned arbiter, probability calibration or automatic joint settlement."}
