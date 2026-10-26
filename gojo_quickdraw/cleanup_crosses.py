"""Conservative cross refinement in the selected support frame.

These authored placements become ordinary before/after site correspondences
and shared contacts on import. They do not add source ink or learned support.
"""
from .layout import _clamp


def dimensions(cross, scene_scale):
    a,t,b,d=cross['bounds']; height=d-t
    # A soft, increasing response: an unusually large motif stays larger than
    # a normal one. A narrow supporting tower is not a cross-size ceiling.
    knee=.60*scene_scale
    height=max(.06*scene_scale,min(height,knee)+.55*max(0,height-knee))
    aspect=.8*_clamp((b-a)/max(.001,d-t),.35,1.3)+.2*.6
    return aspect*height,height


def placement(cross, parent, earlier_parent, scale):
    width,height=dimensions(cross,scale)
    a,pt,b,base=parent['bounds'];oa,ot,ob,og=earlier_parent['bounds']
    points=cross['geometry']['points'];cx=(a+b)/2
    old_x,old_bar=points[12]
    x=a+(old_x-oa)*(b-a)/max(.001,ob-oa)
    # Small offsets can become centered; strongly offset motifs retain most
    # of their displacement instead of being moved wholesale to the apex.
    x=cx if abs(x-cx)<=.20*(b-a) else x+.35*(cx-x)
    fraction=_clamp((old_bar-cross['bounds'][1])/max(.001,cross['bounds'][3]-cross['bounds'][1]),.24,.55)
    reason=['Cross keeps its relative prominence; only excess above the broad building scale is softened.']
    if cross.get('placement','').startswith('within'):
        bar=pt+(old_bar-ot)*(base-pt)/max(.001,og-ot)
        top=bar-fraction*height
        mode='on-face'
        reason.append('Face-relative position retained, including a motif spanning adjoining roof and wall areas.')
    else:
        x=_clamp(x,a+.04*(b-a),b-.04*(b-a))
        support=pt
        if parent['shape']=='triangle':
            support=pt+(base-pt)*abs(x-cx)/max(.001,(b-a)/2)
        elif parent['shape']=='dome':
            # Use the same finite polygon as the dome builder, so the shared
            # site lies on an actual proposed edge, not an analytic lookalike.
            import math
            dome=[[cx-(b-a)/2*math.cos(i*math.pi/32),base-(base-pt)*math.sin(i*math.pi/32)] for i in range(33)]
            for u,v in zip(dome,dome[1:]):
                if u[0]<=x<=v[0]:support=u[1]+(x-u[0])*(v[1]-u[1])/(v[0]-u[0]);break
        gap=max(0,ot-cross['bounds'][3])
        gap=0 if gap<=.10*scale or cross['status']=='prior-only' else .65*gap
        top=support-gap-height;bar=top+fraction*height
        mode='attached-top' if not gap else 'above-detached'
        reason.append('Cross follows its selected support; substantial drawn offsets remain visible.')
    outline=cross.get('outlineRead') or {}
    factor=height/max(.001,cross['bounds'][3]-cross['bounds'][1])
    stem=_clamp(outline['stemWidth']*factor if 'stemWidth' in outline else .12*height,.10*height,.30*width)
    thickness=_clamp(outline['barThickness']*factor if 'barThickness' in outline else stem,.09*height,.24*height)
    return [x-width/2,top,x+width/2,top+height],dict(placement=mode,bar=bar,
        stemWidth=stem,barThickness=thickness,outlined=cross.get('markerBasis')=='outline-contour-read',reason=reason)
