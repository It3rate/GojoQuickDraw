"""Coarse church placement proposals. Ideal scaffolds never replace source ink.

Regional extent/direction reads and authored relative-placement costs propose a
small layout. These are finite heuristic costs, not learned likelihoods or a
native global solver. Explicit context witnesses retain source and dependencies.
"""
from __future__ import annotations

from copy import deepcopy
import itertools
import math

COLORS = {"ground": "#63735f", "building": "#277da8", "roof": "#bf712a",
          "cross": "#ae3f6d", "cross-level": "#ae3f6d", "door": "#23846c", "window": "#8c68aa"}
POLICY = {"version": "church-area-layout.v1", "learned": False, "maximumSamples": 2048,
          "maximumUprights": 16, "maximumDoors": 2, "maximumWindows": 8,
          "basis": "normalized observer page; down is a chosen church-upright premise",
          "qualification": "Authored regional plausibility costs, not calibrated probabilities; no source geometry is replaced."}


def _box(points):
    return [min(p[0] for p in points), min(p[1] for p in points), max(p[0] for p in points), max(p[1] for p in points)]


def _clamp(x, lo, hi):
    return min(hi, max(lo, x))


def _quantile(values, q):
    """Weighted finite read: one contribution per sampled source interval."""
    values = sorted((v, w) for v, w in values if w > 0)
    if not values:
        raise ValueError("A quantile needs source participation")
    target = q * sum(w for _, w in values)
    for value, weight in values:
        target -= weight
        if target <= 0:
            return value
    return values[-1][0]


def _samples(observed):
    samples = []
    for stroke in observed.get("strokes", []):
        points = stroke["points"]
        for a, b in zip(points, points[1:]):
            distance = math.dist(a, b)
            if distance:
                count = max(1, math.ceil(distance/8))
                for i in range(count):
                    t=(i+.5)/count
                    samples.append([a[0]+(b[0]-a[0])*t, a[1]+(b[1]-a[1])*t, distance/count, stroke["index"]])
    if len(samples) <= POLICY["maximumSamples"]:
        return samples, False
    # Aggregate adjacent acquisition intervals, preserving mass and explicit loss.
    size = math.ceil(len(samples)/POLICY["maximumSamples"])
    reduced = []
    for i in range(0, len(samples), size):
        block = samples[i:i+size]; weight = sum(s[2] for s in block)
        reduced.append([sum(s[j]*s[2] for s in block)/weight for j in (0, 1)] + [weight, None])
    return reduced, True


def _ring(points, site_sources=None):
    result = {"points": points, "segments": [[i, (i+1) % len(points)] for i in range(len(points))],
              "fillIndices": list(range(len(points)))}
    if site_sources is not None:
        result["siteSources"] = site_sources
    return result


def _comparison(actual, expected, allowance, basis):
    return dict(actual=actual, expected=expected, allowance=allowance, basis=basis)


def _region_support(spans, bounds):
    l, t, r, b = bounds
    return [s["id"] for s in spans if any(l <= p[0] <= r and t <= p[1] <= b for p in s["controls"])]


def _opening_regions(observed, bounds, excluded):
    """Small source regions suggest optional openings without fitting a contour."""
    l, t, r, g = bounds; w, h = r-l, g-t
    eligible = []
    by_id = {s["id"]: s for s in observed.get("spans", [])}
    for stroke in observed.get("strokes", []):
        if set(stroke.get("spans", [])) & excluded or len(stroke["points"]) < 2:
            continue
        box = _box(stroke["points"]); a, b, c, d = box
        if (c-a > .56*w or d-b > .88*h or a < l-.04*w or c > r+.04*w or b < t+.04*h or d > g+.12*h):
            continue
        eligible.append((stroke, box))
    groups = [[i] for i in range(len(eligible))]
    remaining = set(range(len(eligible)))
    tolerance = .045*min(w, h)
    while remaining:
        pending = [min(remaining)]; group = set()
        while pending:
            i = pending.pop()
            if i in group:
                continue
            group.add(i); remaining.discard(i)
            a = eligible[i][0]["points"]
            for j in sorted(remaining):
                b = eligible[j][0]["points"]
                if min(math.dist(x, y) for x in (a[0], a[-1]) for y in (b[0], b[-1])) < tolerance:
                    pending.append(j)
        if len(group) > 1:
            groups.append(sorted(group))
    result = []
    for group in groups:
        strokes = [eligible[i][0] for i in group]
        box = _box([p for s in strokes for p in s["points"]]); iw, ih = box[2]-box[0], box[3]-box[1]
        travel = sum(math.dist(a,b) for s in strokes for a,b in zip(s["points"], s["points"][1:]))
        if min(iw, ih) < .035*min(w, h) or iw > .56*w or ih > .88*h or travel < 1.25*(iw+ih):
            continue
        support = sorted({sid for s in strokes for sid in s.get("spans", []) if sid in by_id})
        if support and not any(set(support) == set(p["spans"]) for p in result):
            result.append({"bounds": box, "spans": support, "travel": travel})
    return result


def estimate(observed):
    """Place a small ideal construction from broad source regions and priors."""
    spans = observed.get("spans", [])
    samples, reduced = _samples(observed)
    if not samples or not spans:
        return {"schema": "gojo.quickdraw-area-layout.v1", "sourceId": observed.get("sourceId"),
                "parts": [], "relations": [], "alternatives": [], "status": "open-no-extended-ink", "policy": deepcopy(POLICY)}
    scene = _box([p for s in observed["strokes"] for p in s["points"]]); sl, st, sr, sb = scene
    sw, sh = sr-sl, sb-st
    if min(sw, sh) < 8:
        return {"schema": "gojo.quickdraw-area-layout.v1", "sourceId": observed.get("sourceId"),
                "parts": [], "relations": [], "alternatives": [], "status": "open-insufficient-area", "policy": deepcopy(POLICY)}
    cross_hints = [c for c in observed.get("crosses", []) if c["point"][1] < st+.68*sh]
    cross_spans = {sid for c in cross_hints for sid in c["spans"]}
    cross_strokes = {s["strokeIndex"] for s in spans if s["id"] in cross_spans}
    uprights = []
    for s in spans:
        a, b = s["controls"][0], s["controls"][-1]
        dx, dy = abs(b[0]-a[0]), abs(b[1]-a[1])
        if s["id"] not in cross_spans and dy > .17*sh and dx < .5*dy and max(a[1], b[1]) > st+.72*sh:
            uprights.append({"span": s["id"], "x": (a[0]+b[0])/2, "top": min(a[1],b[1]), "bottom": max(a[1],b[1]), "weight": dy})
    uprights.sort(key=lambda s: -s["weight"]); uprights = uprights[:POLICY["maximumUprights"]]
    lower = [p for p in samples if p[1] >= st+.60*sh] or samples
    ground_values = [(s["bottom"], s["weight"]) for s in uprights]
    # The ground is a level estimate, whether or not a baseline was drawn.
    ground_values.append((_quantile([(p[1],p[2]) for p in lower], .98), sum(s["weight"] for s in uprights)*.6 or sh))
    ground = round(_quantile(ground_values, .5), 3)
    lx = _quantile([(p[0],p[2]) for p in lower], .04); rx = _quantile([(p[0],p[2]) for p in lower], .96)
    if rx-lx < .25*sw:
        lx, rx = sl+.1*sw, sr-.1*sw
    horizontal = []
    for s in spans:
        a,b = s["controls"][0],s["controls"][-1]
        if abs(b[0]-a[0]) > .35*sw and abs(b[1]-a[1]) < .25*abs(b[0]-a[0]) and st+.25*sh < (a[1]+b[1])/2 < ground-.18*sh:
            horizontal.append((a[1]+b[1])/2)
    widths = [(lx,rx), (lx-.03*sw,rx+.03*sw)]
    for a,b in itertools.combinations(uprights, 2):
        l,r = sorted((a["x"],b["x"]))
        if r-l > .32*sw:
            widths.append((l,r))
    tops = [st+fraction*sh for fraction in (.35,.45,.55,.65)] + horizontal
    tops += [s["top"] for s in uprights if st+.24*sh <= s["top"] <= ground-.18*sh]
    alternatives = []
    for l,r in widths:
        if r-l < .25*sw:
            continue
        for top in sorted(set(round(v, 3) for v in tops)):
            if not st+.20*sh < top < ground-.18*sh:
                continue
            terms = {"lowerRegionExtent": .8*(abs(l-lx)+abs(r-rx))/(.18*sw),
                     "bodyHeightPrior": .3*abs(top-(ground-.48*sh))/(.30*sh)}
            for side, x in (("leftBoundaryRegion", l),("rightBoundaryRegion",r)):
                terms[side] = min((min(4, abs(x-s["x"])/(.10*sw)+abs(top-s["top"])/(.30*sh)) for s in uprights), default=1)
            terms["upperLevelEvidence"] = .55*min((abs(top-y)/(.15*sh) for y in horizontal), default=0)
            alternatives.append({"bounds": [round(l,3),top,round(r,3),ground], "cost": sum(terms.values()), "costTerms": terms})
    alternatives.sort(key=lambda c: (c["cost"], c["bounds"]))
    distinct = []
    for candidate in alternatives:
        if not any(sum(abs(a-b) for a,b in zip(candidate["bounds"], old["bounds"])) < .06*(sw+sh) for old in distinct):
            distinct.append(candidate)
        if len(distinct) == 3:
            break
    l, top, r, g = distinct[0]["bounds"]
    bw,bh = r-l,g-top; cx=(l+r)/2
    upper = [p for p in samples if p[1] < top-.04*sh and l-.15*bw <= p[0] <= r+.15*bw and p[3] not in cross_strokes]
    apex_y = _quantile([(p[1],p[2]) for p in upper], .025) if upper else top-.28*sh
    apex_y = round(_clamp(apex_y, st-.06*sh, top-.12*sh),3)
    peak_band = [p for p in upper if p[1] < apex_y+.10*sh]
    hint_x = _quantile([(p[0],p[2]) for p in peak_band],.5) if peak_band else cx
    apex_x = round(_clamp(.6*hint_x+.4*cx, cx-.12*bw, cx+.12*bw),3)
    if cross_hints:
        cross = min(cross_hints, key=lambda c: abs(c["point"][0]-apex_x)/bw + max(0,c["point"][1]-top)/sh)
        support = cross["spans"]
        cross_box = _box([p for s in spans if s["id"] in support for p in s["controls"]])
        ch = _clamp(cross_box[3]-cross_box[1], .08*sh, .27*sh)
        cross_x, bar_y = cross["point"]
        above = bar_y < apex_y
        if above:
            ch = min(ch, .8*max(.08*sh,apex_y-st)); cross_x = _clamp(cross_x,cx-.22*bw,cx+.22*bw)
            cross_top = min(bar_y-.32*ch, apex_y-ch)
        else:
            ch = min(ch,.48*(top-apex_y)); cross_top = _clamp(bar_y-.32*ch, apex_y+.18*(top-apex_y),top-ch-.04*sh)
            cross_x = _clamp(cross_x, apex_x-.12*bw, apex_x+.12*bw)
    else:
        support=[]; above=True; ch=.12*sh; cross_x=apex_x; cross_top=apex_y-ch
    if not above:
        # Every outline vertex must fit the chosen triangle, including the arms.
        # This adjusts a proposed ideal marker, never the acquired crossing.
        for _ in range(12):
            cross_top=_clamp(cross_top,apex_y+.22*(top-apex_y),top-ch-.015*sh)
            offsets=[(-.06*ch,0),(.06*ch,0),(-.30*ch,.26*ch),(.30*ch,.26*ch),
                     (-.30*ch,.38*ch),(.30*ch,.38*ch),(-.06*ch,ch),(.06*ch,ch)]
            lo,hi=l,r
            for dx,dy in offsets:
                fraction=(cross_top+dy-apex_y)/(top-apex_y)
                lo=max(lo,apex_x+(l-apex_x)*fraction-dx)
                hi=min(hi,apex_x+(r-apex_x)*fraction-dx)
            if lo+.002 < hi:
                cross_x=_clamp(cross_x,lo+.001,hi-.001); break
            ch*=.8
    # Christian cross: upper arm at 32% of its height, longer lower stem.
    cross_x, ch, cross_top = round(cross_x,3), round(ch,3), round(cross_top,3)
    bar_y=round(cross_top+.32*ch,3); thick=.12*ch; half=.30*ch
    opening_regions = _opening_regions(observed,[l,top,r,g],cross_spans)
    doors,windows=[],[]
    for region in sorted(opening_regions, key=lambda q: -(q["bounds"][2]-q["bounds"][0])*(q["bounds"][3]-q["bounds"][1])):
        a,b,c,d=region["bounds"]
        if d > g-.18*bh and top+.18*bh < b < g-.10*bh and c-a < .5*bw:
            if len(doors) < POLICY["maximumDoors"] and not any(set(region["spans"]) & set(o["spans"]) for o in doors):
                width=_clamp(c-a,.08*bw,.4*bw); centre=_clamp((a+c)/2,l+.04*bw+width/2,r-.04*bw-width/2)
                doors.append({**region,"placedBounds":[centre-width/2,_clamp(b,top+.22*bh,g-.15*bh),centre+width/2,g]})
    dw=min((q["placedBounds"][2]-q["placedBounds"][0] for q in doors),default=.24*bw)
    dh=min((g-q["placedBounds"][1] for q in doors),default=.40*bh)
    for region in opening_regions:
        a,b,c,d=region["bounds"]
        if (d >= g-.12*bh or b < top+.08*bh or len(windows) >= POLICY["maximumWindows"] or
            any(set(region["spans"]) & set(o["spans"]) for o in doors+windows)):
            continue
        width=min(c-a,.85*dw); height=min(d-b,.75*dh)
        x=_clamp((a+c)/2,l+.04*bw+width/2,r-.04*bw-width/2)
        y=_clamp((b+d)/2,top+.07*bh+height/2,g-.12*bh-height/2)
        windows.append({**region,"placedBounds":[x-width/2,y-height/2,x+width/2,y+height/2]})
    parts=[]; relations=[]
    def add(role, geo, bounds, ids=(), parents=(), assumed=False, extra=None):
        ident="layout-"+role+("-"+str(sum(p["role"]==role for p in parts)+1) if role in ("door","window") else "")
        item={"id":ident,"kind":"area-layout-"+role,"role":role,"spans":list(dict.fromkeys(ids)),
              "participants":list(parents),"geometry":geo,"bounds":bounds,"color":COLORS[role],
              "status":"prior-only" if assumed else "regional-estimate", "comparisons":[],
              "expected":{"meaning":"Proposed ideal area/level, not acquired boundary or an object assignment"},
              "residue":["Actual source geometry, gaps and contrary ink remain unchanged."],"qualification":POLICY["qualification"]}
        if extra:item.update(extra)
        parts.append(item); return item
    def relate(name, selected, actual, expected=0, allowance=0):
        relations.append({"id":"layout-relation-"+name,"kind":name,"participants":selected,"spans":[],
            "comparisons":[_comparison(actual,expected,allowance,"normalized page; chosen layout premise")],
            "status":"conditional-construction","residue":["Construction consistency is not independent observed evidence."]})
    ground_x=sorted(set([round(l-.10*bw,3),l,r,round(r+.10*bw,3)]+[round(v,3) for d in doors for v in (d["placedBounds"][0],d["placedBounds"][2])]))
    ground_points=[[x,g] for x in ground_x]
    source_ground=_region_support(spans,[sl,g-.10*sh,sr,g+.10*sh])
    add("ground",{"points":ground_points,"segments":[[i,i+1] for i in range(len(ground_points)-1)]},[ground_x[0],g,ground_x[-1],g],source_ground,assumed=not source_ground)
    ground_site=lambda x:{"part":"layout-ground","index":ground_x.index(round(x,3))}
    building=add("building",_ring([[l,top],[r,top],[r,g],[l,g]],[None,None,ground_site(r),ground_site(l)]),[l,top,r,g],
        _region_support(spans,[l-.08*bw,top-.08*bh,r+.08*bw,g+.08*bh]),["layout-ground"],extra={"costTerms":distinct[0]["costTerms"],"cost":distinct[0]["cost"]})
    building["comparisons"]=[_comparison(value,0,1,"authored regional cost: "+key) for key,value in distinct[0]["costTerms"].items()]
    add("roof",_ring([[l,top],[apex_x,apex_y],[r,top]],[{"part":building["id"],"index":0},None,{"part":building["id"],"index":1}]),
        [l,apex_y,r,top],_region_support(spans,[l-.10*bw,apex_y,r+.10*bw,top]),[building["id"]],assumed=not upper)
    x=cross_x; y=cross_top; b=bar_y; e=y+ch; k=thick/2
    cross_points=[[x-k,y],[x+k,y],[x+k,b-k],[x+half,b-k],[x+half,b+k],[x+k,b+k],
                  [x+k,e],[x-k,e],[x-k,b+k],[x-half,b+k],[x-half,b-k],[x-k,b-k]]
    cross_geo=_ring([[round(a,3),round(c,3)] for a,c in cross_points]); cross_geo["points"].append([x,b])
    add("cross",cross_geo,[x-half,y,x+half,e],support,["layout-roof"],assumed=not support,
        extra={"placement":"above-roof" if above else "within-roof"})
    add("cross-level",{"points":[[ground_x[0],b],[x,b],[ground_x[-1],b]],"segments":[[0,1],[1,2]],
        "siteSources":[None,{"part":"layout-cross","index":12},None]},[min(ground_x[0],x),b,max(ground_x[-1],x),b],support,["layout-cross"],assumed=not support)
    relate("building-on-ground",["layout-ground",building["id"]],0)
    relate("roof-on-building",[building["id"],"layout-roof"],0)
    for role,regions in (("door",doors),("window",windows)):
        for region in regions:
            a,b,c,d=[round(v,3) for v in region["placedBounds"]]
            sources=[None,None,ground_site(c),ground_site(a)] if role=="door" else None
            item=add(role,_ring([[a,b],[c,b],[c,d],[a,d]],sources),[a,b,c,d],region["spans"],
                ["layout-ground",building["id"]] if role=="door" else [building["id"]], extra={"sourceRegion":region["bounds"]})
            relate(item["id"]+"-inside-building",[building["id"],item["id"]],sum(max(0,v) for v in (l-a,top-b,c-r,d-g)))
            if role=="door":relate(item["id"]+"-on-ground",["layout-ground",item["id"]],d-g)
            else:relate(item["id"]+"-smaller-than-door",[item["id"]]+[p["id"] for p in parts if p["role"]=="door"],(c-a)*(d-b),0,dw*dh*.75)
    return {"schema":"gojo.quickdraw-area-layout.v1","sourceId":observed.get("sourceId"),"status":"proposed",
        "parts":parts,"relations":relations,"alternatives":distinct,"policy":deepcopy(POLICY),"sampleReduction":reduced,
        "sequence":["ground level","building area","attached roof area","upper cross region","optional openings"],
        "qualification":POLICY["qualification"],"sourceUnchanged":True,
        "openQuestions":["One main building and one main roof are assumed; side views and multiple towers are compressed.",
                         "Missing cross uses an explicit prior-only placement. Missing openings are left open."]}


def enrich_context(observed, context):
    result=deepcopy(context)
    for key in ("parts","relations"):
        result[key]=[p for p in result.get(key,[]) if not p["id"].startswith("layout-")]
    layout=estimate(observed)
    result["parts"].extend(deepcopy(layout["parts"]))
    result["relations"].extend(deepcopy(layout["relations"]))
    for i, alternative in enumerate(layout["alternatives"][1:],2):
        l,t,r,b=alternative["bounds"]
        result["parts"].append({"id":f"layout-alternative-building-{i}","kind":"area-layout-alternative", "role":"building-alternative",
            "participants":["layout-ground"],"spans":_region_support(observed["spans"],alternative["bounds"]),
            "bounds":alternative["bounds"],"geometry":_ring([[l,t],[r,t],[r,b],[l,b]]),"status":"alternative-region",
            "comparisons":[_comparison(value,0,1,"authored regional cost: "+key) for key,value in alternative["costTerms"].items()],
            "residue":["Alternative area retained; not combined with the chosen building as independent support."],
            "qualification":POLICY["qualification"]})
    return result,layout
