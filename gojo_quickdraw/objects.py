"""Bounded church part proposals over source profiles, without changing geometry.

The dictionaries are an adapter for ContextEvidence: every new part selects
actual profile witnesses and, where applicable, earlier candidate witnesses.
Role words and colours are review aids, not proof of ownership or physical law.
"""
from __future__ import annotations

from collections import defaultdict
from copy import deepcopy
import hashlib
import itertools
import math


POLICY = {"version": "church-object-proposals.v1", "learned": False,
          "maximumSpans": 256, "maximumGroups": 384, "maximumWalls": 12,
          "maximumCandidates": 96, "maximumRelations": 96,
          "qualification": "Finite authored grouping at the fitted-source resolution; support is not calibrated probability."}


def _bounds(points):
    return [min(p[0] for p in points), min(p[1] for p in points),
            max(p[0] for p in points), max(p[1] for p in points)]


def _size(box):
    return box[2]-box[0], box[3]-box[1]


def _center(box):
    return [(box[0]+box[2])/2, (box[1]+box[3])/2]


def _outside(inner, outer):
    return [max(0, outer[0]-inner[0]), max(0, outer[1]-inner[1]),
            max(0, inner[2]-outer[2]), max(0, inner[3]-outer[3])]


def _key(role, support):
    digest = hashlib.sha256("\0".join(sorted(set(support))).encode()).hexdigest()[:16]
    return "object-"+role+"-"+digest


def _geometry(box):
    l, t, r, b = box
    return {"points": [[l, t], [r, t], [r, b], [l, b]],
            "segments": [[0, 1], [1, 2], [2, 3], [3, 0]]}


def _comparison(actual, expected, allowance, basis):
    return {"actual": float(actual), "expected": float(expected),
            "allowance": float(allowance), "basis": basis}


def _segment_distance(point, first, last):
    vector = [last[i]-first[i] for i in (0, 1)]
    length2 = sum(v*v for v in vector)
    ratio = max(0, min(1, sum((point[i]-first[i])*vector[i] for i in (0, 1))/length2)) if length2 else 0
    return math.dist(point, [first[i]+ratio*vector[i] for i in (0, 1)])


def _chains(spans, tolerance):
    """Endpoint proximity proposes a graph; it never aliases acquired sites.

    Complete-link endpoint clusters prevent a long proximity chain joining
    distant corners. Branched components are retained by callers as unknown.
    """
    nodes, ends = [], {}
    for span in sorted(spans, key=lambda s: s["id"]):
        ends[span["id"]] = []
        for point in (span["controls"][0], span["controls"][-1]):
            choices = [i for i, members in enumerate(nodes)
                       if all(math.dist(point, p) <= tolerance for p in members)]
            node = min(choices, key=lambda i: math.dist(point, nodes[i][0])) if choices else len(nodes)
            if node == len(nodes):
                nodes.append([])
            nodes[node].append(point)
            ends[span["id"]].append(node)
    incident = defaultdict(set)
    for sid, pair in ends.items():
        for node in pair:
            incident[node].add(sid)
    remaining = set(ends)
    result = []
    while remaining:
        pending = [min(remaining)]; component = set()
        while pending:
            sid = pending.pop()
            if sid in component:
                continue
            component.add(sid); remaining.discard(sid)
            pending.extend(other for node in ends[sid] for other in incident[node] if other not in component)
        degrees = defaultdict(int)
        for sid in component:
            for node in ends[sid]:
                degrees[node] += 1
        terminals = [nodes[node][0] for node, degree in degrees.items() if degree == 1]
        result.append((sorted(component), terminals, max(degrees.values(), default=0)))
    return result


def _features(support, spans, tolerance):
    selected = [spans[s] for s in support]
    box = _bounds([p for s in selected for p in s["controls"]])
    components = _chains(selected, tolerance)
    if len(components) != 1:
        return None
    _, terminals, degree = components[0]
    if degree > 2 or len(terminals) not in (0, 2):
        return None
    w, h = _size(box)
    if min(w, h) <= tolerance:
        return None
    curved = any(len(s["controls"]) > 2 for s in selected)
    straight = all(len(s["controls"]) == 2 or s.get("lineError", {}).get("max", math.inf) <= 7 for s in selected)
    axis_runs = sum(abs(s["controls"][-1][0]-s["controls"][0][0]) <= .35*abs(s["controls"][-1][1]-s["controls"][0][1])
                    or abs(s["controls"][-1][1]-s["controls"][0][1]) <= .35*abs(s["controls"][-1][0]-s["controls"][0][0]) for s in selected)
    closed = not terminals
    bottom_open = len(terminals) == 2 and min(p[1] for p in terminals) >= box[1]+.65*h
    corners = _geometry(box)["points"]
    boundary_points = [point for span in selected for point in [
        *span["controls"],
        *[[(a[i]+b[i])/2 for i in (0, 1)] for a, b in zip(span["controls"], span["controls"][1:])]]]
    boundary_following = all(min(_segment_distance(point, corners[i], corners[(i+1) % 4])
                                 for i in range(4)) <= tolerance for point in boundary_points)
    rectangular = (straight and len(selected) >= (4 if closed else 3)
                   and axis_runs >= len(selected)-1 and boundary_following)
    if not closed and not bottom_open:
        return None
    return {"bounds": box, "closed": closed, "bottomOpen": bottom_open,
            "rectangular": rectangular, "curved": curved,
            "shape": "rectangle" if rectangular else "curved-contour" if curved and closed else "arch" if curved else "closed-contour" if closed else "open-contour",
            "terminalGap": math.dist(*terminals) if terminals else 0.0}


def enrich_context(observed, context, annotations):
    """Return independent context/summary snapshots; no acquisition is rewritten.

    Membership may overlap. Native participant links are backward only;
    parentIds describe the review hierarchy and can refer to a later assembly.
    """
    result = deepcopy(context)
    result.setdefault("parts", []); result.setdefault("relations", [])
    # Refresh this provider's derived surface instead of appending duplicate
    # witnesses when the caller supplies an already enriched context.
    result["parts"] = [p for p in result["parts"] if not p["id"].startswith("object-")]
    result["relations"] = [r for r in result["relations"] if not r["id"].startswith("object-")]
    source = observed.get("sourceId")
    if context.get("sourceId") != source or annotations and annotations.get("sourceId") != source:
        raise ValueError("Object context and source identities differ")
    all_spans = {s["id"]: s for s in observed.get("spans", [])}
    if len(all_spans) != len(observed.get("spans", [])):
        raise ValueError("Object proposals need unique source span identities")
    # Preserve acquisition order when selecting a finite work budget.
    spans = dict(list(all_spans.items())[:POLICY["maximumSpans"]])
    capacity = {"spanBudget": len(all_spans) > len(spans), "groupBudget": False, "wallBudget": False,
                "candidateBudget": False, "relationBudget": False}
    candidates, lookup, withheld = [], {}, []
    building_scopes = {}
    legacy = list(result["parts"])
    known = {p["id"] for p in legacy}

    def add(role, support, box=None, *, participants=(), parents=(), reasons=(), comparisons=(), focus="direct", origin="endpoint-chain"):
        support = sorted(set(support))
        if not support or any(s not in all_spans for s in support):
            return None
        ident = _key(role, support)
        if ident in lookup:
            old = lookup[ident]
            old["parentIds"] = sorted(set(old["parentIds"]) | set(parents))
            old["reasons"] = list(dict.fromkeys(old["reasons"]+list(reasons)))
            # Actual participant relations were captured when first introduced.
            return old
        if len(candidates) >= POLICY["maximumCandidates"]:
            capacity["candidateBudget"] = True
            withheld.append(ident); return None
        participants = list(dict.fromkeys(participants))
        if any(p not in known for p in participants):
            raise ValueError("Object proposal references a missing or forward participant")
        box = list(box) if box else _bounds([p for s in support for p in all_spans[s]["controls"]])
        item = {"id": ident, "instanceId": _key("instance", support), "kind": role, "role": role,
                "focusKind": focus, "spans": support, "bounds": box, "participants": participants,
                "parentIds": list(dict.fromkeys(parents)), "geometry": _geometry(box),
                "status": "unresolved" if role == "unknown" else "supported-candidate",
                "reasons": list(reasons), "conflicts": [], "comparisons": list(comparisons), "source": origin,
                "expected": {"role": role, "frameMeaning": "provisional bounding scaffold; not asserted contour or acquired ink"},
                "residue": ["Membership and role remain authored hypotheses; shared source is not independent evidence."],
                "qualification": POLICY["qualification"]}
        candidates.append(item); lookup[ident] = item; known.add(ident); result["parts"].append(item)
        return item

    relation_count = 0
    def relate(kind, participants, comparisons=(), reasons=()):
        nonlocal relation_count
        if not all(participants):
            return
        ids = [p["id"] for p in participants]
        ident = _key(kind, ids)
        if any(r["id"] == ident for r in result["relations"]):
            return
        if relation_count >= POLICY["maximumRelations"]:
            capacity["relationBudget"] = True
            return
        result["relations"].append({"id": ident, "kind": kind, "status": "seeded-candidate", "participants": ids,
            "spans": sorted({s for p in participants for s in p["spans"]}), "comparisons": list(comparisons),
            "expected": {"order": ids}, "measured": {}, "reasons": list(reasons),
            "residue": ["Conditional relation between candidate instances; no source sites are merged."]})
        relation_count += 1

    if str(context.get("category", "church")).casefold() == "church" and spans:
        scene = observed.get("bounds") or _bounds([p for s in spans.values() for p in s["controls"]])
        sw, sh = _size(scene)
        tolerance = max(2.0, min(14.0, .028*max(sw, sh)))
        groups = {}
        def group(support):
            support = tuple(sorted(set(s for s in support if s in spans)))
            if support and support not in groups:
                if len(groups) >= POLICY["maximumGroups"]:
                    capacity["groupBudget"] = True
                    return
                features = _features(support, spans, tolerance)
                if features:
                    groups[support] = features
        # A recorded stroke is one proposal source, never the only boundary.
        for stroke in observed.get("strokes", []):
            ids = [s for s in stroke.get("spans", []) if s in spans]
            group(ids)
            # Closed sub-contours can occur inside a longer acquisition stroke.
            for count in range(2, min(8, len(ids))+1):
                for i in range(len(ids)-count+1):
                    subset = ids[i:i+count]
                    if math.dist(spans[subset[0]]["controls"][0], spans[subset[-1]]["controls"][-1]) <= tolerance:
                        group(subset)
        for support, _, _ in _chains(list(spans.values()), tolerance):
            group(support)

        walls = []
        for part in legacy:
            if part.get("kind") == "body":
                ident = _key("wall", part["spans"])
                if len(walls) >= POLICY["maximumWalls"] and ident not in lookup:
                    capacity["wallBudget"] = True; withheld.append(ident)
                    continue
                item = add("wall", part["spans"], part["bounds"], participants=[part["id"]],
                    reasons=["Supported opposed uprights and nearby boundary runs", "Competing body scopes remain alternatives"], origin="legacy-context")
                if item and item not in walls:
                    walls.append(item)
        # Upper local rectangular scopes retain tower/body identity instead of
        # pooling every rectangle into one global facade.
        for support, feature in sorted(groups.items(), key=lambda x: -math.prod(_size(x[1]["bounds"]))):
            box = feature["bounds"]; w, h = _size(box)
            if not feature["rectangular"]:
                continue
            upper = box[1] < scene[1]+.45*sh
            large = w >= .45*sw and h >= .28*sh
            tower = upper and .08*sw <= w <= .65*sw and h >= .14*sh
            if not (large or tower):
                continue
            ident = _key("wall", support)
            if len(walls) >= POLICY["maximumWalls"] and ident not in lookup:
                capacity["wallBudget"] = True; withheld.append(ident)
                continue
            item = add("wall", support, box, reasons=["Local rectangular boundary chain", "Missing lower boundary remains open" if not feature["closed"] else "Near-closed source-supported contour"])
            if item and item not in walls:
                walls.append(item)

        # Each assembly focus retains its own local wall scope. Candidate count
        # is not actual building count: alternative wall scopes remain open.
        for wall in walls:
            building = add("building", wall["spans"], wall["bounds"],
                participants=[wall["id"]], reasons=["Building focus over this local wall scope", "Alternative wall scopes are not counted as separate proven buildings"], focus="assembly")
            if building:
                building_scopes[wall["id"]] = building
                wall["parentIds"].append(building["id"])
                relate("assembly-membership", [building, wall], reasons=["Shared evidence at assembly and wall focuses is intentional"])

        roofs, peaks = [], []
        for part in legacy:
            role = part.get("kind")
            if role not in ("roof", "steeple"):
                continue
            box = part["bounds"]; cx, _ = _center(box)
            parents = [w for w in walls if box[1] < w["bounds"][1]+.25*_size(w["bounds"])[1]
                       and w["bounds"][0]-.15*_size(w["bounds"])[0] <= cx <= w["bounds"][2]+.15*_size(w["bounds"])[0]]
            parents.sort(key=lambda w: abs(box[3]-w["bounds"][1])/max(1, _size(w["bounds"])[1])+abs(cx-_center(w["bounds"])[0])/max(1, _size(w["bounds"])[0]))
            parents = parents[:2]
            item = add(role, part["spans"], box, participants=[part["id"], *[p["id"] for p in parents]], parents=[p["id"] for p in parents],
                reasons=["Upper converging profile", "Each attachment is tested in a candidate wall frame"], origin="legacy-context")
            if item:
                (roofs if role == "roof" else peaks).append(item)
                for parent in parents:
                    relate("roof-wall-attachment" if role == "roof" else "upper-part-attachment", [parent, item],
                           [_comparison(box[3]-parent["bounds"][1], 0, .25*_size(parent["bounds"])[1],
                                        "roof base relative to candidate wall top")])

        # A whole upper contour may support a roof even when individual curved
        # fitted intervals did not produce the earlier two-line roof read.
        for support, feature in groups.items():
            if len(support) < 2 or feature["rectangular"]:
                continue
            box = feature["bounds"]; rw, rh = _size(box)
            if rw < .15*sw or rh < .10*sh:
                continue
            points = [p for sid in support for p in spans[sid]["controls"]]
            apex = min((p for sid in support for p in (spans[sid]["controls"][0], spans[sid]["controls"][-1])), key=lambda p: p[1])
            vertices = [[box[0], box[3]], apex, [box[2], box[3]]]
            if not box[0]+.12*rw <= apex[0] <= box[2]-.12*rw:
                continue
            error = max(min(_segment_distance(p, vertices[i], vertices[(i+1) % 3]) for i in range(3)) for p in points)
            if error > .11*max(rw, rh):
                continue
            parents = [w for w in walls if box[1] < w["bounds"][1]-.05*_size(w["bounds"])[1]
                       and abs(box[3]-w["bounds"][1]) <= .35*_size(w["bounds"])[1]
                       and w["bounds"][0]-.15*_size(w["bounds"])[0] <= apex[0] <= w["bounds"][2]+.15*_size(w["bounds"])[0]]
            if not parents:
                continue
            parents.sort(key=lambda w: abs(box[3]-w["bounds"][1])+abs(apex[0]-_center(w["bounds"])[0]))
            parents = parents[:2]
            checks = [_comparison(error, 0, .11*max(rw, rh), "source control envelope distance to proposed triangular profile")]
            item = add("roof", support, box, participants=[p["id"] for p in parents], parents=[p["id"] for p in parents],
                reasons=["Upper contour supports converging sides above a candidate wall", "Triangle is a hypothesis; no source profiles are straightened"], comparisons=checks)
            if item:
                if item not in roofs:
                    roofs.append(item)
                for parent in parents:
                    relate("roof-wall-attachment", [parent, item], checks + [
                        _comparison(box[3]-parent["bounds"][1], 0, .35*_size(parent["bounds"])[1],
                                    "roof base relative to candidate wall top")])

        for part in legacy:
            if part.get("kind") != "cross":
                continue
            centre = _center(part["bounds"])
            possible = roofs+peaks+walls
            parents = sorted(possible, key=lambda p: math.dist(centre, [_center(p["bounds"])[0], p["bounds"][1]]))[:1]
            item = add("cross", part["spans"], part["bounds"], participants=[part["id"], *[p["id"] for p in parents]],
                parents=[p["id"] for p in parents], reasons=["Two crossing source profiles", "Cross versus incidental crossing remains a role hypothesis"], origin="legacy-context")
            if item and parents:
                relate("upper-marker-attachment", [parents[0], item])

        openings = []
        for wall in walls:
            box = wall["bounds"]; w, h = _size(box)
            local_tolerance = max(2.0, min(tolerance, .045*min(w, h)))
            eligible = [s for s in spans.values() if s["id"] not in wall["spans"]
                        and sum(_outside(_bounds(s["controls"]), box)) <= .10*(w+h)]
            local_groups = dict(groups)
            for support, _, degree in _chains(eligible, local_tolerance):
                if degree <= 2 and tuple(support) not in local_groups:
                    if len(local_groups) >= POLICY["maximumGroups"]:
                        capacity["groupBudget"] = True
                        continue
                    features = _features(support, spans, local_tolerance)
                    if features:
                        local_groups[tuple(support)] = features
            for support, feature in local_groups.items():
                if set(support) & set(wall["spans"]):
                    continue
                inner = feature["bounds"]; iw, ih = _size(inner); centre = _center(inner)
                if not (.025*w <= iw <= .58*w and .04*h <= ih <= .92*h):
                    continue
                outside = sum(_outside(inner, box))
                if outside > .08*(w+h) or not (box[0] <= centre[0] <= box[2] and box[1] <= centre[1] <= box[3]):
                    continue
                if not feature["closed"] and not (feature["bottomOpen"] and (feature["rectangular"] or feature["curved"])):
                    continue
                low = inner[3] >= box[1]+.82*h
                central = abs(centre[0]-_center(box)[0]) <= .23*w
                role = "door" if low and central else "window"
                checks = [_comparison(outside, 0, .08*(w+h), "opening outside candidate wall envelope")]
                reason = ["Near-closed contour" if feature["closed"] else "Open-bottom contour with supported sides and cap",
                          "Lower central placement" if role == "door" else "Local interior opening",
                          "Membership can cross acquisition-stroke boundaries", "Shape read: "+feature["shape"]]
                item = add(role, support, inner, participants=[wall["id"]], parents=[wall["id"]], reasons=reason, comparisons=checks)
                if item:
                    item["shapeRead"] = feature["shape"]
                    if item not in openings:
                        openings.append(item)
                    relate("containment", [wall, item], checks, ["Envelope relation; occupied wall fill remains unproven"])
        for wall in walls:
            local = [o for o in openings if wall["id"] in o["parentIds"]]
            for a, b in itertools.combinations(local, 2):
                if set(a["spans"]) & set(b["spans"]):
                    continue
                aw, ah = _size(a["bounds"]); bw, bh = _size(b["bounds"])
                if min(aw, bw)/max(aw, bw) >= .5 and min(ah, bh)/max(ah, bh) >= .5:
                    relate("repeated-part-candidates", [wall, a, b],
                           [_comparison(_center(a["bounds"])[1]-_center(b["bounds"])[1], 0, .18*_size(wall["bounds"])[1], "repeat baseline difference in parent wall frame")],
                           ["Similar parts retain separate instances; repeat controller remains open"])

    # Explicit ambiguity is independent of coverage. Assembly overlap and
    # wall/roof boundary sharing are not automatically conflicting ownership.
    direct = [c for c in candidates if c["focusKind"] == "direct"]
    for a, b in itertools.combinations(direct, 2):
        shared = set(a["spans"]) & set(b["spans"])
        if not shared:
            continue
        ratio = len(shared)/min(len(a["spans"]), len(b["spans"]))
        same = set(a["spans"]) == set(b["spans"])
        competing = (a["role"] == b["role"] and ratio >= .5 and not same) or (
            a["role"] != b["role"] and (same or ratio >= .75) and {a["role"], b["role"]} != {"roof", "steeple"})
        # Roof/steeple are two readings of one upper part, explicitly open.
        competing = competing or (same and {a["role"], b["role"]} == {"roof", "steeple"})
        if competing:
            a["conflicts"].append(b["id"]); b["conflicts"].append(a["id"])
            a["status"] = b["status"] = "ambiguous-candidate"
    for wall_id, building in building_scopes.items():
        wall = lookup[wall_id]
        if wall["conflicts"]:
            building["status"] = "ambiguous-candidate"
            building["conflicts"] = [building_scopes[c]["id"] if c in building_scopes else c for c in wall["conflicts"]]
            building["reasons"].append("Assembly focus inherits unresolved wall-scope alternatives")
    ambiguous = {s for c in candidates if c["conflicts"] for s in c["spans"]}
    covered = {s for c in direct for s in c["spans"]}
    unknown = [s for s in all_spans if s not in covered]
    for support, _, _ in _chains([spans[s] for s in unknown if s in spans], 6):
        add("unknown", support, reasons=["No retained part interpretation supports this local profile group"])
    # Sources beyond the bounded read remain explicitly unknown, never dropped.
    for item in candidates:
        item["parentIds"] = sorted(set(item["parentIds"]))
        item["conflicts"] = sorted(set(item["conflicts"]))
    summary = {"schema": "gojo.quickdraw-objects.v1", "sourceId": source, "category": context.get("category", "church"),
        "candidates": deepcopy(candidates), "unknownSpanIds": unknown,
        "ambiguousSpanIds": [s for s in all_spans if s in ambiguous], "policy": deepcopy(POLICY),
        "capacityReached": any(capacity.values()), "capacity": capacity, "withheldCandidateIds": sorted(set(withheld)),
        "unreadSpanIds": [s for s in all_spans if s not in spans],
        "passes": ["source-supported grouping", "local role and parent proposals", "cross-candidate disagreement read"],
        "learned": False, "geometryRewritten": False,
        "qualification": "Authored object candidates at fitted-source resolution. Roles, parentage and common response remain revisable; no automatic synthesis feedback or physical containment is asserted."}
    return result, summary
