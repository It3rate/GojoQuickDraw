"""Source-supported opening reads; costs are authored, not learned probabilities."""
import math


def distance(point, a, b):
    dx,dy=b[0]-a[0],b[1]-a[1]; length=dx*dx+dy*dy
    u=max(0,min(1,((point[0]-a[0])*dx+(point[1]-a[1])*dy)/length)) if length else 0
    return math.hypot(point[0]-a[0]-u*dx,point[1]-a[1]-u*dy)


def outline(bounds, arch_height=0):
    l,t,r,b=bounds
    if not arch_height:return [[l,b],[l,t],[r,t],[r,b]]
    return [[l,b]]+[[round((l+r)/2-(r-l)/2*math.cos(i*math.pi/24),3),
                     round(t+arch_height-arch_height*math.sin(i*math.pi/24),3)] for i in range(25)]+[[r,b]]


def ellipse(bounds):
    l,t,r,b=bounds
    return [[round((l+r)/2+(r-l)/2*math.cos(i*math.tau/32),3),
             round((t+b)/2+(b-t)/2*math.sin(i*math.tau/32),3)] for i in range(32)]


def source_paths(observed, ids):
    """Read selected acquired intervals, never every point on a shared stroke."""
    ids=set(ids); by_id={s['id']:s for s in observed['spans']}; paths=[]
    for stroke in observed['strokes']:
        selected=[by_id[i] for i in stroke.get('spans',[]) if i in ids and i in by_id]
        if not selected:continue
        if len(selected)==len(stroke.get('spans',[])):
            paths.append(stroke['points']);continue
        for span in selected:
            if 'startIndex' in span and 'endIndex' in span:
                paths.append(stroke['points'][span['startIndex']:span['endIndex']+1])
            else:paths.append(span['controls'])
    return paths


def _sample(path, step):
    result=[]
    for a,b in zip(path,path[1:]):
        count=max(1,math.ceil(math.dist(a,b)/step))
        result.extend([[a[k]+(b[k]-a[k])*(i+.5)/count for k in (0,1)] for i in range(count)])
    return result


def classify(observed, region, *, rectangle_only=False):
    l,t,r,b=region['bounds']; width,height=r-l,b-t; scale=max(1,min(width,height))
    paths=source_paths(observed,region['spans'])
    edges=[(a,c) for path in paths for a,c in zip(path,path[1:])]
    points=[p for path in paths for p in _sample(path,max(1,scale/16))]
    def read(path, closed=True):
        trace=path+[path[0]] if closed else path
        model=list(zip(trace,trace[1:]))
        errors=sorted(min(distance(p,a,c) for a,c in model)/scale for p in points)
        # Door dividers/handles and stained-glass spokes can be interior detail.
        keep=max(1,math.ceil(len(errors)*.85))
        cost=sum(errors[:keep])/keep if errors else 1
        probes=_sample(trace,max(1,scale/12))
        coverage=sum(min((distance(p,a,c) for a,c in edges),default=scale)<.12*scale for p in probes)/max(1,len(probes))
        return cost,coverage
    rect=outline(region['bounds']); rc,rv=read(rect)
    arches=[(read(outline(region['bounds'],f*height)),f) for f in (.25,.4,.55)]
    (ac,av),fraction=min(arches,key=lambda item:item[0][0])
    ec,ev=read(ellipse(region['bounds']))
    round_shape=not rectangle_only and ec+.012<min(rc,ac) and ec<.09 and ev>=.76
    arch=not rectangle_only and ac+.004<rc and ac<.80*rc
    shape=('circle' if .80<width/max(1,height)<1.25 else 'ellipse') if round_shape else 'arch' if arch else 'rectangle'
    path=ellipse(region['bounds']) if round_shape else outline(region['bounds'],fraction*height if arch else 0)
    cost,closed_coverage=read(path)
    _,open_coverage=read(path,closed=False)
    support=cost<.105 and (closed_coverage>=.76 if round_shape else open_coverage>=.65)
    return {'shape':shape,'archFraction':fraction if shape=='arch' else 0,
            'shapeCosts':{'arch':ac,'rectangle':rc,'ellipse':ec},
            'closedTrace':closed_coverage>=.70,'openingSupport':support,
            'shapeReads':{'boundaryCoverage':closed_coverage,'openHeadAndSidesCoverage':open_coverage,
                          'boundaryCost':cost,'basis':'length-sampled selected source intervals; finite perimeter coverage'}}


def geometry(bounds, shape, ground_site=None):
    l,t,r,b=bounds; rounded=shape['shape'] in ('circle','ellipse')
    points=ellipse(bounds) if rounded else outline(bounds,shape.get('archFraction',.35)*(b-t) if shape['shape']=='arch' else 0)
    refs=[None]*len(points)
    if ground_site and not rounded:refs[0],refs[-1]=ground_site(l),ground_site(r)
    result={'points':points,'segments':[[i,(i+1)%len(points)] for i in range(len(points))],
            'fillIndices':list(range(len(points))),'siteSources':refs}
    if rounded:result['boundaryGroups']=[{'name':'round perimeter','segments':list(range(len(points)))}]
    elif shape['shape']=='arch':
        result['boundaryGroups']=[{'name':'left side','segments':[0]},
            {'name':'arched head','segments':list(range(1,25))},
            {'name':'right side','segments':[25]},{'name':'base','segments':[26]}]
    if shape['shape']=='rectangle' and shape.get('panePattern'):
        pane=shape['panePattern'];fx,fy=pane['fractions'];x=l+(r-l)*fx;y=t+(b-t)*fy
        # Four frame contacts and one shared center. The bars are divided at
        # that same site, so they move with this window's construction frame.
        points.extend([[l,y],[x,t],[r,y],[x,b],[x,y]])
        result['siteSources'].extend([None]*5)
        result['segments']=[[0,4],[4,1],[1,5],[5,2],[2,6],[6,3],[3,7],[7,0],[5,8],[8,7],[4,8],[8,6]]
        result['boundaryGroups']=[
            {'name':name,'segments':[2*i,2*i+1],'sourceSpans':pane['frameSpans']}
            for i,name in enumerate(('left boundary','top boundary','right boundary','base boundary'))]
        result['boundaryGroups'] += [
            {'name':'vertical window divider','segments':[8,9],'sourceSpans':pane['verticalSpans']},
            {'name':'horizontal window divider','segments':[10,11],'sourceSpans':pane['horizontalSpans']}]
    return result
