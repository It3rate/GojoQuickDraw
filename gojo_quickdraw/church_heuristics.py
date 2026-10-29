"""Compile the existing church experts into proposed machines and port relations.

Cleanup chooses families, support, pairing and foreground alternatives. Its final
coordinates are NOT settlement targets: observed machines start at source bounds.
All affine constraints are imported as native, inspectable error structures.
"""
from copy import deepcopy
import math

from . import cleanup, layout_mapping
from .layout import _opening_regions
from .layout_openings import classify, geometry

VERSION = 'church-heuristic-relations.v1'


def supplement(observed, layout):
    """Retry existing enclosure experts on unused intervals, without a count prior."""
    result = deepcopy(layout)
    body = next((p for p in result['parts'] if p['role'] == 'building'), None)
    decisions = []
    if body is None:
        return result, decisions
    original = layout_mapping.build(observed, layout)
    used = {row['span'] for row in original['sourceCoverage'] if row['status'] != 'unresolved'}
    # Do not remove a partially explained interval from consideration.
    unused = {row['span'] for row in original['sourceCoverage'] if row['status'] == 'unresolved'}
    excluded = used - unused
    l, t, r, b = body['bounds']; w, h = r-l, b-t
    for region in _opening_regions(observed, body['bounds'], excluded):
        a, y, c, z = region['bounds']
        if not (l+.04*w < a < c < r-.04*w and t+.04*h < y < z < b-.08*h):
            continue
        if c-a > .46*w or z-y > .60*h:
            continue
        if any(overlap(region['bounds'], p['bounds']) > .45 for p in result['parts'] if p['role'] in ('door', 'window')):
            continue
        read = classify(observed, region)
        accepted = (read['openingSupport'] and read['closedTrace'] and
                    read['shapeReads']['boundaryCoverage'] >= .76 and
                    read['shapeReads']['boundaryCost'] < .085)
        decisions.append(dict(bounds=region['bounds'], spans=region['spans'], accepted=accepted, read=read,
                              basis='Existing enclosure expert reconsidered residual ink; unknown remains an alternative.'))
        if not accepted:
            continue
        ident = 'layout-residual-window-' + str(len(decisions))
        result['parts'].append(dict(id=ident, role='window', kind='shape-area-window',
            bounds=region['bounds'], status='source-supported', participants=[body['id']],
            spans=region['spans'], **read, geometry=geometry(region['bounds'], read)))
    return result, decisions


def overlap(a, b):
    area = max(0, min(a[2], b[2])-max(a[0], b[0])) * max(0, min(a[3], b[3])-max(a[1], b[1]))
    return area / max(1e-6, min((a[2]-a[0])*(a[3]-a[1]), (b[2]-b[0])*(b[3]-b[1])))


def compile_plan(observed, layout, connections=False, cad=False, recover_missing=False):
    if cad and not connections:
        raise ValueError('CAD relation proposals require the connected construction experiment')
    if recover_missing:
        from .layout_recovery import recover
        layout=recover(observed,layout)
    recovery=layout.get('recovery')
    context={}
    if connections:
        from .church_opening_context import revise
        layout,context=revise(observed,layout)
    augmented, discoveries = supplement(observed, layout)
    proposal = cleanup.build(observed, augmented)
    if connections:
        source_to_proposal={p['sourcePart']:p['id'] for p in proposal['parts'] if p.get('sourcePart')}
        for pair in context.get('pairs',[]):
            if all(pair[k] in source_to_proposal for k in ('left','right','axisPart')):
                mapped={k:source_to_proposal[pair[k]] for k in ('left','right','axisPart')}
                mapped['leader']=source_to_proposal[pair['leader']]
                existing=next((p for p in proposal['symmetries'] if {p['left'],p['right']}=={mapped['left'],mapped['right']}),None)
                if existing:existing['leader']=mapped['leader']
                else:
                    proposal['symmetries'].append(mapped)
    original = {p['id']: p for p in augmented['parts']}
    # Visible wall cells will be derived from live frame/tower ports, rather
    # than independently parameterized rectangles which could drift apart.
    cells = {p['id'] for p in proposal['parts'] if p['role'] == 'building' and p['parent'] and
             next((q['shape'] for q in proposal['parts'] if q['id'] == p['parent']), None) == 'frame'}
    selected = [p for p in proposal['parts'] if p['id'] not in cells and p['role'] not in ('ground', 'cross-level')]
    aliases = {p['id']: (p['sourcePart'] or p['id']) for p in selected}
    for p in proposal['parts']:
        if p['id'] in cells:
            aliases[p['id']] = aliases[p['parent']]
    parts = []
    for p in selected:
        source = original.get(p['sourcePart'])
        seed = list(source['bounds'] if source else p['bounds'])
        if min(seed[2]-seed[0], seed[3]-seed[1]) < 1:
            continue
        a, t, b, d = p['bounds']; sw, sh = b-a, d-t
        points = [[seed[0]+(x-a)/max(.001,sw)*(seed[2]-seed[0]),
                   seed[1]+(y-t)/max(.001,sh)*(seed[3]-seed[1])] for x,y in p['geometry']['points']]
        geo = deepcopy(p['geometry']); geo.update(points=points, siteSources=[None]*len(points))
        if p['shape']=='frame':
            geo['segments']=[[0,1],[1,2],[2,3],[3,0]]
        if 'displaySegments' in geo:
            # Keep centerline-only crosses as centerlines; the outline is an
            # alternative construction, not extra visible ink.
            geo['segments'] = [geo['segments'][i] for i in geo['displaySegments']]
            geo.pop('displaySegments')
            geo.pop('boundaryGroups', None)
        item = dict(id=aliases[p['id']], role=p['role'], bounds=seed, shape=p['shape'],
                    status='prior-only' if p['status']=='prior-only' else 'source-supported',
                    spans=list(source.get('spans', [])) if source else [], participants=[], geometry=geo,
                    sourceGeometry=deepcopy(source.get('earlierHypothesis',source)['geometry']) if source else None,
                    sourceBounds=list(source.get('earlierHypothesis',source)['bounds']) if source else None,
                    proposal=p['id'], parent=aliases.get(p['parent']), changes=p['changes'])
        if item['parent']:
            item['participants'] = [item['parent']]
        parts.append(item)
    by_id = {p['id']: p for p in parts}
    proposed = {aliases[p['id']]: p for p in selected if aliases[p['id']] in by_id}
    rules = []

    def rule(name, layer, terms, lo=0, hi=0, influence=3):
        if all(part in by_id for part,_,_ in terms):
            rules.append(dict(name=name, layer=layer, terms=[dict(part=p, coordinate=c, coefficient=k) for p,c,k in terms],
                              lower=lo, upper=hi, influence=influence))

    def equal(name, layer, a, ca, b, cb, tolerance=.6):
        rule(name, layer, [(a,ca,1),(b,cb,-1)], -tolerance, tolerance)

    def center(a, b, layer='attachment'):
        rule('Centers coupled through exposed bounds', layer, [(a,0,1),(a,2,1),(b,0,-1),(b,2,-1)], -1,1)

    def size(a,b,axis,ratio=1,tolerance=.025,layer='repetition'):
        low,high=(0,2) if axis==0 else (1,3)
        # Ratio is a coarse local read; all terms participate bidirectionally.
        rule('Relative extent lower expectation',layer,[(a,high,1),(a,low,-1),(b,high,-ratio+tolerance),(b,low,ratio-tolerance)],0,100000)
        rule('Relative extent upper expectation',layer,[(a,high,1),(a,low,-1),(b,high,-ratio-tolerance),(b,low,ratio+tolerance)],-100000,0)

    def inside(a,b,axis=0,margin=.04):
        low,high=(0,2) if axis==0 else (1,3)
        rule('Unsupported or outside lower extent','support' if by_id[a]['role']=='tower' else 'containment',
             [(a,low,1),(b,low,-1+margin),(b,high,-margin)],0,100000,4)
        rule('Unsupported or outside upper extent','support' if by_id[a]['role']=='tower' else 'containment',
             [(b,high,1-margin),(b,low,margin),(a,high,-1)],0,100000,4)

    body = next((p for p in parts if p['role']=='building'), None)
    front = proposal.get('view',{}).get('selected') == 'front-like'
    for p in parts:
        ident=p['id']; parent=p['parent']; role=p['role']; target=proposed[ident]
        if role=='building':
            continue
        owner=by_id.get(parent)
        if owner is None:
            continue
        ob=proposed[parent]['bounds']; pb=target['bounds']
        if role=='tower':
            upper=abs(pb[3]-ob[1])<.01
            equal('Tower supported by selected wall','support',ident,3,parent,1 if upper else 3)
            if upper:
                inside(ident,parent,margin=0)
        elif role=='roof':
            equal('Roof base meets support top','attachment',ident,3,parent,1)
            if p['shape']!='parallelogram':
                center(ident,parent)
                ratio=(pb[2]-pb[0])/max(1,ob[2]-ob[0])
                size(ident,parent,0,ratio,.035,'roof-alignment')
            else:
                inside(ident,parent,margin=0)
        elif role=='cross':
            if target.get('placement')=='attached-top':
                center(ident,parent)
                equal('Cross foot follows moving support peak','attachment',ident,3,parent,1)
            elif front:
                center(ident,parent)
                inside(ident,parent,margin=.03)
            if target.get('placement')!='attached-top':
                v=(pb[3]-ob[1])/max(1,ob[3]-ob[1])
                rule('Cross retains its selected relative support level','attachment',
                     [(ident,3,1),(parent,1,-1+v),(parent,3,-v)],-1,1)
            ratio=(pb[3]-pb[1])/max(1,ob[3]-ob[1])
            size(ident,parent,1,ratio,max(.03,.25*ratio),'cross-scale')
        elif role in ('window','door'):
            inside(ident,parent,margin=.08 if role=='window' else .04)
            inside(ident,parent,axis=1,margin=.06 if role=='window' else 0)
            if role=='door':
                equal('Entrance base meets its support base','attachment',ident,3,parent,3)
            if p['shape']=='circle':
                rule('Circular opening equal diameters','shape',[(ident,2,1),(ident,0,-1),(ident,3,-1),(ident,1,1)],-.6,.6)
    for pair in proposal.get('symmetries',[]):
        a,b,axis = (aliases.get(pair[key]) for key in ('left','right','axisPart'))
        if a not in by_id or b not in by_id or axis not in by_id:
            continue
        rule('Selected pair mirror centers','symmetry',[(a,0,1),(a,2,1),(b,0,1),(b,2,1),(axis,0,-2),(axis,2,-2)],-1,1,4)
        size(a,b,0); size(a,b,1)
        equal('Repeated parts share top level','repetition',a,1,b,1)
        equal('Repeated parts share base level','repetition',a,3,b,3)
    openings=[p for p in parts if p['role'] in ('door','window')]
    for i,a in enumerate(openings):
        for b in openings[i+1:]:
            if a['parent']!=b['parent']:
                continue
            ab,bb=proposed[a['id']]['bounds'],proposed[b['id']]['bounds']
            owner=a['parent']
            if min(ab[3],bb[3])-max(ab[1],bb[1])>0:
                left,right=sorted((a,b),key=lambda p:sum(proposed[p['id']]['bounds'][::2]))
                rule('Opening clearance, horizontal','clearance',[(right['id'],0,1),(left['id'],2,-1),(owner,2,-.03),(owner,0,.03)],0,100000,4)
            else:
                top,bottom=sorted((a,b),key=lambda p:proposed[p['id']]['bounds'][1])
                rule('Opening clearance, vertical','clearance',[(bottom['id'],1,1),(top['id'],3,-1),(owner,3,-.03),(owner,1,.03)],0,100000,4)
    plan=dict(schema=VERSION, rules=rules, discoveries=discoveries,
              view=proposal.get('view'), symmetries=proposal.get('symmetries',[]),
              partitions=[dict(body=body['id'],cutters=[p['id'] for p in parts if p['role']=='tower'])] if cells and body else [],
              qualification='Authored family/attachment proposals; native port errors settle source-seeded parameters. Not learned truth or a final-coordinate target.')
    scaffold=dict(parts=parts)
    mapping=layout_mapping.build(observed,scaffold)
    plan['knots']=corner_knots(observed,scaffold)
    if connections:
        from .church_connection_plan import extend
        plan['connections']=extend(parts,proposed,aliases,proposal,rules,context)
        plan['openingContext']=context
        if cad:
            from .church_cad import extend as cad_relations
            plan['cad']=cad_relations(parts,proposed,aliases,proposal,rules,context,plan['connections'])
    if recovery and recovery['decisions']:
        plan['recovery']=deepcopy(recovery)
    return scaffold,mapping,plan,proposal


def corner_knots(observed, scaffold):
    """Source-supported bends may expose a missing interior construction site."""
    spans={s['id']:s for s in observed['spans']}; result=[]
    for p in scaffold['parts']:
        if p['status']=='prior-only' or p['shape'] not in ('rectangle','frame','triangle'):
            continue
        points=p['geometry']['points']; edges=p['geometry']['segments']
        if not edges and p['shape']=='frame':
            edges=[[0,1],[1,2],[2,3],[3,0]]
        for i,target in enumerate(points):
            adjacent=[points[b if a==i else a] for a,b in edges if a==i or b==i]
            if len(adjacent)!=2:
                continue
            v=[[q[k]-target[k] for k in (0,1)] for q in adjacent]
            denom=math.hypot(*v[0])*math.hypot(*v[1])
            if not denom or abs(sum(v[0][k]*v[1][k] for k in (0,1))/denom)>.85:
                continue
            for ident in p['spans']:
                s=spans[ident]; controls=s['controls']
                if len(controls)<3:
                    continue
                samples=[layout_mapping.point(controls,j/100) for j in range(101)]
                j=min(range(8,93),key=lambda j:math.dist(samples[j],target))
                scale=min(p['bounds'][2]-p['bounds'][0],p['bounds'][3]-p['bounds'][1])
                a=samples[j-7]; b=samples[j]; c=samples[j+7]
                u=[b[k]-a[k] for k in (0,1)]; v=[c[k]-b[k] for k in (0,1)]
                den=math.hypot(*u)*math.hypot(*v)
                if den and sum(u[k]*v[k] for k in (0,1))/den < .82 and math.dist(b,target)<max(4,.15*scale):
                    result.append(dict(span=ident,t=j/100,part=p['id'],point=i,
                                       basis='Acquired fitted bend near a selected angular construction; no source interval deleted.'))
    unique={}
    for row in result:
        unique.setdefault((row['span'],round(row['t'],1)),row)
    return list(unique.values())[:32]
