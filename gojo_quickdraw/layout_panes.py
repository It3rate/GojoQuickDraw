"""A traced frame plus two interior divider runs is one opening candidate.

The intersection is a proposed shared site, not a claim that acquired points
already share identity. Frame and divider source selections remain separate.
"""
import math
from .layout_openings import classify, source_paths


def read(observed, region):
    l,t,r,b=region['bounds'];w,h=r-l,b-t
    if min(w,h)<4:return None
    vertical=[];horizontal=[]
    for span in observed['spans']:
        a,z=span['controls'][0],span['controls'][-1];dx,dy=z[0]-a[0],z[1]-a[1]
        points=[p for path in source_paths(observed,[span['id']]) for p in path]
        if not points or any(not l-.18*w<=p[0]<=r+.18*w or not t-.18*h<=p[1]<=b+.18*h for p in points):continue
        x,y=(a[0]+z[0])/2,(a[1]+z[1])/2
        if abs(dy)>.40*h and abs(dx)<.5*abs(dy) and l+.18*w<x<r-.18*w:
            if min(a[1],z[1])<t+.30*h and max(a[1],z[1])>b-.30*h:vertical.append(span)
        if abs(dx)>.40*w and abs(dy)<.60*abs(dx) and t+.18*h<y<b-.18*h:
            if min(a[0],z[0])<l+.30*w and max(a[0],z[0])>r-.30*w:horizontal.append(span)
    candidates=[]
    for v in vertical:
        for u in horizontal:
            if v['id']==u['id']:continue
            a,c=v['controls'][0],v['controls'][-1];d,e=u['controls'][0],u['controls'][-1]
            vx,vy=c[0]-a[0],c[1]-a[1];ux,uy=e[0]-d[0],e[1]-d[1];det=vx*uy-vy*ux
            if abs(det)<.001:continue
            k=((d[0]-a[0])*uy-(d[1]-a[1])*ux)/det
            q=((d[0]-a[0])*vy-(d[1]-a[1])*vx)/det
            x,y=a[0]+k*vx,a[1]+k*vy
            if not -.12<=k<=1.12 or not -.12<=q<=1.12 or not l+.16*w<x<r-.16*w or not t+.16*h<y<b-.16*h:continue
            dividers={u['id'],v['id']};frame=[sid for sid in region['spans'] if sid not in dividers]
            if not frame:continue
            fit=classify(observed,dict(region,spans=frame),rectangle_only=True)
            # Interior dividers strengthen a roughly rectangular, partly open
            # frame. They cannot turn a bare plus or a circle into a box.
            costs=fit['shapeCosts'];reads=fit['shapeReads']
            if reads['boundaryCost']>.095 or reads['boundaryCoverage']<.58:continue
            if costs['rectangle']>1.3*min(costs['arch'],costs['ellipse'])+.02:continue
            alignment=abs(vx*ux+vy*uy)/(math.hypot(vx,vy)*math.hypot(ux,uy))
            score=reads['boundaryCost']+(1-reads['boundaryCoverage'])*.15+alignment*.12+abs((x-l)/w-.5)*.1+abs((y-t)/h-.5)*.1
            candidates.append((score,{'kind':'frame-with-interior-cross','frameSpans':frame,
                'verticalSpans':[v['id']],'horizontalSpans':[u['id']],
                'sourceCrossing':[x,y],'fractions':[(x-l)/w,(y-t)/h],
                'orthogonalityResidual':alignment,'frameRead':fit,
                'qualification':'Authored composite support; closure and a shared divider junction are proposed, not acquired contact.'}))
    return min(candidates,key=lambda c:c[0])[1] if candidates else None


def enrich(observed, region):
    pattern=read(observed,region)
    if not pattern:return region
    frame=pattern['frameRead']
    return {**region,**frame,'openingSupport':True,'closedTrace':True,'panePattern':pattern,
            'spans':list(dict.fromkeys(pattern['frameSpans']+pattern['verticalSpans']+pattern['horizontalSpans'])),
            'closureBasis':'frame-and-interior-divider-support'}
