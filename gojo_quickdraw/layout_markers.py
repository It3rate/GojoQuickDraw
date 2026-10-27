"""Independent upper markers, with explicit partial-cross assumptions."""
from __future__ import annotations
import math


def markers(observed, roofs, building, scene, maximum=6, outlines=None, excluded_spans=()):
    sh=scene[3]-scene[1]; sw=scene[2]-scene[0]
    if outlines is None:
        from .layout_outlines import crosses
        outlines=crosses(observed,scene)
    excluded_spans=set(excluded_spans)
    hints=[hint for hint in outlines if not set(hint['spans'])<=excluded_spans]
    for hint in observed.get('crosses',[]):
        if set(hint['spans'])<=excluded_spans:continue
        if hint['point'][1]>scene[1]+.68*sh: continue
        if any(set(hint['spans']) & set(h['spans']) for h in outlines):continue
        if any(math.dist(hint['point'],h['point'])<.055*min(sw,sh) or set(hint['spans'])==set(h['spans']) for h in hints): continue
        hints.append({**hint,'markerBasis':'crossing-read'})
    selected={sid for h in hints for sid in h['spans']}
    # A lone upright above a cap can support a partial cross hypothesis. Its
    # missing arm remains assumed; a diagonal roof tip is not this evidence.
    for span in observed['spans']:
        if span['id'] in selected or span['id'] in excluded_spans: continue
        a,b=span['controls'][0],span['controls'][-1]; top,bottom=sorted((a[1],b[1]))
        height=bottom-top; x=(a[0]+b[0])/2
        if not .065*sh<height<.4*sh or abs(a[0]-b[0])>.2*height: continue
        nearby=[roof for roof in roofs if abs(bottom-roof['bounds'][1])<.075*sh and
                top<roof['bounds'][1]-.05*sh and abs(x-roof['apex'][0])<.28*(roof['bounds'][2]-roof['bounds'][0])]
        point=[x,top+.32*height]
        if nearby and not any(math.dist(point,h['point'])<.08*sh for h in hints):
            hints.append({'point':point,'spans':[span['id']],'markerBasis':'upright-marker-prior',
                          'missingHorizontalArm':True})
    if not hints:
        roof=min(roofs,key=lambda r:r['bounds'][1]) if roofs else {'bounds':building,'apex':[(building[0]+building[2])/2,building[1]]}
        hints=[{'point':[roof['apex'][0],roof['bounds'][1]-.0816*sh], 'spans':[], 'markerBasis':'prior-only'}]
    hints.sort(key=lambda h:(h['point'][0],h['point'][1]))
    withheld=hints[maximum:];hints=hints[:maximum]
    for hint in hints:
        x,y=hint['point']
        parents=[(i,r) for i,r in enumerate(roofs) if y<=r['bounds'][3]+.03*sh and
                 r['bounds'][0]-.2*(r['bounds'][2]-r['bounds'][0])<=x<=r['bounds'][2]+.2*(r['bounds'][2]-r['bounds'][0])]
        hint['roofIndex']=min(parents,key=lambda ir:abs(x-ir[1]['apex'][0])+.3*abs(y-ir[1]['bounds'][1]))[0] if parents else None
    return hints,{'withheldMarkers':withheld,'capacityReached':bool(withheld)}


def geometry(observed, hint, parent, scene):
    """Ideal Latin cross; local parent shape limits only a proposed marker."""
    sh=scene[3]-scene[1]; l,top,r,base=parent['bounds']; cx=(l+r)/2
    support=[s for s in observed['spans'] if s['id'] in hint['spans']]
    positions=[p for s in support for p in s['controls']]
    height=max(p[1] for p in positions)-min(p[1] for p in positions) if positions else .12*sh
    if 'outlineBounds' in hint:
        a,t,b,d=hint['outlineBounds']; x,bar=hint['point'];height=d-t
        return cross_geometry(x,t,height,(b-a)/2,hint['stemWidth']/2,hint['barThickness']/2,bar,outline=True),\
            [a,t,b,d],('above-' if bar<top else 'within-')+parent.get('role','building')
    height=max(.06*sh,min(.27*sh,height));x,bar=hint['point']
    above=bar<top; shape=parent.get('shape','rectangle')
    if above:
        height=min(height,max(.06*sh,.8*(top-scene[1]))) if support else height
        y=min(bar-.32*height,top-height)
        x=max(l+.06*(r-l),min(r-.06*(r-l),x))
    else:
        height=min(height,.48*(base-top)); y=max(top+.18*(base-top),min(base-height-.015*sh,bar-.32*height))
        for _ in range(16):
            y=max(top+.18*(base-top),min(base-height-.01*sh,y));lo,hi=l,r
            for dx,dy in [(-.06*height,0),(.06*height,0),(-.30*height,.26*height),(.30*height,.26*height),
                          (-.30*height,.38*height),(.30*height,.38*height),(-.06*height,height),(.06*height,height)]:
                if shape=='triangle':
                    apex=parent['apex'][0];f=(y+dy-top)/(base-top)
                    left,right=apex+(l-apex)*f,apex+(r-apex)*f
                elif shape=='dome':
                    reach=(r-l)/2*math.sqrt(max(0,1-((base-y-dy)/(base-top))**2))
                    left,right=cx-reach,cx+reach
                else:left,right=l,r
                lo=max(lo,left-dx);hi=min(hi,right-dx)
            if lo+.004<hi:
                x=max(lo+.002,min(hi-.002,x));break
            height*=.8
    x,y,height=round(x,3),round(y,3),round(height,3)
    return cross_geometry(x,y,height),[x-.30*height,y,x+.30*height,y+height],('above-' if above else 'within-')+parent.get('role','building' if shape=='rectangle' else 'roof')


def cross_geometry(x,y,height,arm=None,k=None,bar_half=None,bar=None,outline=False):
    bar=round(y+.32*height,3) if bar is None else bar
    k=.06*height if k is None else k;arm=.30*height if arm is None else arm
    half=k if bar_half is None else bar_half;bottom=y+height
    points=[[x-k,y],[x+k,y],[x+k,bar-half],[x+arm,bar-half],[x+arm,bar+half],[x+k,bar+half],
            [x+k,bottom],[x-k,bottom],[x-k,bar+half],[x-arm,bar+half],[x-arm,bar-half],[x-k,bar-half]]
    points=[[round(a,3),round(b,3)] for a,b in points]+[[x,bar]]
    points += [[x,y],[x,round(bottom,3)],[round(x-arm,3),bar],[round(x+arm,3),bar]]
    return {'points':points,'segments':[[i,(i+1)%12] for i in range(12)]+[[13,14],[15,16]],
            'fillIndices':list(range(12)), 'boundaryGroups':[
                {'name':'upright','segments':[12],'priorOnly':outline}, {'name':'crossbar','segments':[13],'priorOnly':outline},
                {'name':'outline thickness','segments':list(range(12)),'priorOnly':not outline}]}
