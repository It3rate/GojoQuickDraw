"""Finite outline-cross reads, including open contours and several pen strokes.

The inferred medial axes are construction references, never acquired ink.
"""
import math
from .layout_roofs import _simplify


def crosses(observed, scene):
    sw, sh = scene[2]-scene[0], scene[3]-scene[1]
    strokes = [s for s in observed['strokes'] if len(s['points']) > 1]
    groups = [[s] for s in strokes]
    remaining = set(range(len(strokes)))
    while remaining:
        pending = [min(remaining)]; group = set()
        while pending:
            i = pending.pop()
            if i in group: continue
            group.add(i); remaining.discard(i)
            ends = [strokes[i]['points'][0], strokes[i]['points'][-1]]
            for j in sorted(remaining):
                if min(math.dist(a,b) for a in ends for b in (strokes[j]['points'][0], strokes[j]['points'][-1])) < .055*min(sw,sh):
                    pending.append(j)
        if 1 < len(group) <= 12: groups.append([strokes[i] for i in sorted(group)])
    candidates = []
    for group in groups:
        paths = [_simplify(s['points'], .012*min(sw,sh)) for s in group]
        points = [p for path in paths for p in path]
        l,t,r,b = [min(p[0] for p in points),min(p[1] for p in points),max(p[0] for p in points),max(p[1] for p in points)]
        w,h = r-l,b-t
        if w < .035*sw or h < .06*sh or not .28 < w/h < 1.8 or t > scene[1]+.60*sh: continue
        edges = [(a,c) for path in paths for a,c in zip(path,path[1:])]
        horizontal = [(a,c) for a,c in edges if abs(c[1]-a[1]) < .30*abs(c[0]-a[0]) and abs(c[0]-a[0])>.14*w]
        vertical = [(a,c) for a,c in edges if abs(c[0]-a[0]) < .35*abs(c[1]-a[1])]
        if len(horizontal) < 3 or len(vertical) < 3: continue
        levels = sorted((a[1]+c[1])/2 for a,c in horizontal if t+.12*h < (a[1]+c[1])/2 < b-.16*h)
        options = []
        for upper in levels:
            for lower in levels:
                if not .06*h < lower-upper < .38*h: continue
                top = [p for p in points if p[1] < upper-.045*h]
                bottom = [p for p in points if p[1] > lower+.045*h]
                if len(top)<2 or len(bottom)<2: continue
                xs = sorted(p[0] for p in top+bottom); cx = sum(xs)/len(xs)
                stem = max(xs)-min(xs)
                # Inset upper and lower stems distinguish a cross from a box,
                # window grid, H, or a tower joined to one horizontal roof.
                if not .09*w < stem < .68*w or min(xs)<l+.12*w or max(xs)>r-.12*w: continue
                left = [p for p in points if p[0]<l+.10*w and upper-.18*h<p[1]<lower+.18*h]
                right = [p for p in points if p[0]>r-.10*w and upper-.18*h<p[1]<lower+.18*h]
                if not left or not right: continue
                cost = abs(cx-(l+r)/2)/w + abs((lower-upper)-stem)/max(w,h)
                options.append((cost,cx,upper,lower,stem))
        if not options: continue
        cost,cx,upper,lower,stem = min(options)
        ids = list(dict.fromkeys(sid for s in group for sid in s.get('spans',[])))
        candidates.append({'point':[cx,(upper+lower)/2], 'spans':ids,
            'markerBasis':'outline-contour-read','outlineBounds':[l,t,r,b],
            'stemWidth':stem,'barThickness':lower-upper,'outlineCost':cost,
            'openContour':any(min((math.dist(point,other) for j,s in enumerate(group) for end,other in enumerate((s['points'][0],s['points'][-1]))
                                  if (j,end)!=(i,at)),default=float('inf'))>.025*min(sw,sh)
                              for i,s in enumerate(group) for at,point in enumerate((s['points'][0],s['points'][-1])))})
    result=[]
    for c in sorted(candidates,key=lambda c:c['outlineCost']):
        if not any(set(c['spans']) & set(old['spans']) for old in result): result.append(c)
    return result
