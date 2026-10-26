"""Authored shape-layer refinement with explicit attachments and prior additions.

New scaffolds are proposals. Source estimates and acquisition are observations,
not mutable working geometry. The native adapter retains before/after site
correspondence and actual shared attachment sites independently of these labels.
"""
from copy import deepcopy
import math
from .layout import COLORS, _ring, _rectangle, _clamp
from .layout_markers import cross_geometry
from .layout_openings import geometry as opening_geometry

POLICY = {'version':'church-shape-cleanup.v1','learned':False,
          'qualification':'Authored church expectations; not a learned likelihood or physical simulation.',
          'windowCountWhenAbsent':2,'sourceInkUnchanged':True}


def _center(bounds):return (bounds[0]+bounds[2])/2
def _width(bounds):return bounds[2]-bounds[0]
def _height(bounds):return bounds[3]-bounds[1]
def _round(bounds):return [round(x,3) for x in bounds]


def build(observed, layout):
    original = {p['id']:p for p in layout.get('parts',[]) if p['role']!='cross-level'}
    body = next((p for p in original.values() if p['role']=='building'),None)
    result={'schema':'gojo.quickdraw-shape-cleanup.v1','sourceId':observed.get('sourceId'),
            'policy':deepcopy(POLICY),'parts':[],'relations':[],'correspondences':[],
            'attachments':[],'symmetries':[],'sourceUnchanged':True}
    if not body:
        result['status']='open-no-building-area';return result
    l,t,r,g=body['bounds'];w,h=r-l,g-t;cx=(l+r)/2
    specs=[]; by_source={}
    span_order={s['id']:s.get('strokeIndex') for s in observed['spans']}
    def spec(role,bounds,source=None,parent=None,shape='rectangle',reason=None,**extra):
        ident='cleanup-'+(source['id'].removeprefix('layout-') if source else role+'-prior-'+str(len(specs)+1))
        p={'id':ident,'role':role,'bounds':_round(bounds),'shape':shape,'parent':parent,
           'sourcePart':source['id'] if source else None,'spans':[],
           'sourceStrokeOrder':sorted({span_order[s] for s in source.get('spans',[]) if s in span_order}) if source else [],
           'status':'prior-only' if source is None or source.get('status')=='prior-only' else 'refined-estimate',
           'changes':reason or [],**extra}
        specs.append(p)
        if source:by_source[source['id']]=p
        return p
    ground_source=next(p for p in original.values() if p['role']=='ground')
    ground=spec('ground',ground_source['bounds'],ground_source,shape='level')
    building=spec('building',body['bounds'],body,ground['id'])
    towers=[p for p in original.values() if p['role']=='tower']
    side_roof=any(p.get('shape')=='parallelogram' for p in original.values())
    offset_tower=len(towers)==1 and abs(_center(towers[0]['bounds'])-cx)>.26*w
    side=side_roof or offset_tower
    result['view']={'selected':'side-or-oblique' if side else 'front-like',
        'basis':'oblique roof band' if side_roof else 'one strongly offset tower' if offset_tower else 'front-like symmetry prior',
        'qualification':'A provisional presentation read; not a recovered camera or proven viewpoint.'}
    for tower in towers:
        bounds=list(tower['bounds']); reason=[]
        if side and (bounds[0]<l+.20*w or bounds[2]>r-.20*w):
            bounds[3]=g;reason.append('Side tower extends to the shared ground premise.')
        spec('tower',bounds,tower,ground['id'] if bounds[3]==g else building['id'],reason=reason)
    if side:
        for tower in [s for s in specs if s['role']=='tower' and s['bounds'][3]==g]:
            a,_,b,_=tower['bounds']
            if a>cx and a<r:building['bounds'][2]=a
            elif b<cx and b>l:building['bounds'][0]=b
        building['changes'].append('Main wall meets the adjacent side tower without an interior seam through it.')
    grounded=[s for s in specs if s['role'] in ('building','tower') and s['bounds'][3]==g]
    ground['bounds']=[round(min(p['bounds'][0] for p in grounded)-.04*w,3),g,
                      round(max(p['bounds'][2] for p in grounded)+.04*w,3),g]
    paired=set()
    if not side:
        candidates=[s for s in specs if s['role']=='tower']
        for left in candidates:
            if left['id'] in paired or _center(left['bounds'])>=cx:continue
            options=[right for right in candidates if right['id'] not in paired and _center(right['bounds'])>cx and
                     .55<_width(left['bounds'])/_width(right['bounds'])<1.8 and
                     .55<_height(left['bounds'])/_height(right['bounds'])<1.8 and left['parent']==right['parent']]
            if not options:continue
            right=min(options,key=lambda p:abs(_center(p['bounds'])+_center(left['bounds'])-2*cx))
            if abs(_center(right['bounds'])+_center(left['bounds'])-2*cx)>.28*w:continue
            tw=(_width(left['bounds'])+_width(right['bounds']))/2
            top=(left['bounds'][1]+right['bounds'][1])/2;bottom=(left['bounds'][3]+right['bounds'][3])/2
            distance=(_center(right['bounds'])-_center(left['bounds']))/2
            for p,sign in ((left,-1),(right,1)):
                x=cx+sign*distance;p['bounds']=_round([x-tw/2,top,x+tw/2,bottom]);p['changes'].append('Paired towers use equal size and mirrored placement.')
                paired.add(p['id'])
            result['symmetries'].append({'left':left['id'],'right':right['id'],'axisPart':building['id']})
    for roof in [p for p in original.values() if p['role']=='roof']:
        parent=next((by_source[i] for i in roof['participants'] if i in by_source),building)
        a,peak,b,base=roof['bounds'];pa,pt,pb,pbase=parent['bounds'];shape=roof.get('shape','triangle')
        height=base-peak;reason=[]
        if shape=='parallelogram':
            base=t;peak=base-height
            # Keep the roof beside the tower, preserving its side silhouette.
            for tower in [s for s in specs if s['role']=='tower']:
                if tower['bounds'][0]>cx and abs(b-tower['bounds'][0])<.18*w:b=tower['bounds'][0]
                elif tower['bounds'][2]<cx and abs(a-tower['bounds'][2])<.18*w:a=tower['bounds'][2]
            a=max(l,a);b=min(r,b)
            quad=roof['geometry']['points']; shift=((quad[1][0]-quad[0][0])+(quad[2][0]-quad[3][0]))/2
            shift=_clamp(shift,-.18*(b-a),.18*(b-a))
            extra={'shear':shift};reason.append('Side roof keeps a sloped end and parallel long edges.')
        else:
            if parent['role']=='tower' or .70*(pb-pa)<b-a<1.4*(pb-pa):a,b=pa,pb
            else:
                centre=_clamp((a+b)/2,pa+(b-a)/2,pb-(b-a)/2);a,b=centre-(b-a)/2,centre+(b-a)/2
            base=pt;peak=base-height;extra={}
            reason.append('Cap centered on its support; front peak made symmetric.')
        spec('roof',[a,peak,b,base],roof,parent['id'],shape=shape,reason=reason,**extra)
    for pair in list(result['symmetries']):
        caps=[next((p for p in specs if p['role']=='roof' and p['parent']==side_id),None) for side_id in (pair['left'],pair['right'])]
        if all(caps) and caps[0]['shape']==caps[1]['shape']:
            height=sum(_height(p['bounds']) for p in caps)/2
            for p in caps:p['bounds'][1]=round(p['bounds'][3]-height,3)
            result['symmetries'].append({'left':caps[0]['id'],'right':caps[1]['id'],'axisPart':building['id']})
    for cross in [p for p in original.values() if p['role']=='cross']:
        parent=next((by_source[i] for i in cross['participants'] if i in by_source and by_source[i]['role'] in ('roof','tower','building')),building)
        # A prior marker on the body should follow the visible highest support.
        if cross['status']=='prior-only':
            supports=[s for s in specs if s['role'] in ('roof','tower')]
            if supports:parent=min(supports,key=lambda p:p['bounds'][1])
        a,pt,b,base=parent['bounds'];height=_clamp(_height(cross['bounds']),.06*max(w,h),.22*max(w,h))
        height=min(height,.42*(b-a));x=(a+b)/2
        within=cross.get('placement','').startswith('within') and parent['role']=='roof' and parent['shape']!='parallelogram'
        if within:
            height=min(height,.38*(base-pt));top=pt+.42*(base-pt)
        else:top=pt-height
        spec('cross',[x-.3*height,top,x+.3*height,top+height],cross,parent['id'],shape='cross',
             reason=['Cross follows the support center/peak after refinement.'],placement='within' if within else 'attached-top',
             outlined=cross.get('markerBasis')=='outline-contour-read')
    # Openings inherit their enclosing frame, not an arbitrary global center.
    owners=[building]+[s for s in specs if s['role']=='tower' and s['bounds'][3]==g]
    def owner_for(bounds):
        x=_center(bounds)
        fits=[p for p in owners if p['bounds'][0]<=x<=p['bounds'][2]]
        return min(fits,key=lambda p:_width(p['bounds'])) if fits else building
    doors=[p for p in original.values() if p['role']=='door']
    if not doors:
        owner=next((p for p in owners[1:] if side),building)
        a,pt,b,base=owner['bounds'];dw=.23*(b-a);dh=min(.42*(base-pt),2.1*dw);x=(a+b)/2
        spec('door',[x-dw/2,base-dh,x+dw/2,base],None,owner['id'],shape='arch',reason=['Optional entrance supplied by the church prior.'])
    else:
        for door in doors:
            owner=owner_for(door['bounds']);a,pt,b,base=owner['bounds'];dw=min(_width(door['bounds']),.38*(b-a));dh=min(_height(door['bounds']),.65*(base-pt))
            x=(a+b)/2 if len(doors)==1 else _clamp(_center(door['bounds']),a+.08*(b-a)+dw/2,b-.08*(b-a)-dw/2)
            spec('door',[x-dw/2,base-dh,x+dw/2,base],door,owner['id'],shape=door.get('shape','rectangle'),reason=['Entrance contained in its wall and attached to ground.'])
    windows=[p for p in original.values() if p['role']=='window']
    if windows:
        for window in windows:
            owner=owner_for(window['bounds']);a,pt,b,base=owner['bounds'];ww=min(_width(window['bounds']),.18*(b-a));wh=min(_height(window['bounds']),.24*(base-pt))
            x=_clamp(_center(window['bounds']),a+.08*(b-a)+ww/2,b-.08*(b-a)-ww/2)
            y=_clamp((window['bounds'][1]+window['bounds'][3])/2,pt+.12*(base-pt)+wh/2,base-.22*(base-pt)-wh/2)
            spec('window',[x-ww/2,y-wh/2,x+ww/2,y+wh/2],window,owner['id'],shape=window.get('shape','rectangle'),reason=['Window constrained within its enclosing wall.'])
    else:
        # A bounded prior addition: two small openings on the main wall. Avoid
        # overlap with the doorway/tower by using the exposed wall area.
        a,pt,b,base=building['bounds']
        if side:
            for tower in owners[1:]:
                if _center(tower['bounds'])>cx:b=min(b,tower['bounds'][0])
                else:a=max(a,tower['bounds'][2])
        if b-a>.22*w:
            ww=.12*(b-a);wh=min(.20*h,1.6*ww);cy=pt+.39*h
            for x in (a+.25*(b-a),a+.75*(b-a)):
                spec('window',[x-ww/2,cy-wh/2,x+ww/2,cy+wh/2],None,building['id'],shape='arch',reason=['Optional paired windows supplied by the church prior.'])
    # Match a plausible pair inside the same wall; keep separate identities.
    wins=[s for s in specs if s['role']=='window']
    if len(wins)==2 and wins[0]['parent']==wins[1]['parent']:
        left,right=sorted(wins,key=lambda p:_center(p['bounds']));owner=next(p for p in specs if p['id']==left['parent'])
        average=(_center(left['bounds'])+_center(right['bounds']))/2;owner_axis=_center(owner['bounds'])
        mirrored=not side and _center(left['bounds'])<owner_axis<_center(right['bounds']) and abs(average-owner_axis)<.20*_width(owner['bounds'])
        axis=owner_axis if mirrored else average
        ww=(_width(left['bounds'])+_width(right['bounds']))/2;wh=(_height(left['bounds'])+_height(right['bounds']))/2
        y=sum(p['bounds'][1]+p['bounds'][3] for p in wins)/4;distance=(_center(right['bounds'])-_center(left['bounds']))/2
        for p,sign in ((left,-1),(right,1)):
            x=axis+sign*distance;p['bounds']=_round([x-ww/2,y-wh/2,x+ww/2,y+wh/2]);p['changes'].append('Paired openings share size and height.')
        if mirrored:result['symmetries'].append({'left':left['id'],'right':right['id'],'axisPart':owner['id']})
    by_id={s['id']:s for s in specs}
    # Build all geometry first, then split each support edge at requested
    # attachment sites. Explicit aliases, never coordinate coincidence, create
    # the native contacts. Parent order is construction order, not pen order.
    for p in specs:
        a,pt,b,base=p['bounds'];shape=p['shape']
        if shape=='level':geo={'points':[[a,pt],[b,base]],'segments':[[0,1]]}
        elif shape=='rectangle':geo=_ring([[a,pt],[b,pt],[b,base],[a,base]])
        elif shape=='triangle':geo=_ring([[a,base],[(a+b)/2,pt],[b,base]])
        elif shape=='dome':geo=_ring([[round((a+b)/2-(b-a)/2*math.cos(i*math.pi/32),3),round(base-(base-pt)*math.sin(i*math.pi/32),3)] for i in range(33)])
        elif shape=='parallelogram':geo=_ring([[a,base],[a+p['shear'],pt],[b+p['shear'],pt],[b,base]])
        elif shape=='cross':
            geo=cross_geometry((a+b)/2,pt,base-pt)
            geo['displaySegments']=list(range(12)) if p['outlined'] else [12,13]
        else:geo=opening_geometry(p['bounds'],{'shape':shape,'archFraction':.35})
        geo['points']=[_round(point) for point in geo['points']]
        p['geometry']=geo;geo['siteSources']=[None]*len(geo['points'])
    def anchor(parent,point):
        geo=parent['geometry'];point=_round(point)
        for i,other in enumerate(geo['points']):
            if math.dist(point,other)<.001:return i
        index=len(geo['points']);geo['points'].append(point);geo['siteSources'].append(None)
        for k,(a,b) in enumerate(geo['segments']):
            u,v=geo['points'][a],geo['points'][b]
            if math.dist(u,point)+math.dist(point,v)-math.dist(u,v)<.002:
                geo['segments'][k:k+1]=[[a,index],[index,b]];break
        return index
    for p in specs:
        if not p['parent']:continue
        parent=by_id[p['parent']];contacts=[];geo=p['geometry']
        if p['role'] in ('building','tower'):contacts=[2,3]
        elif p['role']=='roof':contacts=[0,2 if p['shape']=='triangle' else 32 if p['shape']=='dome' else 3]
        elif p['role']=='door':
            contacts=[i for i,point in enumerate(geo['points']) if abs(point[1]-g)<.002]
            parent=ground
        elif p['role']=='cross' and p['placement']=='attached-top':contacts=[14]
        for index in contacts:
            point=geo['points'][index];pi=anchor(parent,point)
            # This is an explicit proposed attachment. Use the selected site's
            # finite position, including a possible final rounding tick.
            geo['points'][index]=list(parent['geometry']['points'][pi])
            geo['siteSources'][index]={'part':parent['id'],'index':pi}
            result['attachments'].append({'parent':parent['id'],'child':p['id'],'parentPoint':pi,'childPoint':index})
        p['participants']=list(dict.fromkeys([i for i in (p['sourcePart'],p['parent'],parent['id']) if i]))
    if side:
        for tower in [s for s in specs if s['role']=='tower' and s['bounds'][3]==g]:
            for bi in (0,1):
                point=building['geometry']['points'][bi]
                if min(abs(point[0]-tower['bounds'][0]),abs(point[0]-tower['bounds'][2]))<.002 and tower['bounds'][1]<=point[1]<=g:
                    ti=anchor(tower,point);tower['geometry']['siteSources'][ti]={'part':building['id'],'index':bi}
                    if building['id'] not in tower['participants']:tower['participants'].append(building['id'])
                    result['attachments'].append({'parent':building['id'],'child':tower['id'],'parentPoint':bi,'childPoint':ti})
    for p in specs:
        p.setdefault('participants',[p['sourcePart']] if p['sourcePart'] else [])
        p.update(kind='shape-cleanup-'+p['role'],color=COLORS[p['role']],comparisons=[],
            qualification=POLICY['qualification'],residue=['Original ink and earlier shape estimates remain available.'])
        if p['sourcePart']:
            source=original[p['sourcePart']];old=source['geometry']['points'];pairs=[]
            for i,point in enumerate(p['geometry']['points']):
                # Correspondence is an explicit selected read, not site identity.
                a,pt,b,base=p['bounds'];ol,ot,orr,ob=source['bounds']
                target=[ol+(point[0]-a)*(orr-ol)/max(.001,b-a),ot+(point[1]-pt)*(ob-ot)/max(.001,base-pt)]
                j=min(range(len(old)),key=lambda k:math.dist(old[k],target));pairs.append([j,i])
            result['correspondences'].append({'source':p['sourcePart'],'target':p['id'],'points':pairs})
        if p['parent']:
            result['relations'].append({'id':p['id']+'-dependency','kind':'shape-refinement-dependency',
                'participants':list(dict.fromkeys([p['parent'],p['id']])),'spans':[],
                'status':'conditional-construction','residue':['Parent choice and containment are authored expectations.']})
    result['parts']=specs;result['status']='proposed'
    result['summary']={'parts':len(specs),'priorOnly':sum(p['status']=='prior-only' for p in specs),
                       'attachments':len(result['attachments']),'symmetryPairs':len(result['symmetries'])}
    return result


def enrich_context(observed, context, layout):
    result=deepcopy(context)
    for key in ('parts','relations'):result[key]=[p for p in result.get(key,[]) if not p['id'].startswith('cleanup-')]
    cleanup=build(observed,layout)
    result['parts'].extend(deepcopy(cleanup['parts']));result['relations'].extend(deepcopy(cleanup['relations']))
    return result,cleanup
