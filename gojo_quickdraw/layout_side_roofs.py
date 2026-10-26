"""Broad upper bands can support an oblique roof without a front gable."""
from .layout_roofs import _simplify, _support


def regions(observed, building, scene, caps, towers, excluded):
    l,t,r,g = building; sw,sh = scene[2]-scene[0],scene[3]-scene[1]; width=r-l
    candidates=[]
    for stroke in observed['strokes']:
        points=_simplify(stroke['points'],.012*min(sw,sh))
        for a,b in zip(points,points[1:]):
            a,b=sorted((a,b)); dx=b[0]-a[0]; dy=abs(b[1]-a[1]); top=(a[1]+b[1])/2
            if dx<.45*width or dy>.25*dx or not t-.45*sh<top<t-.06*sh: continue
            ids=_support(observed,stroke['index'],[a,b],.02*sh)
            if not ids or set(ids)<=excluded:continue
            if any(max(0,min(b[0],c['bounds'][2])-max(a[0],c['bounds'][0]))>.5*dx for c in caps):continue
            left=max(l,a[0]-.15*dx);right=min(r,b[0]+.15*dx)
            adjacent=[]
            for tower in towers:
                tl,tt,tr,tb=tower['bounds']
                if tt>=top:continue
                if abs(b[0]-tl)<.18*width: right=tl;adjacent.append(tower)
                elif abs(a[0]-tr)<.18*width:left=tr;adjacent.append(tower)
            # Require an observed eave near the body top, or a sloping end
            # connecting the band. A second horizontal floating stroke is weak.
            support=[]
            for s in observed['spans']:
                if s['id'] in excluded:continue
                p,q=s['controls'][0],s['controls'][-1]; ex=abs(q[0]-p[0]);ey=abs(q[1]-p[1])
                eave=ex>.45*(right-left) and ey<.25*ex and abs((p[1]+q[1])/2-t)<.045*sh
                end=.05*sh<ey<t-top+.06*sh and ex<.65*ey and min(abs(p[0]-left),abs(p[0]-right))<.1*width and min(p[1],q[1])<top+.06*sh and max(p[1],q[1])>t-.06*sh
                if eave or end:support.append(s['id'])
            if not support or right-left<.35*width:continue
            upper_left=max(left-.12*width,min(left+.22*width,a[0]));upper_right=max(right-.22*width,min(right+.12*width,b[0]))
            quad=[[round(left,3),t],[round(upper_left,3),round(a[1],3)],
                  [round(upper_right,3),round(b[1],3)],[round(right,3),t]]
            candidates.append({'shape':'parallelogram','bounds':[min(p[0] for p in quad),min(p[1] for p in quad),max(p[0] for p in quad),t],
                'apex':[round((upper_left+upper_right)/2,3),round(top,3)],'quad':quad,
                'spans':list(dict.fromkeys(ids+support)),'towerBottom':None,
                'costTerms':{'upperBandSlope':dy/dx},'shapeCosts':{},'viewBasis':'oblique-roof-band'})
    selected=[]
    for c in sorted(candidates,key=lambda c:c['costTerms']['upperBandSlope']):
        if not any(max(0,min(c['bounds'][2],p['bounds'][2])-max(c['bounds'][0],p['bounds'][0]))>.4*(c['bounds'][2]-c['bounds'][0]) for p in selected):selected.append(c)
    return selected[:2]
