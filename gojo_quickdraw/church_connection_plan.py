"""Generic affine binding recipes and conditional facade collision opinions."""


def extend(parts,proposed,aliases,proposal,rules,context):
    by_id={p['id']:p for p in parts};captures=[]
    def recipe(ident,bindings,distance=12):
        captures.append(dict(id=ident,bindings=[dict(target=dict(part=p,coordinate=k),
             terms=[dict(part=q,coordinate=j,coefficient=a) for q,j,a in terms]) for p,k,terms in bindings],
             captureDistance=distance,releasePressure=24))
    def alias(ident,a,ka,b,kb,distance=12):recipe(ident,[(a,ka,[(b,kb,1)])],distance)
    def center(ident,a,b,distance=None):
        # Fields 4/5 expose the new constructor's intrinsic extents. The gate
        # observes the corresponding public bound differences before import.
        if distance is None:
            owner=proposed[b]['bounds'];distance=max(12,min(32,.25*(owner[2]-owner[0])))
        recipe(ident,[(a,0,[(b,0,.5),(b,2,.5),(a,4,-.5)]),
                      (a,2,[(b,0,.5),(b,2,.5),(a,4,.5)])],distance)
    front=proposal.get('view',{}).get('selected')=='front-like'
    body=next((p for p in parts if p['role']=='building'),None)
    if body is None:return dict(recipes=[])
    body_id=body['id'];bb=proposed[body_id]['bounds'];scale=bb[2]-bb[0]
    columns=sorted((p for p in parts if p['role'] in ('building','tower') and p['shape']!='frame' and
                    abs(proposed[p['id']]['bounds'][3]-bb[3])<.06*max(scale,bb[3]-bb[1])),key=lambda p:sum(proposed[p['id']]['bounds'][::2]))
    if front:
        for i,(a,b) in enumerate(zip(columns,columns[1:])):
            ab,cb=proposed[a['id']]['bounds'],proposed[b['id']]['bounds']
            if min(ab[3],cb[3])-max(ab[1],cb[1])<.4*min(ab[3]-ab[1],cb[3]-cb[1]):continue
            if min(ab[2],cb[2])-max(ab[0],cb[0])>.35*min(ab[2]-ab[0],cb[2]-cb[0]):continue
            rules.append(dict(name='Neighboring facade columns do not overlap',layer='collision',
                terms=[dict(part=b['id'],coordinate=0,coefficient=1),dict(part=a['id'],coordinate=2,coefficient=-1)],
                lower=0,upper=100000,influence=4))
            if len(columns)>=3:
                middle=len(columns)//2
                if i<middle:alias('column-contact-'+str(i),b['id'],0,a['id'],2,24)
                else:alias('column-contact-'+str(i),a['id'],2,b['id'],0,24)
        for p in columns:
            if p['id']!=body_id:alias(p['id']+'-ground',p['id'],3,body_id,3)
        if body['shape']=='frame' and len(columns)>=3:
            alias('frame-upper-reference',body_id,1,columns[len(columns)//2]['id'],1)
    for p in parts:
        a=p['id'];b=p['parent'];role=p['role']
        if b not in by_id:continue
        if role=='roof':
            alias(a+'-base-contact',a,3,b,1)
            bounds=proposed[a]['bounds'];owner=proposed[b]['bounds']
            ratio=(bounds[2]-bounds[0])/max(1,owner[2]-owner[0])
            if p['shape']!='parallelogram' and .94<ratio<1.06:
                recipe(a+'-support-span',[(a,0,[(b,0,1)]),(a,2,[(b,2,1)])])
        elif role=='cross':
            if front or proposed[a].get('placement')=='attached-top':center(a+'-axis',a,b)
            if proposed[a].get('placement')=='attached-top':alias(a+'-foot-contact',a,3,b,1)
        elif role=='tower':
            bounds=proposed[a]['bounds'];owner=proposed[b]['bounds']
            if abs(bounds[3]-owner[1])<=max(1,.03*(owner[3]-owner[1])):
                alias(a+'-base-contact',a,3,b,1)
        elif role=='door':alias(a+'-floor-contact',a,3,b,3)
    pairs=proposal.get('symmetries',[])
    for pair in pairs:
        a,b,axis=(aliases.get(pair[k]) for k in ('left','right','axisPart'))
        if pair.get('leader')==pair['right']:a,b=b,a
        if any(p not in by_id for p in (a,b,axis)):continue
        bindings=[(b,0,[(axis,0,1),(axis,2,1),(a,2,-1)]),
                  (b,2,[(axis,0,1),(axis,2,1),(a,0,-1)]), (b,1,[(a,1,1)])]
        if by_id[b]['role']!='tower':bindings.append((b,3,[(a,3,1)]))
        recipe(a+'-'+b+'-reflection',bindings,24)
    if front and context.get('pairs'):
        axis=context['pairs'][0]['axisPart']
        if axis in by_id and by_id[axis]['role']=='door':
            rules.append(dict(name='Repeated facade has a central entrance axis',layer='symmetry',
                terms=[dict(part=axis,coordinate=0,coefficient=1),dict(part=axis,coordinate=2,coefficient=1),
                       dict(part=body_id,coordinate=0,coefficient=-1),dict(part=body_id,coordinate=2,coefficient=-1)],
                lower=-1,upper=1,influence=3))
            center('entrance-facade-axis',axis,body_id,24)
    return dict(recipes=captures,startPass=3,
                qualification='Conditional construction substitution with capture/hold/release witnesses; broad attraction remains separate. No post-render coordinate snapping.')
