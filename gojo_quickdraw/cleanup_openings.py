"""Bounded local opening translations with inspectable unresolved clearance.

No acquired shape moves. These are alternative placements in an authored
scaffold, preserving dimensions and the chosen enclosing wall.
"""
import math
from .layout import _comparison


def gap(a,b):
    return math.hypot(max(a[0]-b[2],b[0]-a[2],0),max(a[1]-b[3],b[1]-a[3],0))


def separate(specs, result):
    by_id={p['id']:p for p in specs};removed=set();checks=[]
    def room(owner):
        a,t,b,d=owner['bounds'];margin=.08*(b-a)
        return [a+margin,t+.06*(d-t),b-margin,d-.06*(d-t)],.06*(b-a)
    def box(x,y,w,h):return [round(x-w/2,3),round(y-h/2,3),round(x+w/2,3),round(y+h/2,3)]
    def valid(bounds,area,obstacles,clearance):
        a,t,b,d=bounds;l,y,r,z=area
        return l-.001<=a<b<=r+.001 and y-.001<=t<d<=z+.001 and all(gap(bounds,o['bounds'])>=clearance-.002 for o in obstacles)
    def move(p,bounds):
        if bounds!=p['bounds']:
            p['bounds']=bounds;p['changes'].append('Opening moved within its wall to leave clearance around other openings.')
    def omit(parts,owner):
        for p in parts:
            removed.add(p['id'])
            result['relations'].append({'id':p['id']+'-unplaced','kind':'shape-refinement-open-slot',
                'participants':[owner['id']],'spans':[],'status':'unplaced-optional',
                'residue':['Optional window left unplaced: no tested position keeps its size, containment and opening clearance.'],
                'proposedBounds':p['bounds']})
    handled=set()
    for pair in result['symmetries']:
        left,right=(by_id[pair[k]] for k in ('left','right'))
        if left['role']!='window' or right['role']!='window':continue
        owner=by_id[left['parent']];area,clearance=room(owner);axis=sum(by_id[pair['axisPart']]['bounds'][::2])/2
        obstacles=[p for p in specs if p['role'] in ('door','window') and p['parent']==owner['id'] and p not in (left,right)]
        w=left['bounds'][2]-left['bounds'][0];h=left['bounds'][3]-left['bounds'][1]
        y=(left['bounds'][1]+left['bounds'][3])/2;distance=axis-(left['bounds'][0]+left['bounds'][2])/2
        candidates=[]
        # If translation alone cannot accommodate a supported pair, a small
        # shared reduction is preferable to independently breaking its mirror.
        # The earlier size stays in the correspondence; no source line moves.
        for factor in (1,.9,.8):
            ww,hh=w*factor,h*factor
            max_distance=min(axis-area[0],area[2]-axis)-ww/2
            distances={distance,max_distance,(ww+clearance)/2};ys={y,area[1]+hh/2,area[3]-hh/2}
            for other in obstacles:
                a,t,b,d=other['bounds']
                distances.update((axis-a+clearance+ww/2,b-axis+clearance+ww/2))
                ys.update((t-clearance-hh/2,d+clearance+hh/2))
            for delta in distances:
                if delta<0:continue
                for cy in ys:
                    a,b=box(axis-delta,cy,ww,hh),box(axis+delta,cy,ww,hh)
                    if valid(a,area,obstacles,clearance) and valid(b,area,obstacles,clearance) and gap(a,b)>=clearance-.002:
                        candidates.append(((delta-distance)**2+(cy-y)**2,a,b))
            if candidates:
                if factor<1:
                    for p in (left,right):p['changes'].append('Paired opening size reduced together to keep wall margins and door clearance.')
                break
        if candidates:
            _,a,b=min(candidates);move(left,a);move(right,b);handled.update((left['id'],right['id']))
        elif all(p['status']=='prior-only' for p in (left,right)):
            omit((left,right),owner);handled.update((left['id'],right['id']))
    # Drawing-supported openings get first choice. A failed optional placement
    # stays an open proposal; an observed one stays visible with a failed read.
    windows=sorted((p for p in specs if p['role']=='window'),key=lambda p:(p['status']=='prior-only',p['id']))
    for p in windows:
        if p['id'] in handled:continue
        owner=by_id[p['parent']];area,clearance=room(owner)
        obstacles=[q for q in specs if q['role'] in ('door','window') and q['id'] not in removed and q is not p and q['parent']==owner['id']]
        a,t,b,d=p['bounds'];w,h=b-a,d-t;x,y=(a+b)/2,(t+d)/2
        xs={x,area[0]+w/2,area[2]-w/2};ys={y,area[1]+h/2,area[3]-h/2}
        for q in obstacles:
            l,top,r,bottom=q['bounds'];xs.update((l-clearance-w/2,r+clearance+w/2));ys.update((top-clearance-h/2,bottom+clearance+h/2))
        candidates=[]
        for cx in xs:
            for cy in ys:
                bounds=box(cx,cy,w,h)
                if valid(bounds,area,obstacles,clearance):candidates.append(((cx-x)**2+(cy-y)**2,bounds))
        if candidates:move(p,min(candidates)[1])
        elif p['status']=='prior-only':omit((p,),owner)
    specs[:]=[p for p in specs if p['id'] not in removed]
    result['symmetries'][:]=[p for p in result['symmetries'] if p['left'] not in removed and p['right'] not in removed]
    openings=[p for p in specs if p['role'] in ('door','window')]
    for i,a in enumerate(openings):
        for b in openings[i+1:]:
            if a['parent']!=b['parent'] or 'window' not in (a['role'],b['role']):continue
            owner=by_id[a['parent']];_,minimum=room(owner);actual=gap(a['bounds'],b['bounds']);shortfall=max(0,minimum-actual)
            ident=a['id']+'--'+b['id']+'-clearance';status='needs-review' if shortfall>.002 else 'within-allowance'
            participants=[owner['id'],a['id'],b['id']]
            result['relations'].append({'id':ident,'kind':'shape-refinement-opening-clearance','participants':participants,
                'spans':[],'status':status,'comparisons':[_comparison(round(actual,3),round(minimum,3),.002,'Euclidean separation of the two axis-aligned opening bounds')],
                'residue':['Authored minimum gap; supporting ink and differing source shapes remain unchanged.']})
            checks.append({'id':ident+'-check','kind':'clearance-check','participants':participants,'status':status,
                'label':a['role'].capitalize()+' / '+b['role']+' clearance',
                'nativeRead':{'surface':'context.relations','id':ident},
                'actual':round(actual,3),'minimum':round(minimum,3),'shortfall':round(shortfall,3),
                'qualification':'Native participating bounds remain readable; gap threshold is authored adapter policy.'})
    return checks
