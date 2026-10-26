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


def placement(cross, parent, earlier_parent, scale, observed=None):
    width,height=dimensions(cross,scale)
    a,pt,b,base=parent['bounds'];oa,ot,ob,og=earlier_parent['bounds']
    points=cross['geometry']['points'];cx=(a+b)/2
    old_x,old_bar=points[12]
    x=a+(old_x-oa)*(b-a)/max(.001,ob-oa)
    # Small offsets can become centered; strongly offset motifs retain most
    # of their displacement instead of being moved wholesale to the apex.
    x=cx if abs(x-cx)<=.20*(b-a) else x+.35*(cx-x)
    old_fraction=(old_bar-cross['bounds'][1])/max(.001,cross['bounds'][3]-cross['bounds'][1])
    fraction=_clamp(1-old_fraction if old_fraction>.5 else old_fraction,.24,.44)
    reason=['Cross keeps its relative prominence; only excess above the broad building scale is softened.']
    if abs(fraction-old_fraction)>.001:
        reason.append('Church prior puts the crossbar above halfway; the original bar remains in the source evidence.')
    support_read={'basis':'face-relative' if cross.get('placement','').startswith('within') else 'offset-support',
                  'earlierSupport':earlier_parent['id'],'earlierFoot':points[14], 'endpointReads':[]}
    if cross.get('placement','').startswith('within'):
        previous_bar=pt+(old_bar-ot)*(base-pt)/max(.001,og-ot)
        top=previous_bar-_clamp(old_fraction,.24,.55)*height
        bar=top+fraction*height
        mode='on-face'
        reason.append('Face-relative position retained, including a motif spanning adjoining roof and wall areas.')
    else:
        peak=earlier_parent.get('apex') or [(oa+ob)/2,ot]
        cap=parent['shape'] in ('triangle','dome')
        near_axis=abs(old_x-peak[0])<=max(.18*(ob-oa),.08*scale)
        near_foot=abs(points[14][1]-peak[1])<=.10*scale
        # A regional cross envelope can stop short of its actual upright.
        # Read only endpoints of this cross's selected intervals, without
        # converting that observed proximity into acquired site identity.
        for span in (observed or {}).get('spans',[]):
            if span['id'] not in cross.get('spans',[]):continue
            for index in (0,len(span['controls'])-1):
                point=span['controls'][index]
                if abs(point[0]-peak[0])<=.08*scale and abs(point[1]-peak[1])<=.08*scale:
                    support_read['endpointReads'].append({'span':span['id'],'control':index,'point':list(point)})
        at_peak=cap and (cross['status']=='prior-only' or near_axis and (near_foot or support_read['endpointReads']))
        if at_peak:
            x=cx;support_read.update(basis='peak-attachment',earlierPeak=list(peak))
            reason.append('Selected peak attachment follows the revised peak, including a short gap in the earlier envelope.')
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
        gap=0 if at_peak or gap<=.10*scale or cross['status']=='prior-only' else .65*gap
        top=support-gap-height;bar=top+fraction*height
        mode='attached-top' if not gap else 'above-detached'
        reason.append('Cross follows its selected support; substantial drawn offsets remain visible.')
    outline=cross.get('outlineRead') or {}
    factor=height/max(.001,cross['bounds'][3]-cross['bounds'][1])
    stem=_clamp(outline['stemWidth']*factor if 'stemWidth' in outline else .12*height,.10*height,.30*width)
    thickness=_clamp(outline['barThickness']*factor if 'barThickness' in outline else stem,.09*height,.24*height)
    return [x-width/2,top,x+width/2,top+height],dict(placement=mode,bar=bar,
        supportRead=support_read,stemWidth=stem,barThickness=thickness,
        outlined=cross.get('markerBasis')=='outline-contour-read',reason=reason)
