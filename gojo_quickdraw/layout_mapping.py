"""Non-exclusive, finite correspondence from fitted ink intervals to scaffolds.

The fitted-profile parameter is not acquisition time. Ranges select existing
profile witnesses; expected geometry and its uncovered complement stay separate.
"""
import math

QUANTUM = 10000
BINS = 40
UNRESOLVED_COLOR = '#a65368'
SHARED_COLOR = '#98712b'


def point(controls, t):
    row=[list(p) for p in controls]
    while len(row)>1:row=[[(1-t)*a[k]+t*b[k] for k in (0,1)] for a,b in zip(row,row[1:])]
    return row[0]


def _project(p,a,b):
    dx,dy=b[0]-a[0],b[1]-a[1]; size=dx*dx+dy*dy
    u=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/size)) if size else 0
    return math.hypot(p[0]-a[0]-u*dx,p[1]-a[1]-u*dy),u


def groups(part):
    geo=part['geometry']; edges=geo['segments']; points=geo['points']
    if 'boundaryGroups' in geo:return geo['boundaryGroups']
    pending=set(range(len(edges))); result=[]
    while pending:
        i=min(pending); pending.remove(i); chosen=[i]; a,b=(points[k] for k in edges[i])
        dx,dy=b[0]-a[0],b[1]-a[1]
        while True:
            last=edges[chosen[-1]][1]
            next_edge=next((j for j in sorted(pending) if edges[j][0]==last and
                abs(dx*(points[edges[j][1]][1]-b[1])-dy*(points[edges[j][1]][0]-b[0]))<.01),None)
            if next_edge is None:break
            pending.remove(next_edge);chosen.append(next_edge)
        l,t,r,d=part['bounds']
        if abs(dy)<.01:
            name='reference level' if t==d else 'top boundary' if abs(a[1]-t)<.01 else 'base boundary'
        elif abs(dx)<.01:name='left boundary' if abs(a[0]-l)<.01 else 'right boundary'
        else:name='sloping boundary'
        result.append({'name':name,'segments':chosen})
    return result


def _merge(ranges):
    rows=[]
    for a,b in sorted((min(a,b),max(a,b)) for a,b in ranges if a!=b):
        if rows and a<=rows[-1][1]:rows[-1][1]=max(b,rows[-1][1])
        else:rows.append([a,b])
    return rows


def _complement(ranges):
    result=[];last=0
    for a,b in _merge(ranges):
        if a>last:result.append([last,a])
        last=b
    if last<QUANTUM:result.append([last,QUANTUM])
    return result


def _ticks(value):return min(QUANTUM,max(0,round(value*QUANTUM)))


def build(observed, layout):
    spans=observed.get('spans',[]); parts=layout.get('parts',[])
    all_points=[p for s in observed.get('strokes',[]) for p in s['points']]
    scale=max(1,min(max(p[0] for p in all_points)-min(p[0] for p in all_points),
                    max(p[1] for p in all_points)-min(p[1] for p in all_points))) if all_points else 1
    samples={s['id']:[point(s['controls'],i/BINS) for i in range(BINS+1)] for s in spans}
    by_span={s['id']:s for s in spans}; targets=[]
    for part in parts:
        geo=part['geometry']; seed=set(part['spans']); role=part['role']
        seed_orders=[by_span[s]['strokeIndex'] for s in seed if s in by_span]
        l,t,r,b=part['bounds']; allowance=max(5,.025*scale+.08*min(r-l,max(1,b-t)))
        for ordinal,group in enumerate(groups(part)):
            edges=[(geo['points'][geo['segments'][i][0]],geo['points'][geo['segments'][i][1]]) for i in group['segments']]
            lengths=[math.dist(a,b) for a,b in edges]; total=max(.001,sum(lengths)); offsets=[]; offset=0
            for length in lengths:offsets.append(offset);offset+=length
            def project(p):
                distance,j,u=min((_project(p,a,b)[0],j,_project(p,a,b)[1]) for j,(a,b) in enumerate(edges))
                return distance,j,(offsets[j]+u*lengths[j])/total
            prior=group.get('priorOnly',False) or role in ('ground','cross-level') or part['status']=='prior-only'
            if role=='cross' and part.get('missingHorizontalArm') and group['name']=='crossbar':prior=True
            target={'id':f"mapping-{part['id']}-{ordinal}",'part':part['id'],'name':group['name'],
                    'segments':group['segments'],'ink':[],'endpointSupport':[],
                    'relatedParts':list(dict.fromkeys([part['id'],*part['participants']])),
                    'reason':'Expected construction; no drawn interval is claimed.' if prior else 'Local boundary correspondence proposal.'}
            if not prior:
                for span in spans:
                    if role in ('cross','door','window') and span['id'] not in seed:continue
                    pts=samples[span['id']]; hits=[]
                    order_gap=min((abs(span['strokeIndex']-i) for i in seed_orders),default=0)
                    for k,(p,q) in enumerate(zip(pts,pts[1:])):
                        mid=[(p[i]+q[i])/2 for i in (0,1)]; dist,j,u=project(mid)
                        a,c=edges[j]; dx,dy=q[0]-p[0],q[1]-p[1]; ex,ey=c[0]-a[0],c[1]-a[1]
                        denom=math.hypot(dx,dy)*math.hypot(ex,ey)
                        alignment=abs(dx*ex+dy*ey)/denom if denom else 0
                        cost=dist/allowance+1.4*(1-alignment)+.025*min(order_gap,5)
                        hits.append((k,dist,alignment,cost) if alignment>.78 and cost<1.25 else None)
                    runs=[]; current=[]
                    for hit in hits+[None]:
                        if hit is not None:current.append(hit)
                        elif current:runs.append(current);current=[]
                    for run in runs:
                        start,end=run[0][0],run[-1][0]+1
                        travel=sum(math.dist(pts[k],pts[k+1]) for k in range(start,end))
                        if travel<max(3,.035*total):continue
                        ta,tb=project(pts[start])[2],project(pts[end])[2]
                        if abs(ta-tb)<.003:continue
                        target['ink'].append({'span':span['id'],'from':start/BINS,'to':end/BINS,
                            'targetFrom':_ticks(ta)/QUANTUM,'targetTo':_ticks(tb)/QUANTUM,
                            'cost':sum(h[3] for h in run)/len(run),
                            'features':{'meanDistance':sum(h[1] for h in run)/len(run),
                                        'alignment':sum(h[2] for h in run)/len(run),
                                        'strokeOrderGap':order_gap,'allowance':allowance},
                            'sourceOrder':{'stroke':span['strokeIndex'],'startIndex':span.get('startIndex'),
                                           'endIndex':span.get('endIndex')},
                            'basis':'fitted profile parameter; not an exact sample or time interval'})
            target['targetRemainder']=[[a/QUANTUM,b/QUANTUM] for a,b in _complement(
                [(_ticks(m['targetFrom']),_ticks(m['targetTo'])) for m in target['ink']])]
            if not target['ink'] and role not in ('cross','cross-level'):
                # Endpoints can explain a support level or a missing boundary;
                # they never become evidence that an intervening line was drawn.
                endpoints=[]
                for span in spans:
                    if role!='ground' and span['id'] not in seed:continue
                    a,c=span['controls'][0],span['controls'][-1]
                    for at,p in ((0,a),(1,c)):
                        dist=project(p)[0]
                        if role=='ground' and (abs(a[1]-c[1])<.17*scale or p[1]<part['bounds'][1]-.10*scale):continue
                        if dist<.09*scale:endpoints.append((dist,span['id'],at))
                target['endpointSupport']=[{'span':sid,'at':at} for _,sid,at in sorted(endpoints)[:4]]
            target['status']='ink-supported' if target['ink'] else 'endpoint-inference' if target['endpointSupport'] else 'expectation-prior'
            if target['ink'] and target['targetRemainder']:target['status']='ink-and-completion'
            if role=='ground':target['reason']='Support level inferred from lower wall endpoints and the building premise; not a detected ground stroke.'
            if role=='cross-level':target['reason']='Reference level through the proposed cross; not acquired ink.'
            targets.append(target)
    source_rows=[]
    for span in spans:
        selected=[(target,m) for target in targets for m in target['ink'] if m['span']==span['id']]
        breaks=sorted({0,QUANTUM,*[_ticks(m[k]) for _,m in selected for k in ('from','to')]})
        for a,b in zip(breaks,breaks[1:]):
            owners=sorted({target['part'] for target,m in selected if _ticks(m['from'])<=a and _ticks(m['to'])>=b})
            ids=sorted({target['id'] for target,m in selected if _ticks(m['from'])<=a and _ticks(m['to'])>=b})
            source_rows.append({'span':span['id'],'from':a/QUANTUM,'to':b/QUANTUM,'parts':owners,'targets':ids,
                'status':'shared-support' if len(owners)>1 else 'mapped' if owners else 'unresolved',
                'reason':'Several proposed boundaries select the same ink; one acquisition cause.' if len(owners)>1 else
                         'Selected boundary correspondence.' if owners else 'No selected boundary explains this interval; meaning remains open.'})
    missing=[]
    for stroke in observed.get('strokes',[]):
        covered={i for s in spans if s['strokeIndex']==stroke['index'] and 'startIndex' in s for i in range(s['startIndex'],s['endIndex']+1)}
        missing.extend({'stroke':stroke['index'],'sample':i} for i in range(len(stroke['points'])) if i not in covered)
    return {'schema':'gojo.quickdraw-boundary-mapping.v1','sourceId':observed.get('sourceId'),
        'targets':targets,'sourceCoverage':source_rows,'unprofiledSamples':missing,
        'policy':{'version':'boundary-correspondence.v1','parameterQuantum':QUANTUM,'profileBins':BINS,'learned':False,
                  'meaning':'Non-draining correspondence hypotheses. Shape and direction are primary; stroke-order separation is a small cost, not an identity rule.'},
        'summary':{'targets':len(targets),'mappedIntervals':sum(len(t['ink']) for t in targets),
                   'sharedSourceRanges':sum(r['status']=='shared-support' for r in source_rows),
                   'unresolvedSourceRanges':sum(r['status']=='unresolved' for r in source_rows),
                   'unprofiledSamples':len(missing),'priorTargets':sum(t['status']=='expectation-prior' for t in targets)},
        'qualification':'Authored finite association, not ground-truth ownership, calibrated confidence or additional independent evidence.'}
