"""Authored local pairing reads. Each accepted pair remains two participants.

The adapter emits native mirror witnesses and before/after correspondences;
these finite choices do not alter acquired ink or assert a recovered camera.
"""
from .layout import _comparison


def center(p):return (p['bounds'][0]+p['bounds'][2])/2
def width(p):return p['bounds'][2]-p['bounds'][0]
def height(p):return p['bounds'][3]-p['bounds'][1]


def cap_pairs(parts, body):
    axis=center(body);w=width(body);caps=[p for p in parts if p['role']=='roof' and p.get('shape') in ('triangle','dome')]
    candidates=[]
    for a in caps:
        for b in caps:
            if not center(a)<axis<center(b) or a['shape']!=b['shape']:continue
            residual=abs(center(a)+center(b)-2*axis)
            if residual>.23*w or not .4<width(a)/max(1,width(b))<2.5:continue
            if not .5<height(a)/max(1,height(b))<2:continue
            if abs(a['bounds'][3]-b['bounds'][3])>.12*w:continue
            candidates.append((residual,a,b))
    used=set();pairs=[]
    for _,a,b in sorted(candidates,key=lambda c:c[0]):
        if a['id'] in used or b['id'] in used:continue
        used.update((a['id'],b['id']));pairs.append((a,b))
    return pairs


def support_towers(specs, building, ground, result):
    l,t,r,g=building['bounds'];w=r-l
    for p in specs:
        if p['role']!='tower' or abs(p['bounds'][3]-g)<.002:continue
        a,top,b,base=p['bounds'];before=list(p['bounds'])
        # In this upright presentation, a tower terminating at the wall must
        # have its full footprint supported. A grounded tower is a different
        # depth hypothesis and may remain beside / in front of the wall.
        tw=min(b-a,w);x=min(r-tw/2,max(l+tw/2,(a+b)/2))
        p['bounds']=[round(x-tw/2,3),top,round(x+tw/2,3),t]
        p['parent']=building['id']
        if before!=p['bounds']:
            p['changes'].append('Upper tower footprint moved inside its supporting wall; connected cap and cross follow.')
        overhang=max(0,l-p['bounds'][0])+max(0,p['bounds'][2]-r)
        result['relations'].append({'id':p['id']+'-supported-footprint','kind':'shape-refinement-supported-footprint',
            'participants':[building['id'],p['id']],'spans':[], 'status':'conditional-construction',
            'comparisons':[_comparison(overhang,0,.002,'upper footprint inside the selected wall span')],
            'residue':['Authored upright support premise, not physical simulation. Earlier overhang remains in the preceding rung.'],
            'earlierBounds':before})


def pair_windows(specs, original, result, side=False):
    by_id={p['id']:p for p in specs};candidates=[]
    windows=[p for p in specs if p['role']=='window']
    for i,a in enumerate(windows):
        for b in windows[i+1:]:
            if a['parent']!=b['parent']:continue
            left,right=sorted((a,b),key=center);owner=by_id[a['parent']];ow=width(owner);oh=height(owner)
            doors=[p for p in specs if p['role']=='door' and p['parent']==owner['id'] and center(left)<center(p)<center(right)]
            axis_part=min(doors,key=lambda p:abs(center(p)-center(owner))) if doors else owner
            if side and not doors:continue
            axis=center(axis_part)
            if not center(left)<axis<center(right):continue
            if abs(center(left)+center(right)-2*axis)>.35*ow:continue
            if not .35<width(left)/max(1,width(right))<2.85 or not .45<height(left)/max(1,height(right))<2.2:continue
            if abs((left['bounds'][1]+left['bounds'][3]-right['bounds'][1]-right['bounds'][3])/2)>.30*oh:continue
            round_a=left['shape'] in ('circle','ellipse');round_b=right['shape'] in ('circle','ellipse')
            if round_a!=round_b:continue
            score=abs(center(left)+center(right)-2*axis)/ow+abs(height(left)-height(right))/oh
            candidates.append((score,left,right,owner,axis_part))
    used=set()
    for _,left,right,owner,axis_part in sorted(candidates,key=lambda c:c[0]):
        if left['id'] in used or right['id'] in used:continue
        axis=center(axis_part);ow=width(owner);oh=height(owner)
        ww=(width(left)+width(right))/2;wh=(height(left)+height(right))/2
        distance=min((center(right)-center(left))/2,axis-owner['bounds'][0]-.08*ow-ww/2,
                     owner['bounds'][2]-axis-.08*ow-ww/2)
        if distance<(ww+.06*ow)/2:continue
        y=sum(p['bounds'][1]+p['bounds'][3] for p in (left,right))/4
        y=min(owner['bounds'][3]-.06*oh-wh/2,max(owner['bounds'][1]+.06*oh+wh/2,y))
        # A common head is a separate repetition hypothesis, selected through
        # both source fits. Round and angular families are never conflated.
        if left['shape']!=right['shape'] and left['shape'] in ('rectangle','arch') and right['shape'] in ('rectangle','arch'):
            reads=[original[p['sourcePart']] for p in (left,right)]
            shape=min(('rectangle','arch'),key=lambda s:sum(p.get('shapeCosts',{}).get(s,1) for p in reads)+(.03 if s=='arch' else 0))
            if any(p.get('panePattern') for p in (left,right)):shape='rectangle'
            for p in (left,right):p['shape']=shape
        arch=sum(p.get('archFraction',.35) for p in (left,right))/2
        for p,sign in ((left,-1),(right,1)):
            x=axis+sign*distance;p['bounds']=[round(v,3) for v in (x-ww/2,y-wh/2,x+ww/2,y+wh/2)]
            if p['shape']=='arch':p['archFraction']=arch
            p['changes'].append('Two supported openings share size, level and mirrored placement in their local facade.')
        result['symmetries'].append({'left':left['id'],'right':right['id'],'axisPart':axis_part['id']})
        used.update((left['id'],right['id']))
