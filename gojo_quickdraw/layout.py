"""Coarse church placement proposals. Ideal scaffolds never replace source ink.

Regional extent/direction reads and authored relative-placement costs propose a
small layout. These are finite heuristic costs, not learned likelihoods or a
native global solver. Explicit context witnesses retain source and dependencies.
"""
from __future__ import annotations

from copy import deepcopy
import itertools
import math

COLORS = {"ground": "#63735f", "building": "#277da8", "tower": "#56729a", "roof": "#bf712a",
          "cross": "#ae3f6d", "cross-level": "#ae3f6d", "door": "#23846c", "window": "#8c68aa"}
POLICY = {"version": "church-area-layout.v3", "learned": False, "maximumSamples": 2048,
          "maximumUprights": 16, "maximumDoors": 2, "maximumWindows": 8,
          "maximumRoofs": 6, "maximumCrosses": 6, "domePartitions": 32,
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


def _rectangle(bounds, top_anchors=(), bottom_anchors=(), top_source=None, bottom_source=None):
    """Expose boundary sites by splitting actual ordered rectangle edges."""
    l,t,r,b=bounds; points=[[l,t],[r,t],[r,b],[l,b]]; sources=[None]*4
    geometry={"points":points,"segments":[],"fillIndices":[0,1,2,3],"siteSources":sources}
    maps=[]
    for y,start,end,anchors,source in ((t,0,1,top_anchors,top_source),(b,3,2,bottom_anchors,bottom_source)):
        mapping={l:start,r:end}
        for x in sorted(set(round(x,3) for x in anchors if l<x<r)):
            mapping[x]=len(points);points.append([x,y]);sources.append(None)
        if source:
            for x,index in mapping.items():sources[index]=source(x)
        indices=[mapping[x] for x in sorted(mapping,reverse=y==b)]
        geometry['segments'] += [[a,c] for a,c in zip(indices,indices[1:])]
        maps.append(mapping)
    geometry['segments'] += [[1,2],[3,0]]
    return geometry,maps


def _cap_geometry(cap, parent_id, parent_sites):
    l,top,r,base=cap['bounds'];mid=round((l+r)/2,3)
    if cap['shape']=='dome':
        points=[[round(mid+(r-l)/2*math.cos(math.pi-i*math.pi/POLICY['domePartitions']),3),
                 round(base-(base-top)*math.sin(math.pi-i*math.pi/POLICY['domePartitions']),3)]
                for i in range(POLICY['domePartitions']+1)]
        points[0]=[l,base];points[-1]=[r,base]
    else:points=[[l,base],cap['apex'],[r,base]]
    points.append([mid,base]);sources=[]
    for x,y in points:
        sources.append({'part':parent_id,'index':parent_sites[x]} if y==base and x in parent_sites else None)
    geometry=_ring(points,sources)
    if cap['shape']=='dome':
        count=POLICY['domePartitions']
        geometry['boundaryGroups']=[{'name':'domed cap','segments':list(range(count))},
                                    {'name':'base','segments':[count,count+1]}]
    return geometry


def _region_support(spans, bounds):
    l, t, r, b = bounds
    return [s["id"] for s in spans if any(l <= p[0] <= r and t <= p[1] <= b for p in s["controls"])]


def _opening_regions(observed, bounds, excluded):
    """Small source regions suggest optional openings without fitting a contour."""
    from .layout_openings import distance
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
                # A side may attach partway along a slightly overhanging beam.
                near = min([distance(p,x,y) for p in (a[0],a[-1]) for x,y in zip(b,b[1:])] +
                           [distance(p,x,y) for p in (b[0],b[-1]) for x,y in zip(a,a[1:])])
                if near < tolerance:
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
        if s['id'] not in cross_spans and abs(b[0]-a[0]) > .35*sw and abs(b[1]-a[1]) < .25*abs(b[0]-a[0]) and st <= (a[1]+b[1])/2 < ground-.18*sh:
            horizontal.append((a[1]+b[1])/2)
    widths = [(lx,rx), (lx-.03*sw,rx+.03*sw)]
    for a,b in itertools.combinations(uprights, 2):
        l,r = sorted((a["x"],b["x"]))
        if r-l > .32*sw:
            widths.append((l,r))
    tops = [st+fraction*sh for fraction in (.35,.45,.55,.65)] + horizontal
    tops += [s["top"] for s in uprights if st <= s["top"] <= ground-.18*sh]
    alternatives = []
    for l,r in widths:
        if r-l < .25*sw:
            continue
        for top in sorted(set(round(v, 3) for v in tops)):
            if not st <= top < ground-.18*sh:
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
    bw,bh = r-l,g-top
    from .layout_roofs import regions as roof_regions
    from .layout_towers import regions as tower_regions
    from .layout_openings import classify as opening_shape, geometry as opening_geometry
    from .layout_markers import markers, geometry as marker_geometry
    roofs, roof_reads = roof_regions(observed,[l,top,r,g],scene,cross_spans,POLICY['maximumRoofs'])
    towers,tower_reads=tower_regions(observed,[l,top,r,g],scene,roofs,cross_spans,
                                    POLICY['maximumRoofs']-sum(cap['towerBottom'] is not None for cap in roofs))
    for cap in roofs:
        if cap['shape']=='dome':cap['apex']=[round((cap['bounds'][0]+cap['bounds'][2])/2,3),cap['bounds'][1]]
    opening_regions = _opening_regions(observed,[l,top,r,g],cross_spans)
    for region in opening_regions:region.update(opening_shape(observed,region))
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
        if (not region['closedTrace'] or d >= g-.12*bh or b < top+.08*bh or len(windows) >= POLICY["maximumWindows"] or
            any(set(region["spans"]) & set(o["spans"]) for o in doors+windows)):
            continue
        width=min(c-a,.85*dw); height=min(d-b,.75*dh)
        x=_clamp((a+c)/2,l+.04*bw+width/2,r-.04*bw-width/2)
        y=_clamp((b+d)/2,top+.07*bh+height/2,g-.12*bh-height/2)
        windows.append({**region,"placedBounds":[x-width/2,y-height/2,x+width/2,y+height/2]})
    parts=[]; relations=[]
    def add(role, geo, bounds, ids=(), parents=(), assumed=False, extra=None):
        ident="layout-"+role+("-"+str(sum(p["role"]==role for p in parts)+1) if role in ("door","window","tower") or any(p["role"]==role for p in parts) else "")
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
    def anchors(cap):
        a,_,b,_=cap['bounds'];return [a,round((a+b)/2,3),b]
    grounded=[x for cap in roofs if cap['towerBottom']==g for x in anchors(cap)]
    grounded += [x for tower in towers if tower['bounds'][3]==g for x in anchors(tower)]
    ground_x=sorted(set([round(l-.10*bw,3),l,r,round(r+.10*bw,3)]+grounded+
                       [round(v,3) for d in doors for v in (d['placedBounds'][0],d['placedBounds'][2])]))
    ground_points=[[x,g] for x in ground_x]
    source_ground=_region_support(spans,[sl,g-.10*sh,sr,g+.10*sh])
    add('ground',{'points':ground_points,'segments':[[i,i+1] for i in range(len(ground_points)-1)]},
        [ground_x[0],g,ground_x[-1],g],source_ground,assumed=not source_ground)
    ground_site=lambda x:{'part':'layout-ground','index':ground_x.index(round(x,3))}
    top_anchors=[x for cap in roofs if cap['towerBottom'] in (None,top) for x in anchors(cap)]
    top_anchors += [x for tower in towers if tower['bounds'][3]==top for x in anchors(tower)]
    body_geo,body_sites=_rectangle([l,top,r,g],top_anchors,bottom_source=ground_site)
    building=add('building',body_geo,[l,top,r,g],
        _region_support(spans,[l-.08*bw,top-.08*bh,r+.08*bw,g+.08*bh]),['layout-ground'],
        extra={'costTerms':distinct[0]['costTerms'],'cost':distinct[0]['cost']})
    building['comparisons']=[_comparison(value,0,1,'authored regional cost: '+key) for key,value in distinct[0]['costTerms'].items()]
    relate('building-on-ground',['layout-ground',building['id']],0)
    marker_parents=[]
    for cap in roofs:
        a,peak,b,base=cap['bounds'];bottom=cap['towerBottom']
        parent=building;sites=body_sites[0]
        if bottom is not None:
            from_body=lambda x:{'part':building['id'],'index':body_sites[0][x]} if x in body_sites[0] else None
            geo,tower_sites=_rectangle([a,base,b,bottom],anchors(cap),anchors(cap),
                                      bottom_source=ground_site if bottom==g else from_body)
            parent=add('tower',geo,[a,base,b,bottom],cap['towerSpans'],['layout-ground',building['id']],
                extra={'lowerBoundary':'inferred-level','expected':{'meaning':'Upright support below a cap; no drawn separating line is required.'}})
            sites=tower_sites[0]
            relate(parent['id']+'-on-'+('ground' if bottom==g else 'building'),
                   ['layout-ground' if bottom==g else building['id'],parent['id']],0)
        roof=add('roof',_cap_geometry(cap,parent['id'],sites),cap['bounds'],cap['spans'],[parent['id']],
            assumed=not cap['spans'],extra={'shape':cap['shape'],'apex':cap['apex'],'fallback':cap.get('fallback',False),
                 'costTerms':cap['costTerms'],'shapeCosts':cap['shapeCosts'],
                 'construction':{'partitions':POLICY['domePartitions'] if cap['shape']=='dome' else 2,
                                 'basis':'Half ellipse or two slopes with an explicit closing base; finite proposed sites.'}})
        roof['comparisons']=[_comparison(value,0,1,'authored cap cost: '+key) for key,value in {**cap['costTerms'],**cap['shapeCosts']}.items()]
        gap=max(0,parent['bounds'][0]-b,a-parent['bounds'][2])
        relate(roof['id']+'-on-'+parent['id'],[parent['id'],roof['id']],gap)
        marker_parents.append(roof)
    for tower in towers:
        a,b,c,d=tower['bounds']
        from_body=lambda x:{'part':building['id'],'index':body_sites[0][x]} if x in body_sites[0] else None
        geo,_=_rectangle(tower['bounds'],bottom_anchors=anchors(tower),
                         bottom_source=ground_site if d==g else from_body)
        part=add('tower',geo,tower['bounds'],tower['spans'],['layout-ground',building['id']],
                 extra={'lowerBoundary':'inferred-level','roofOptional':True,'apex':[round((a+c)/2,3),b]})
        marker_parents.append(part)
    cross_markers,marker_reads=markers(observed,marker_parents,[l,top,r,g],scene,POLICY['maximumCrosses'])
    for hint in cross_markers:
        parent=marker_parents[hint['roofIndex']] if hint['roofIndex'] is not None else building
        geo,bounds,placement=marker_geometry(observed,hint,parent,scene)
        marker=add('cross',geo,bounds,hint['spans'],[parent['id']],assumed=not hint['spans'],
            extra={'placement':placement,'markerBasis':hint['markerBasis'],
                   **({'status':'partial-marker-prior','missingHorizontalArm':True} if hint.get('missingHorizontalArm') else {})})
        x,y=geo['points'][12]
        left,right=(parent['bounds'][0]-.1*bw,parent['bounds'][2]+.1*bw) if len(cross_markers)>1 else (ground_x[0],ground_x[-1])
        add('cross-level',{'points':[[left,y],[x,y],[right,y]],'segments':[[0,1],[1,2]],
            'siteSources':[None,{'part':marker['id'],'index':12},None]},[min(left,x),y,max(right,x),y],
            hint['spans'],[marker['id']],assumed=not hint['spans'])
    for role,regions in (("door",doors),("window",windows)):
        for region in regions:
            a,b,c,d=[round(v,3) for v in region["placedBounds"]]
            item=add(role,opening_geometry([a,b,c,d],region,ground_site if role=='door' else None),[a,b,c,d],region['spans'],
                ['layout-ground',building['id']] if role=='door' else [building['id']],
                extra={'sourceRegion':region['bounds'],'shape':region['shape'],'shapeCosts':region['shapeCosts'],
                       'shapeReads':region['shapeReads']})
            relate(item["id"]+"-inside-building",[building["id"],item["id"]],sum(max(0,v) for v in (l-a,top-b,c-r,d-g)))
            if role=="door":relate(item["id"]+"-on-ground",["layout-ground",item["id"]],d-g)
            else:relate(item["id"]+"-smaller-than-door",[item["id"]]+[p["id"] for p in parts if p["role"]=="door"],(c-a)*(d-b),0,dw*dh*.75)
    return {"schema":"gojo.quickdraw-area-layout.v1","sourceId":observed.get("sourceId"),"status":"proposed",
        "parts":parts,"relations":relations,"alternatives":distinct,"policy":deepcopy(POLICY),"sampleReduction":reduced,
        "sequence":["ground level","building area","local cap and tower areas","independent upper markers","optional openings"],
        "roofReads":roof_reads,"towerReads":tower_reads,"markerReads":marker_reads,
        "qualification":POLICY["qualification"],"sourceUnchanged":True,
        "openQuestions":["One main building envelope remains; local towers and triangular or domed caps are provisional interpretations.",
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
