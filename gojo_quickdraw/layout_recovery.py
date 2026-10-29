"""Bounded second opinions from the first layout's unexplained source intervals.

Acquired paths and fitted profiles remain unchanged. A recovered cap, tower or
cross is an authored construction with selected source support, not a new joint
measurement or a ground-truth label.
"""
from copy import deepcopy
import itertools
import math

from . import layout_mapping
from .layout_roofs import _simplify

VERSION='church-residual-recovery.v1'


def _box(points):
    return [min(p[k] for p in points) for k in (0,1)]+[max(p[k] for p in points) for k in (0,1)]


def _residue(observed, layout):
    mapping=layout_mapping.build(observed,layout);by_id={s['id']:s for s in observed['spans']}
    lengths={sid:0. for sid in by_id};intervals={sid:[] for sid in by_id}
    for row in mapping['sourceCoverage']:
        if row['status']!='unresolved':continue
        sid=row['span'];a,b=row['from'],row['to'];points=[layout_mapping.point(by_id[sid]['controls'],a+(b-a)*i/8) for i in range(9)]
        lengths[sid]+=sum(math.dist(p,q) for p,q in zip(points,points[1:]));intervals[sid].append([a,b])
    return lengths,intervals


def _routes(strokes, tolerance):
    """One loop or two nearby acquisition strokes, retaining pen-up gaps."""
    for i,first in enumerate(strokes):
        a=first['points']
        if len(a)<2:continue
        if len(a)>=4 and math.dist(a[0],a[-1])<=tolerance:
            yield list(a),[first],[[a[-1],a[0]]]
        for second in strokes[i+1:i+3]:
            b=second['points']
            if len(b)<2:continue
            choices=[]
            for ra,rb in itertools.product((False,True),repeat=2):
                x=list(reversed(a)) if ra else list(a);y=list(reversed(b)) if rb else list(b)
                join,close=math.dist(x[-1],y[0]),math.dist(y[-1],x[0])
                if max(join,close)<=tolerance:choices.append((join+close,x,y))
            if choices:
                _,x,y=min(choices,key=lambda c:c[0])
                yield x+y,[first,second],[[x[-1],y[0]],[y[-1],x[0]]]


def _at_y(path,y):
    for a,b in zip(path,path[1:]):
        if min(a[1],b[1])<=y<=max(a[1],b[1]) and abs(b[1]-a[1])>1e-8:
            t=(y-a[1])/(b[1]-a[1]);return a[0]+t*(b[0]-a[0])
    return min(path,key=lambda p:abs(p[1]-y))[0]


def _pointed(observed, layout, lengths, intervals, scene):
    body=next((p for p in layout['parts'] if p['role']=='building'),None)
    if body is None:return []
    l,t,r,g=body['bounds'];sw,sh=scene[2]-scene[0],scene[3]-scene[1];candidates=[]
    for path,strokes,gaps in _routes(observed['strokes'][:96],max(8,.07*min(sw,sh))):
        ids=sorted({sid for stroke in strokes for sid in stroke.get('spans',[])})
        a,top,b,bottom=_box(path);width=b-a;height=bottom-top
        if not (.09*sw<width<.75*(r-l) and height>.35*sh and top<t-.08*sh and bottom>t+.15*(g-t)):continue
        if max(0,min(b,r)-max(a,l))<.55*width or sum(lengths.get(sid,0) for sid in ids)<.10*sh:continue
        peak=min(range(len(path)),key=lambda j:path[j][1]);apex=path[peak]
        if not a+.10*width<apex[0]<b-.10*width:continue
        branches=[]
        for direction in (1,-1):
            branch=[path[peak]]
            for step in range(1,len(path)):
                p=path[(peak+direction*step)%len(path)];branch.append(p)
                if p[1]>=bottom-.04*height:break
            branches.append(_simplify(branch,.018*min(sw,sh)))
        if any(len(branch)<3 for branch in branches):continue
        vectors=[[branch[1][k]-apex[k] for k in (0,1)] for branch in branches]
        norm=math.hypot(*vectors[0])*math.hypot(*vectors[1])
        angle=math.degrees(math.acos(max(-1,min(1,sum(x*y for x,y in zip(*vectors))/norm)))) if norm else 180
        if not 15<angle<130:continue
        shoulders=[]
        for branch in branches:
            knee=next((p[1] for p,q in zip(branch[1:],branch[2:]) if q[1]-p[1]>.12*height and
                       abs(q[0]-p[0])<.55*(q[1]-p[1])),None)
            if knee is not None:shoulders.append(knee)
        if len(shoulders)!=2:continue
        base=min(shoulders);tower_height=bottom-base
        if base-top<.065*sh or tower_height<.18*sh:continue
        sides=sorted(sum(_at_y(branch,base+f*tower_height) for f in (.30,.65))/2 for branch in branches)
        left,right=sides
        if not .07*sw<right-left<.75*(r-l):continue
        if not left-.15*width<apex[0]<right+.15*width:continue
        if any(p['role']=='roof' and abs(p['bounds'][1]-top)<.08*sh and
               p['bounds'][0]-.03*sw<=apex[0]<=p['bounds'][2]+.03*sw for p in layout['parts']):continue
        candidates.append(dict(bounds=[left,base,right,g if abs(bottom-g)<.12*sh else bottom],
            capBounds=[left,top,right,base],apex=list(apex),spans=ids,
            sourceIntervals=[dict(span=sid,ranges=intervals[sid]) for sid in ids if intervals.get(sid)],
            sourceStrokeOrder=[s['index'] for s in strokes],penUpGaps=gaps,tipAngle=angle,
            residualLength=sum(lengths.get(sid,0) for sid in ids),
            basis='Pointed near-closed acquisition route with two continuing walls; one common eave is inferred across unequal contour runs.'))
    selected=[]
    for c in sorted(candidates,key=lambda c:-c['residualLength']):
        if any(set(c['spans'])==set(p['spans']) or
               abs(sum(c['bounds'][::2])-sum(p['bounds'][::2]))<.15*sw for p in selected):continue
        selected.append(c)
    return selected[:4]


def _intersection(a,b,c,d):
    u=[b[k]-a[k] for k in (0,1)];v=[d[k]-c[k] for k in (0,1)];det=u[0]*v[1]-u[1]*v[0]
    if abs(det)<1e-8:return None
    q=[c[k]-a[k] for k in (0,1)];s=(q[0]*v[1]-q[1]*v[0])/det;t=(q[0]*u[1]-q[1]*u[0])/det
    if not 0<=s<=1 or not 0<=t<=1:return None
    norm=math.hypot(*u)*math.hypot(*v);angle=math.degrees(math.acos(max(-1,min(1,sum(x*y for x,y in zip(u,v))/norm)))) if norm else 0
    return s,t,[a[k]+s*u[k] for k in (0,1)],angle


def _crossings(observed,layout,lengths,intervals,scene):
    if any(p['role']=='cross' and p.get('spans') and not p.get('missingHorizontalArm') for p in layout['parts']):return [],'already-source-supported'
    sw,sh=scene[2]-scene[0],scene[3]-scene[1];scale=min(sw,sh);arms=[]
    owned={sid for p in layout['parts'] if p['role'] in ('door','window') for sid in p.get('spans',[])}
    for s in observed['spans'][:256]:
        if s['id'] in owned or lengths.get(s['id'],0)<.015*scale:continue
        pts=[layout_mapping.point(s['controls'],i/16) for i in range(17)];chord=math.dist(pts[0],pts[-1]);travel=sum(math.dist(a,b) for a,b in zip(pts,pts[1:]))
        if not .045*scale<chord<.65*max(sw,sh) or travel>1.22*chord:continue
        dx,dy=pts[-1][0]-pts[0][0],pts[-1][1]-pts[0][1]
        direction='vertical' if abs(dx)<.5*abs(dy) else 'horizontal' if abs(dy)<.5*abs(dx) else None
        if direction:arms.append((s,pts,direction,travel))
    candidates=[]
    for a,b in itertools.combinations(arms,2):
        if a[2]==b[2] or a[0]['strokeIndex']==b[0]['strokeIndex']:continue
        if a[2]=='horizontal':a,b=b,a
        for i,(p,q) in enumerate(zip(a[1],a[1][1:])):
            for j,(u,v) in enumerate(zip(b[1],b[1][1:])):
                hit=_intersection(p,q,u,v)
                if hit is None:continue
                x,y,point,angle=hit;ta,tb=(i+x)/16,(j+y)/16
                if not 55<angle<125 or min(ta,1-ta,tb,1-tb)<.14:continue
                if min(min(ta,1-ta)*a[3],min(tb,1-tb)*b[3])<max(3,.025*scale):continue
                if any(p['role'] in ('door','window') and p['bounds'][0]<=point[0]<=p['bounds'][2] and p['bounds'][1]<=point[1]<=p['bounds'][3] for p in layout['parts']):continue
                ids=[a[0]['id'],b[0]['id']]
                if any(set(c['spans'])==set(ids) for c in candidates):continue
                candidates.append(dict(point=point,spans=ids,segmentFractions=[ta,tb],
                    sourceIntervals=[dict(span=sid,ranges=intervals[sid]) for sid in ids],
                    intersectionAngle=angle,verticalBounds=_box(a[1]),horizontalBounds=_box(b[1]),
                    residualLength=sum(lengths[sid] for sid in ids),markerBasis='residual-curved-arm-crossing',
                    basis='Two substantial residual arms cross in their interiors; page height is an opinion, not an admission veto.'))
    candidates.sort(key=lambda c:-c['residualLength']);return candidates[:6],'residual-search'


def recover(observed, layout):
    if layout.get('recovery',{}).get('version')==VERSION:return deepcopy(layout)
    if not layout.get('parts') or not observed.get('spans'):return deepcopy(layout)
    from .layout import COLORS,_rectangle,_cap_geometry
    from .layout_markers import cross_geometry
    result=deepcopy(layout);scene=_box([p for s in observed['strokes'] for p in s['points']])
    if min(scene[2]-scene[0],scene[3]-scene[1])<=0:return result
    lengths,intervals=_residue(observed,layout);decisions=[]
    body=next((p for p in result['parts'] if p['role']=='building'),None)
    if body is None:return result
    def add(role,bounds,geo,ids,parents,**extra):
        ident='layout-recovered-'+role+'-'+str(1+sum(p['role']==role and p['id'].startswith('layout-recovered-') for p in result['parts']))
        p=dict(id=ident,role=role,kind='area-layout-'+role,bounds=[round(x,3) for x in bounds],geometry=geo,
            spans=list(ids),participants=list(parents),status='regional-estimate',color=COLORS[role],comparisons=[],
            residue=['Source intervals and the first proposal remain available; recovered geometry is an authored interpretation.'],
            qualification='Second-pass residual-supported construction, not acquired geometry or a training truth label.',**extra)
        result['parts'].append(p);return p
    for candidate in _pointed(observed,layout,lengths,intervals,scene):
        bounds=[round(x,3) for x in candidate['bounds']];geo,sites=_rectangle(bounds,top_anchors=[round(sum(bounds[::2])/2,3)])
        tower=add('tower',bounds,geo,candidate['spans'],['layout-ground',body['id']],lowerBoundary='inferred-level',apex=[sum(bounds[::2])/2,bounds[1]])
        cap=dict(shape='triangle',bounds=[bounds[0],round(candidate['capBounds'][1],3),bounds[2],bounds[1]],apex=[round(x,3) for x in candidate['apex']])
        roof=add('roof',cap['bounds'],_cap_geometry(cap,tower['id'],sites[0]),candidate['spans'],[tower['id']],shape='triangle',apex=cap['apex'])
        decisions.append(dict(kind='pointed enclosure',accepted=True,parts=[tower['id'],roof['id']],**candidate))
    # Reread residue after adding a structure; it is not a second independent
    # observation. Cross discovery still needs both arms left unexplained.
    after_lengths,after_intervals=_residue(observed,result)
    crosses,cross_status=_crossings(observed,result,after_lengths,after_intervals,scene)
    if crosses:
        removed={p['id'] for p in result['parts'] if p['role'] in ('cross','cross-level') and (p['status']=='prior-only' or p.get('missingHorizontalArm'))}
        result['parts']=[p for p in result['parts'] if p['id'] not in removed]
        result['relations']=[p for p in result.get('relations',[]) if not removed.intersection(p.get('participants',[]))]
        for candidate in crosses:
            x,y=candidate['point'];v,h=candidate['verticalBounds'],candidate['horizontalBounds'];bounds=[x-(h[2]-h[0])/2,v[1],x+(h[2]-h[0])/2,v[3]]
            owners=[p for p in result['parts'] if p['role'] in ('building','tower','roof') and
                    p['bounds'][0]-.02*(scene[2]-scene[0])<=bounds[0] and bounds[2]<=p['bounds'][2]+.02*(scene[2]-scene[0]) and
                    p['bounds'][1]<=bounds[1] and bounds[3]<=p['bounds'][3]+.03*(scene[3]-scene[1])]
            parent=min(owners,key=lambda p:(p['bounds'][2]-p['bounds'][0])*(p['bounds'][3]-p['bounds'][1])) if owners else body
            cross=add('cross',bounds,cross_geometry(x,v[1],v[3]-v[1],arm=(h[2]-h[0])/2,bar=y),candidate['spans'],[parent['id']],
                      placement=('within-' if y>=parent['bounds'][1] else 'above-')+parent['role'],markerBasis=candidate['markerBasis'])
            decisions.append(dict(kind='crossing',accepted=True,parts=[cross['id']],replacedPriorParts=sorted(removed),**candidate))
    result['recovery']=dict(version=VERSION,initialParts=deepcopy(layout['parts']),initialRelations=deepcopy(layout.get('relations',[])),
        sourceResidualIntervals=[dict(span=sid,ranges=ranges,length=lengths[sid]) for sid,ranges in intervals.items() if ranges],
        initialResidualLength=sum(lengths.values()),finalResidualLength=sum(_residue(observed,result)[0].values()),
        crossSearch=cross_status,decisions=decisions,
        qualification='Bounded authored second pass. First read, residual and recovery share one acquisition cause; unobserved closure gaps remain assumptions.')
    return result
