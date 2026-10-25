"""Upper rectangles need no triangular cap or drawn separating baseline."""
def regions(observed, building, scene, caps, excluded, maximum=6):
    l,t,r,g=building; sw,sh=scene[2]-scene[0],scene[3]-scene[1]
    uprights=[]
    for span in observed['spans']:
        if span['id'] in excluded:continue
        p,q=sorted((span['controls'][0],span['controls'][-1]),key=lambda p:p[1])
        h=q[1]-p[1]
        if h>.10*sh and abs(p[0]-q[0])<.35*h and p[1]<t-.07*sh and q[1]>t-.14*sh:
            uprights.append((span,p,q))
    candidates=[]
    for sa,a0,a1 in uprights:
        for sb,b0,b1 in uprights:
            width=b0[0]-a0[0]
            if not .07*sw<width<.65*sw or abs(a0[1]-b0[1])>.08*sh:continue
            if min(a1[1],b1[1])-max(a0[1],b0[1])<.09*sh:continue
            top=(a0[1]+b0[1])/2
            bridges=[]
            for s in observed['spans']:
                if s['id'] in excluded:continue
                p,q=s['controls'][0],s['controls'][-1]; dx=abs(p[0]-q[0])
                if (dx>.5*width and abs(p[1]-q[1])<.32*dx and abs((p[1]+q[1])/2-top)<.06*sh and
                    min(p[0],q[0])<a0[0]+.25*width and max(p[0],q[0])>b0[0]-.25*width):bridges.append(s['id'])
            if not bridges:continue
            left,right=round(a0[0],3),round(b0[0],3)
            if any(max(0,min(right,c['bounds'][2])-max(left,c['bounds'][0]))>.55*min(width,c['bounds'][2]-c['bounds'][0]) for c in caps):continue
            bottom=g if min(a1[1],b1[1])>g-.16*sh else t
            if right<l or left>r:continue
            cost=abs(a0[1]-b0[1])/sh+.05*abs(sa['strokeIndex']-sb['strokeIndex'])
            candidates.append({'bounds':[left,round(top,3),right,bottom],
                'spans':sorted({sa['id'],sb['id'],*bridges}),'cost':cost,'lowerBoundary':'inferred-level'})
    selected=[]
    for c in sorted(candidates,key=lambda c:c['cost']):
        a,_,b,_=c['bounds']
        if any(max(0,min(b,p['bounds'][2])-max(a,p['bounds'][0]))>.5*min(b-a,p['bounds'][2]-p['bounds'][0]) for p in selected):continue
        selected.append(c)
    return selected[:maximum],{'withheldRegions':selected[maximum:], 'candidateCount':len(candidates)}
