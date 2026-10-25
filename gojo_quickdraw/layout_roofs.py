"""Local upper-contour proposals for the authored area-layout experiment.

Read caps and supporting upright regions at a declared coarse resolution. Source
profiles remain selected evidence; no fitted or acquired point is moved here.
"""
from __future__ import annotations

import math


def _simplify(points, tolerance):
    if len(points) < 3:
        return list(points)
    keep = {0, len(points)-1}; pending = [(0, len(points)-1)]
    while pending:
        start, end = pending.pop(); a, b = points[start], points[end]
        dx, dy = b[0]-a[0], b[1]-a[1]; length = dx*dx+dy*dy
        far, selected = tolerance, None
        for i in range(start+1, end):
            p = points[i]
            u = max(0, min(1, ((p[0]-a[0])*dx+(p[1]-a[1])*dy)/length)) if length else 0
            distance = math.hypot(p[0]-a[0]-u*dx, p[1]-a[1]-u*dy)
            if distance > far: far, selected = distance, i
        if selected is not None:
            keep.add(selected); pending.extend([(start, selected), (selected, end)])
    return [points[i] for i in sorted(keep)]


def _support(observed, stroke, points, tolerance):
    # A regional profile read, not an exclusive assignment of the whole stroke.
    l=min(p[0] for p in points)-tolerance; r=max(p[0] for p in points)+tolerance
    t=min(p[1] for p in points)-tolerance; b=max(p[1] for p in points)+tolerance
    return [s['id'] for s in observed['spans'] if s['strokeIndex']==stroke and
            any(l<=p[0]<=r and t<=p[1]<=b for p in s['controls'])]


def _arms(observed, scene, excluded):
    sw, sh = scene[2]-scene[0], scene[3]-scene[1]
    tolerance = .018*min(sw, sh); result=[]
    for stroke in observed['strokes']:
        if not stroke['points']: continue
        # Only an entirely selected cross stroke is omitted. A mixed stroke can
        # still supply a roof through other source intervals.
        if stroke.get('spans') and set(stroke['spans']) <= excluded: continue
        points = _simplify(stroke['points'], tolerance)
        current=[]; signs=None
        def finish():
            if len(current)<2: return
            low, high=sorted((current[0],current[-1]),key=lambda p:p[1],reverse=True)
            width=abs(low[0]-high[0]); height=low[1]-high[1]
            if width < .045*sw or height < .055*sh: return
            ids=_support(observed,stroke['index'],current,tolerance)
            if ids:
                result.append({'bottom':low,'top':high,'points':list(current),'spans':ids,
                               'stroke':stroke['index'],'side':'left' if low[0]<high[0] else 'right'})
        for a,b in zip(points,points[1:]):
            dx,dy=b[0]-a[0],b[1]-a[1]
            direction=(1 if dx>0 else -1,1 if dy>0 else -1)
            slope=abs(dx)>.13*abs(dy) and abs(dy)>.12*abs(dx)
            if not slope:
                finish();current=[];signs=None
            elif direction==signs:
                current.append(b)
            else:
                finish();current=[a,b];signs=direction
        finish()
    # A fitted interval can carry a whole curved side whose local tangent turns
    # vertical near an eave. Keep that regional read alongside coarse runs.
    strokes={s['index']:s for s in observed['strokes']}
    for span in observed['spans']:
        if span['id'] in excluded: continue
        a,b=span['controls'][0],span['controls'][-1]
        low,high=sorted((a,b),key=lambda p:p[1],reverse=True)
        dx=abs(low[0]-high[0]);dy=low[1]-high[1]
        if dx<.045*sw or dy<.055*sh or dx<.13*dy or dy<.12*dx: continue
        stroke=strokes[span['strokeIndex']]
        points=stroke['points'][span['startIndex']:span['endIndex']+1] if 'startIndex' in span else [a,b]
        result.append({'bottom':low,'top':high,'points':_simplify(points,tolerance),
                       'spans':[span['id']],'stroke':span['strokeIndex'],
                       'side':'left' if low[0]<high[0] else 'right'})
    return result


def _model_cost(arms, l, r, base, apex):
    """Compare coarse contour heights to a tent and a half ellipse, in cap units."""
    x,y=apex; height=max(1,base-y); costs={'triangle':[], 'dome':[]}
    for a,b in (pair for points in arms for pair in zip(points,points[1:])):
        # Midpoints also constrain straight sides; endpoint-only reads would
        # make every two-line triangle look compatible with a circular cap.
        for p in (a,[(a[0]+b[0])/2,(a[1]+b[1])/2],b):
            width=max(1,x-l if p[0]<x else r-x); u=min(1,abs(p[0]-x)/width)
            actual=(base-p[1])/height
            costs['triangle'].append(abs(actual-(1-u)))
            costs['dome'].append(abs(actual-math.sqrt(max(0,1-u*u))))
    return {kind:sum(values)/max(1,len(values)) for kind,values in costs.items()}


def regions(observed, building, scene, excluded, maximum=6):
    l,t,r,g=building; sw,sh=scene[2]-scene[0],scene[3]-scene[1]; bw=r-l
    arms=_arms(observed,scene,excluded); candidates=[]; unattached=[]
    for a in arms:
        if a['side']!='left': continue
        for b in arms:
            if b['side']!='right': continue
            left,right=a['bottom'][0],b['bottom'][0]; width=right-left
            if width<.09*sw or width>1.4*bw: continue
            peak=[(a['top'][0]+b['top'][0])/2,min(a['top'][1],b['top'][1])]
            base=(a['bottom'][1]+b['bottom'][1])/2; height=base-peak[1]
            gap=math.dist(a['top'],b['top'])
            if not left+.08*width<peak[0]<right-.08*width: continue
            if (peak[1]>t-.025*sh or peak[1]>scene[1]+.62*sh or base>t+.15*sh or
                height<.065*sh or abs(a['bottom'][1]-b['bottom'][1])>.45*height+.035*sh or
                gap>.10*sh or gap>.36*width): continue
            # A cap may overhang or be offset, but a wholly disconnected contour
            # has no support from this building proposal (often writing nearby).
            if max(0,min(right,r)-max(left,l))<.25*width:
                unattached.append({'bounds':[left,peak[1],right,base],
                                   'spans':sorted(set(a['spans']+b['spans'])),
                                   'reason':'insufficient overlap with the proposed building footprint'})
                continue
            model=_model_cost([a['points'],b['points']],left,right,base,peak)
            dome=height<1.1*width and model['dome']+.025<model['triangle']
            terms={'tipSeparation':gap/max(width,height),
                   'eaveLevelDifference':abs(a['bottom'][1]-b['bottom'][1])/max(width,height),
                   'capProfile':model['dome' if dome else 'triangle']}
            candidates.append({'bounds':[left,peak[1],right,base],'apex':peak,
                               'shape':'dome' if dome else 'triangle','spans':sorted(set(a['spans']+b['spans'])),
                               'costTerms':terms,'shapeCosts':model,'cost':sum(terms.values())})
    for c in candidates:
        a,peak,b,base=c['bounds']
        # Prefer explaining the upper contour to selecting an interior eave
        # that happens to be straighter. Keep this finite cost inspectable.
        higher=[p['bounds'][1] for p in candidates if p['bounds'][1]<peak and
                p['bounds'][3]<base+.12*sh and p['bounds'][2]-p['bounds'][0]>.55*(b-a) and
                max(0,min(b,p['bounds'][2])-max(a,p['bounds'][0]))>.55*min(b-a,p['bounds'][2]-p['bounds'][0])]
        c['costTerms']['unexplainedUpperContour']=3*(peak-min(higher,default=peak))/sh
        c['cost']=sum(c['costTerms'].values())
    candidates.sort(key=lambda c:(c['cost'],-(c['bounds'][2]-c['bounds'][0])))
    selected=[]; withheld=[]
    for c in candidates:
        a,_,b,_=c['bounds']
        if any(max(0,min(b,p['bounds'][2])-max(a,p['bounds'][0]))>.45*min(b-a,p['bounds'][2]-p['bounds'][0]) for p in selected):
            continue
        if len(selected)==maximum: withheld.append(c);continue
        selected.append(c)
    selected.sort(key=lambda c:c['bounds'][0])
    for c in selected:
        a,peak,b,base=c['bounds']; width=b-a
        sides=[]
        for x in (a,b):
            rows=[]
            for s in observed['spans']:
                if s['id'] in excluded: continue
                p,q=s['controls'][0],s['controls'][-1]
                top,bottom=sorted((p[1],q[1])); dx=abs(q[0]-p[0]);dy=bottom-top
                if (dy>.10*sh and dx<.35*dy and top<base+.13*sh and bottom>base+.08*sh and
                    abs((p[0]+q[0])/2-x)<.20*width+.015*sw):
                    rows.append({'span':s['id'],'bottom':bottom,'top':top})
            sides.append(rows)
        long_pair=all(any(s['bottom']>g-.16*sh for s in side) for side in sides)
        narrow=width<.72*bw or len(selected)>1
        tower=narrow and (base<t-.055*sh or long_pair)
        if tower:
            c['towerBottom']=g if long_pair else t
            c['towerSpans']=sorted({s['span'] for side in sides for s in side})
            c['baseBoundary']='inferred-level'
        else:
            # Alignment is a soft, local default. A small or displaced cap keeps
            # its own width; only broad near-aligned roofs use the wall corners.
            if .72*bw<width<1.30*bw and abs((a+b-l-r)/2)<.12*bw:
                a,b=l,r
            base=t
        c['bounds']=[round(a,3),round(peak,3),round(b,3),round(base,3)]
        c['apex']=[round(min(b-.08*(b-a),max(a+.08*(b-a),c['apex'][0])),3),round(peak,3)]
        c['towerBottom']=round(c['towerBottom'],3) if tower else None
    return selected, {'armCount':len(arms),'candidateCount':len(candidates),
                      'withheldRegions':withheld,'capacityReached':bool(withheld),
                      'unattachedRegions':unattached,
                      'simplificationTolerance':.018*min(sw,sh)}
