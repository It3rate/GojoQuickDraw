"""Finite reads for optional opening outlines; no source points are changed."""
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


def classify(observed, region):
    ids=set(region['spans']); l,t,r,b=region['bounds']; height=b-t
    selected=[s for s in observed['strokes'] if ids & set(s.get('spans',[]))]
    points=[p for s in selected for p in s['points']]
    def cost(arch):
        path=outline([l,t,r,b],arch)
        return sum(min(distance(p,a,c) for a,c in zip(path,path[1:])) for p in points)/max(1,len(points))/max(1,r-l)
    rect=cost(0); alternatives=[(cost(f*height),f) for f in (.25,.4,.55)]
    curved,fraction=min(alternatives)
    upper=[p[0] for p in points if p[1]<=t+.10*height]
    crown=(sum(upper)/max(1,len(upper))-l)/max(1,r-l)
    # Compare whole U outlines. A nearly flat/curved beam still reads rectangle.
    arch=curved+.005<rect and curved<.95*rect and .2<crown<.8
    closed=any(math.dist(s['points'][0],s['points'][-1])<.16*min(r-l,height) and
               max(p[0] for p in s['points'])-min(p[0] for p in s['points'])>.7*(r-l) and
               max(p[1] for p in s['points'])-min(p[1] for p in s['points'])>.7*height for s in selected)
    return {'shape':'arch' if arch else 'rectangle','archFraction':fraction if arch else 0,
            'shapeCosts':{'arch':curved,'rectangle':rect},'closedTrace':closed,
            'shapeReads':{'upperCrownFraction':crown,'basis':'upper tenth of source opening extent'}}


def geometry(bounds, shape, ground_site=None):
    l,t,r,b=bounds
    points=outline(bounds,shape['archFraction']*(b-t) if shape['shape']=='arch' else 0)
    refs=[None]*len(points)
    if ground_site:refs[0],refs[-1]=ground_site(l),ground_site(r)
    result={'points':points,'segments':[[i,(i+1)%len(points)] for i in range(len(points))],
            'fillIndices':list(range(len(points))),'siteSources':refs}
    if shape['shape']=='arch':
        result['boundaryGroups']=[{'name':'left side','segments':[0]},
            {'name':'arched head','segments':list(range(1,25))},
            {'name':'right side','segments':[25]},{'name':'base','segments':[26]}]
    return result
