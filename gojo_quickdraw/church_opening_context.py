"""Revisable opening roles and row-level repetition from acquired intervals.

These are authored hypotheses, not labels. Incomplete outlines may participate
through a better-supported neighbor. No fixed window count is assumed.
"""
from copy import deepcopy
import math
from .layout import _opening_regions, _box
from .layout_openings import classify, geometry, source_paths


def _center(b):return (b[0]+b[2])/2


def _corners(observed, spans):
    selected=[s for s in observed['spans'] if s['id'] in spans]
    points=[p for s in selected for p in s['controls']]
    bounds=_box(points);scale=max(1,min(bounds[2]-bounds[0],bounds[3]-bounds[1]))
    turns=0
    for a in selected:
        for b in selected:
            if a is b or a.get('strokeIndex')!=b.get('strokeIndex'):continue
            p,q=a['controls'],b['controls']
            if math.dist(p[-1],q[0])>3:continue
            if p[-1][1]>bounds[1]+.5*(bounds[3]-bounds[1]):continue
            if min(math.dist(p[0],p[-1]),math.dist(q[0],q[-1]))<.12*scale:continue
            if len(p)>2 or len(q)>2:continue
            u=[p[-1][k]-p[-2][k] for k in (0,1)];v=[q[1][k]-q[0][k] for k in (0,1)]
            d=math.hypot(*u)*math.hypot(*v)
            if d and sum(u[k]*v[k] for k in (0,1))/d<.55:turns+=1
    return min(turns,4)


def revise(observed, layout):
    result=deepcopy(layout);body=next((p for p in result['parts'] if p['role']=='building'),None)
    changes=[];pairs=[]
    if body is None:return result,dict(changes=changes,pairs=pairs)
    l,t,r,g=body['bounds'];w,h=r-l,g-t
    side=any(p.get('shape')=='parallelogram' for p in result['parts'])
    def raw(part):
        pts=[p for path in source_paths(observed,part['spans']) for p in path]
        return _box(pts) if pts else part['bounds']
    grounded=[p for p in result['parts'] if p['role']=='door' and raw(p)[3]>g-.08*h]
    primary=max(grounded,key=lambda p:(raw(p)[2]-raw(p)[0])*(raw(p)[3]-raw(p)[1]),default=None)
    axis=_center(primary['bounds']) if primary and abs(_center(primary['bounds'])-_center(body['bounds']))<.2*w else _center(body['bounds'])
    regions=_opening_regions(observed,body['bounds'],set())
    candidates=[]
    for region in regions:
        a,y,b,z=region['bounds']
        if not (l+.025*w<a<b<r-.025*w and t+.025*h<y<z<g-.08*h):continue
        if b-a>.46*w or z-y>.60*h:continue
        read=classify(observed,region)
        # Weak candidates require a supported reflected neighbor below.
        if read['shapeReads']['boundaryCost']>.13 or read['shapeReads']['boundaryCoverage']<.42:continue
        existing=next((p for p in result['parts'] if p['role'] in ('door','window') and
            set(region['spans'])<=set(p.get('spans',[]))),None)
        if existing and existing is primary:continue
        candidates.append(dict(region=region,read=read,existing=existing,corners=_corners(observed,region['spans'])))
    possible=[]
    for i,a in enumerate(candidates):
        for b in candidates[i+1:]:
            ab,bb=a['region']['bounds'],b['region']['bounds']
            if _center(ab)>_center(bb):a0,b0=b,a;ab,bb=bb,ab
            else:a0,b0=a,b
            if not _center(ab)<axis<_center(bb) or set(a0['region']['spans'])&set(b0['region']['spans']):continue
            if abs(_center(ab)+_center(bb)-2*axis)>.28*w:continue
            if abs((ab[1]+ab[3]-bb[1]-bb[3])/2)>.17*h:continue
            if not .35<(ab[2]-ab[0])/max(1,bb[2]-bb[0])<2.85:continue
            if not .4<(ab[3]-ab[1])/max(1,bb[3]-bb[1])<2.5:continue
            if side:continue
            if not any(c['read']['openingSupport'] for c in (a0,b0)):continue
            score=abs(_center(ab)+_center(bb)-2*axis)/w+abs((ab[1]+ab[3]-bb[1]-bb[3])/2)/h
            possible.append((score,a0,b0))
    used=set()
    for _,left,right in sorted(possible,key=lambda p:p[0]):
        if any(set(c['region']['spans'])&used for c in (left,right)):continue
        chosen=[]
        for c in (left,right):
            p=c['existing'];region=c['region'];read=c['read']
            if p is None:
                ident='layout-context-window-'+str(len(changes)+1)
                p=dict(id=ident,role='window',kind='shape-area-window',bounds=region['bounds'],status='source-supported',
                       participants=[body['id']],spans=region['spans'],**read,geometry=geometry(region['bounds'],read))
                result['parts'].append(p)
                changes.append(dict(part=ident,action='admit-partial-window',spans=region['spans'],
                    basis='Floating acquired outline plus supported reflected neighbor; completion remains inferred.'))
            elif p['role']=='door':
                if primary is None or raw(p)[3]>=g-.08*h:chosen=[];break
                p.setdefault('earlierHypothesis',deepcopy(p))
                changes.append(dict(part=p['id'],action='door-to-window',spans=p['spans'],earlierBounds=p['bounds'],
                    basis='Actual selected ink floats above ground and has a window-like reflected neighbor; earlier door envelope had extended it to ground.'))
                p.update(role='window',kind='shape-area-window',bounds=region['bounds'],**read,geometry=geometry(region['bounds'],read))
            chosen.append(p)
        if len(chosen)!=2:continue
        def costs(c):
            strength=1 if c['read']['openingSupport'] else .2
            raw_cost=c['read']['shapeCosts'];n=c['corners']
            return {shape:strength*(raw_cost[shape]+(.025*n if shape=='ellipse' else -.020*n if shape=='rectangle' else 0))
                    for shape in ('rectangle','arch','ellipse')}
        reads=[costs(c) for c in (left,right)]
        alternatives=sorted((sum(c[s] for c in reads),s) for s in reads[0])
        best_cost,head=alternatives[0]
        independent=sum(min(c.values()) for c in reads)
        common=best_cost-independent<=.035
        if common:
            for p,c in zip(chosen,(left,right)):
                p.setdefault('earlierHypothesis',deepcopy(p))
                shape=head
                if head=='ellipse':
                    a,y,b,z=p['bounds'];shape='circle' if .8<(b-a)/max(1,z-y)<1.25 else 'ellipse'
                fraction=(left['read'].get('archFraction',0)+right['read'].get('archFraction',0))/2
                if head=='arch':fraction=next((c['read']['archFraction'] for c in (left,right) if c['read']['openingSupport'] and c['read']['shape']=='arch'),fraction or .4)
                changes.append(dict(part=p['id'],action='shared-opening-family',earlierShape=p.get('shape'),selected=shape,
                                    alternatives=[dict(shape=s,cost=v) for v,s in alternatives],corners=c['corners'],
                                    basis='Two source reads plus row repetition; incomplete evidence carries less authored support.'))
                p.update(shape=shape,archFraction=fraction,geometry=geometry(p['bounds'],dict(shape=shape,archFraction=fraction,**({'panePattern':p['panePattern']} if p.get('panePattern') else {}))))
        pairs.append(dict(left=chosen[0]['id'],right=chosen[1]['id'],axisPart=primary['id'] if primary else body['id'],
                          leader=chosen[1]['id'] if right['read']['openingSupport'] and not left['read']['openingSupport'] else chosen[0]['id'],
                          commonFamily=common,alternatives=[dict(shape=s,cost=v) for v,s in alternatives]))
        used.update(left['region']['spans']);used.update(right['region']['spans'])
    return result,dict(changes=changes,pairs=pairs,qualification='Authored row/role hypotheses from source intervals; no target church image or fixed window count.')
