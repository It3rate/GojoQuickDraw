"""Source-conditioned CAD relation proposals over exposed construction bounds.

Names describe observations. The operative output is the same affine error and
binding grammar consumed by the native settlement/capture implementation.
"""
from collections import defaultdict
import math


def _center(p, axis=0):
    b=p['bounds'];return (b[axis]+b[axis+2])/2


def _extent(p, axis=0):
    b=p['bounds'];return b[axis+2]-b[axis]


def _vertical_faces(p):
    points=p['geometry']['points'];height=max(1,_extent(p,1));width=max(1,_extent(p))
    faces=set()
    for a,b in p['geometry']['segments']:
        u,v=points[a],points[b]
        if abs(u[0]-v[0])>.02*width or abs(u[1]-v[1])<.35*height:continue
        for side,k in (('left',0),('right',2)):
            if abs((u[0]+v[0])/2-p['bounds'][k])<.02*width:faces.add(side)
    return faces


def extend(parts, proposed, aliases, proposal, rules, context, connections):
    by_id={p['id']:p for p in parts};decisions=[];groups=[];edge_aligned=set()

    def opinion(name, layer, terms, support, tolerance=.25, influence=3):
        rules.append(dict(name=name,layer=layer,terms=[dict(part=p,coordinate=k,coefficient=c) for p,k,c in terms],
                          lower=-tolerance,upper=tolerance,influence=influence,supportParts=list(dict.fromkeys(support))))

    def construction(ident, kind, bindings, support, distance):
        connections['recipes'].append(dict(id=ident,family=kind,
            bindings=[dict(target=dict(part=p,coordinate=k),terms=[dict(part=q,coordinate=j,coefficient=c) for q,j,c in terms])
                      for p,k,terms in bindings],supportParts=list(dict.fromkeys(support)),captureDistance=distance,releasePressure=24))

    # Stacked contact is the context, rather than proximity between arbitrary
    # lines. Contained windows and an intentional wall margin are different.
    stacked=defaultdict(list)
    for child in parts:
        owner=by_id.get(child.get('parent'))
        if owner is None or child['status']=='prior-only' or owner['status']=='prior-only':continue
        if _vertical_faces(child)!={'left','right'} or _vertical_faces(owner)!={'left','right'}:continue
        target,parent=proposed[child['id']],proposed[owner['id']]
        if abs(target['bounds'][3]-parent['bounds'][1])>max(1,.03*_extent(parent,1)):continue
        if target['bounds'][1]>=parent['bounds'][1]:continue
        stacked[owner['id']].append(child)
        scale=max(_extent(owner),_extent(child));capture=max(4,min(12,.035*scale));search=max(4,min(32,.10*scale))
        selected=[]
        for side,k in (('left',0),('right',2)):
            distance=abs(child['bounds'][k]-owner['bounds'][k]);expected=abs(target['bounds'][k]-parent['bounds'][k])
            eligible=distance<=search and (distance<=math.sqrt(2)*capture or expected<=capture)
            ident=f"cad-collinear-{child['id']}-{owner['id']}-{side}"
            decisions.append(dict(id=ident,kind='collinear boundary continuation',parts=[child['id'],owner['id']],
                proposed=eligible,sourceOffset=distance,searchDistance=search,captureDistance=capture,
                basis='Two exposed vertical boundary faces across an already selected stacked contact. A distant inset and containment are not continuation.'))
            if eligible:selected.append((side,k));opinion('Stacked boundary supporting lines agree','cad-collinearity',
                [(child['id'],k,1),(owner['id'],k,-1)],[child['id'],owner['id']])
        if not selected:continue
        ident='cad-collinear-'+child['id']+'-'+owner['id']
        if len(selected)==2:
            bindings=[(child['id'],k,[(owner['id'],k,1)]) for k in (0,2)]
        elif selected[0][1]==0:
            bindings=[(child['id'],0,[(owner['id'],0,1)]),(child['id'],2,[(owner['id'],0,1),(child['id'],4,1)])]
        else:
            bindings=[(child['id'],2,[(owner['id'],2,1)]),(child['id'],0,[(owner['id'],2,1),(child['id'],4,-1)])]
        construction(ident,'collinearity',bindings,[child['id'],owner['id']],capture);edge_aligned.add(child['id'])

    front=proposal.get('view',{}).get('selected')=='front-like'
    for owner_id,children in stacked.items():
        owner=by_id[owner_id]
        evidence=[];seen=set()
        for pair in context.get('pairs',[]):
            members=[by_id.get(pair[k]) for k in ('left','right')];axis=by_id.get(pair['axisPart'])
            if any(p is None or p.get('parent')!=owner_id for p in members) or axis is None:continue
            left,right=(set(p.get('spans',[])) for p in members)
            if not left or not right or left&right:continue
            # Several derived routes of the same pair cannot strengthen it.
            key=tuple(sorted(left|right))
            if key in seen:continue
            seen.add(key);evidence.append(dict(parts=[p['id'] for p in members]+[axis['id']],sourceSpans=list(key)))
        for child in children:
            distance=abs(_center(child)-_center(owner));limit=max(12,min(48,.12*_extent(owner)))
            eligible=front and len(children)==1 and bool(evidence) and distance<=limit and child['id'] not in edge_aligned
            ident='cad-assembly-axis-'+child['id']+'-'+owner_id
            reason='One upper frame and distinct acquired reflected motifs support testing the parent axis. Inherited roof/cross axes are not additional votes.'
            decisions.append(dict(id=ident,kind='outward assembly symmetry',parts=[child['id'],owner_id],proposed=eligible,
                sourceOffset=distance,captureDistance=limit,evidenceGroups=evidence,
                basis=reason,remaining='Asymmetric/view alternatives remain possible; the prior is authored.'))
            if not eligible:continue
            support=[child['id'],owner_id]+[p for group in evidence for p in group['parts']]
            opinion('Supported assembly midpoint agrees with its parent axis','cad-symmetry',
                [(child['id'],0,1),(child['id'],2,1),(owner_id,0,-1),(owner_id,2,-1)],support,tolerance=.5)
            construction(ident,'shared assembly axis',[
                (child['id'],0,[(owner_id,0,.5),(owner_id,2,.5),(child['id'],4,-.5)]),
                (child['id'],2,[(owner_id,0,.5),(owner_id,2,.5),(child['id'],4,.5)])],support,limit)

    if front:
        # A row is a source-supported grouping hypothesis. Common extents and
        # levels use a spanning set, leaving already mirrored pairs uncounted.
        windows=defaultdict(list)
        for p in parts:
            if p['role']=='window' and p['status']!='prior-only' and p.get('spans'):
                family='round' if p['shape'] in ('circle','ellipse') else p['shape']
                windows[(p.get('parent'),family)].append(p)
        for (owner_id,_),items in windows.items():
            if owner_id not in by_id:continue
            rows=[]
            for item in sorted(items,key=lambda p:_center(p,1)):
                row=next((r for r in rows if abs(_center(item,1)-sum(_center(p,1) for p in r)/len(r))<=.35*max(_extent(item,1),max(_extent(p,1) for p in r))),None)
                if row is None:rows.append([item])
                else:row.append(item)
            for row in rows:
                if len(row)<3:continue
                row.sort(key=_center);widths=[_extent(p) for p in row];heights=[_extent(p,1) for p in row]
                spans=[set(p['spans']) for p in row];pitch=[_center(b)-_center(a) for a,b in zip(row,row[1:])]
                if max(widths)>1.8*min(widths) or max(heights)>1.8*min(heights):continue
                if any(a&b for i,a in enumerate(spans) for b in spans[i+1:]):continue
                if min(pitch)<=0 or max(pitch)>1.6*min(pitch):continue
                support=[p['id'] for p in row]+[owner_id];group_id='cad-row-'+row[0]['id']
                groups.append(dict(id=group_id,kind='repeated row',parts=support[:-1],parent=owner_id,
                    sourceSpans=sorted(set().union(*spans)),basis='Similar source-supported outlines in one owner and height band, with already similar pitches.'))
                parents={p['id']:p['id'] for p in row}
                def find(p):
                    while parents[p]!=p:p=parents[p]
                    return p
                for pair in proposal.get('symmetries',[]):
                    a,b=(aliases.get(pair[k]) for k in ('left','right'))
                    if a in parents and b in parents:parents[find(b)]=find(a)
                prototype=row[0]
                for member in row[1:]:
                    if find(member['id'])==find(prototype['id']):continue
                    parents[find(member['id'])]=find(prototype['id'])
                    for axis,lo,hi in ((0,0,2),(1,1,3)):
                        opinion('Repeated row has a common extent','cad-repetition',[(member['id'],hi,1),(member['id'],lo,-1),
                            (prototype['id'],hi,-1),(prototype['id'],lo,1)],support,tolerance=.5,influence=2)
                    opinion('Repeated row shares a head level','cad-repetition',[(member['id'],1,1),(prototype['id'],1,-1)],support,tolerance=.5,influence=2)
                for a,b,c in zip(row,row[1:],row[2:]):
                    opinion('Repeated row adjacent centre intervals agree','cad-spacing',
                        [(p['id'],k,m) for p,m in ((a,1),(b,-2),(c,1)) for k in (0,2)],support,tolerance=.5,influence=2)

    return dict(decisions=decisions,groups=groups,
        qualification='Authored CAD-inspired affine proposals, actual native exposed-port errors and conditional construction bindings. Distinct source parts and dependency routes remain distinct; no general CAD solver or automatic utility learning.')
