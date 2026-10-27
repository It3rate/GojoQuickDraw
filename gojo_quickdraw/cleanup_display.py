"""Presentation of the saved shape refinement; never an inference engine."""
from html import escape


def svg(packet, original_only=False):
    parts=(packet.get('cleanup') or {}).get('parts',[])
    if not parts and not original_only:return '<div class="missing">Run full analysis to add the church cleanup stage.</div>'
    strokes=packet['observation']['strokes']
    points=[p for s in strokes for p in s['points']]+[p for s in parts for p in s['geometry']['points']]
    l=min([0]+[p[0] for p in points]);t=min([0]+[p[1] for p in points]);r=max([512]+[p[0] for p in points]);b=max([512]+[p[1] for p in points]);pad=.035*max(r-l,b-t)
    image=[f'<svg viewBox="{l-pad} {t-pad} {r-l+2*pad} {b-t+2*pad}" role="img" aria-label="Church cleanup over faint original">']
    for s in strokes:
        pts=' '.join(f'{x},{y}' for x,y in s['points'])
        image.append(f'<polyline data-stroke="{escape(s["id"],quote=True)}" points="{pts}" fill="none" stroke="#465852" stroke-width="2" opacity="{1 if original_only else .20}"/>')
    for p in ([] if original_only else parts):
        geo=p['geometry'];prior=p['status']=='prior-only';added=prior and p['role'] in ('door','window','cross')
        color='#9aa2a6' if added else p['color']
        title=('building construction frame' if p['shape']=='frame' else p['role'])+' · '+('added from expectation' if prior else 'refined from area estimate')+' · '+' '.join(p['changes'])
        image.append(f'<g data-cleanup="{escape(p["id"],quote=True)}"><title>{escape(title)}</title>')
        if geo.get('fillIndices'):
            pts=' '.join(','.join(str(x) for x in geo['points'][i]) for i in geo['fillIndices'])
            image.append(f'<polygon points="{pts}" fill="{color}" fill-opacity=".075" stroke="none"/>')
        for i in geo.get('displaySegments',range(len(geo['segments']))):
            a,b=(geo['points'][j] for j in geo['segments'][i]);dash=' stroke-dasharray="1 4"' if added else ' stroke-dasharray="5 3"' if prior or p['role']=='ground' else ''
            image.append(f'<path d="M{a[0]} {a[1]} L{b[0]} {b[1]}" stroke="{color}" stroke-width="2.8" stroke-linecap="round" fill="none"{dash}/>')
        image.append('</g>')
    warnings=[] if original_only else [c['label'] for c in (packet.get('cleanup') or {}).get('checks',[]) if c['status']=='needs-review']
    note='<div style="color:#99621b;font-size:.85em">Review: '+escape(' · '.join(warnings))+'</div>' if warnings else ''
    return ''.join(image)+'</svg>'+note


SCRIPT=r'''
function isCleanup(){return $('readMode').value==='cleanup'}
function gatherCleanup(){entries=[];for(const row of packet().cleanup.parts||[])entries.push({group:'parts',row:{...row,label:(row.shape==='frame'?'building construction frame':row.role)+' · '+(row.status==='prior-only'?'added from expectation':'refined shape')}});for(const row of packet().cleanup.relations||[])entries.push({group:'contextRelations',row});for(const row of packet().cleanup.checks||[])entries.push({group:'contextRelations',row:{...row,label:row.label+' · '+(row.status==='needs-review'?'review warning':'within allowance')}});entries.forEach((e,i)=>e.key=String(i))}
function renderCleanup(){
 const raw=$('raw'),fit=$('fit'),p=packet(),parts=p.cleanup.parts||[];raw.replaceChildren();fit.replaceChildren();
 const points=[...p.observation.strokes.flatMap(s=>s.points),...parts.flatMap(s=>s.geometry.points)],xs=points.map(p=>p[0]),ys=points.map(p=>p[1]),l=Math.min(0,...xs),t=Math.min(0,...ys),r=Math.max(512,...xs),b=Math.max(512,...ys),pad=.035*Math.max(r-l,b-t);
 for(const svg of [raw,fit]){svg.setAttribute('viewBox',[l-pad,t-pad,r-l+pad*2,b-t+pad*2].join(' '));for(const s of p.observation.strokes)node('polyline',{'data-stroke':s.id,points:s.points.map(p=>p.join(',')).join(' '),fill:'none',stroke:'#465852','stroke-width':2.1,opacity:svg===fit?.20:1,'pointer-events':'none'},svg)}
 for(const e of entries.filter(e=>e.row.geometry)){const s=e.row,g=s.geometry,added=s.status==='prior-only'&&['door','window','cross'].includes(s.role),color=added?'#9aa2a6':s.color,host=node('g',{'data-cleanup':s.id},fit);text(host,'title',s.role+' · '+s.status+' · '+s.changes.join(' '));if(g.fillIndices)node('polygon',{points:g.fillIndices.map(i=>g.points[i].join(',')).join(' '),fill:color,'fill-opacity':.075,stroke:'none'},host);for(const i of g.displaySegments||g.segments.map((_,i)=>i)){const [a,b]=g.segments[i];node('line',{x1:g.points[a][0],y1:g.points[a][1],x2:g.points[b][0],y2:g.points[b][1],stroke:color,'stroke-width':selected===e?3.8:2.8,'stroke-dasharray':added?'1 4':s.status==='prior-only'||s.role==='ground'?'5 3':'none','stroke-linecap':'round'},host)}bind(host,e)}
 const warnings=(p.cleanup.checks||[]).filter(c=>c.status==='needs-review');
 $('objectPanel').hidden=true;$('fitTitle').textContent='Church cleanup · shape layer';$('fitStats').textContent=parts.length?parts.length+' shapes · '+(p.cleanup.summary?.priorOnly??0)+' from expectations · '+(p.cleanup.view?.selected||'open view')+(warnings.length?' · Review: '+warnings.map(c=>c.label).join(', '):''):'No building area to refine · original evidence retained';$('modeNote').textContent='Faint ink = original · gray dots = added doors/windows/crosses · colored solid = refined estimate · other dashes = expectation or ground';$('strokeValue').textContent='All';list();if(selected)detail(selected)
}
function cleanupDetail(e){const box=$('detail');box.replaceChildren();const s=e.row;text(box,'span',s.status==='needs-review'?'Review warning':s.nativeRead?'Consistency check':s.status==='prior-only'?'Added from expectation':'Refined shape proposal','badge seed');text(box,'h3',label(e),'details-title');if(s.shape==='frame')text(box,'p','Reference bounds only. The adjoining wall and tower rectangles carry the visible surfaces.');text(box,'p',s.qualification||'An explicit construction dependency.','subtle');for(const change of s.changes||[])text(box,'p',change);if(s.kind==='mirror-check')text(box,'p','Signed mirror differences: '+s.signedResiduals.join(', ')+'. Authored allowance: '+s.allowance+'.');if(s.kind==='clearance-check')text(box,'p','Opening gap: '+s.actual+'. Requested minimum: '+s.minimum+'. Unmet gap: '+s.shortfall+'.');if(s.sourcePart)text(box,'p','Earlier estimate: '+s.sourcePart,'subtle');objectLinks(box,s.participants,'Construction references');if(s.sourceStrokeOrder?.length)text(box,'p','Supporting pen strokes: '+s.sourceStrokeOrder.join(', ')+'. Pen order is retained as evidence; construction order follows the selected attachments.','subtle');const contacts=packet().cleanup.attachments.filter(a=>a.child===s.id||a.parent===s.id);if(contacts.length)text(box,'p',contacts.length+' explicit shared attachment sites.');text(box,'p','This proposed geometry has no newly observed ink. Its earlier source mapping remains available in Source ↔ shape mapping.','subtle');const full=document.createElement('details');box.append(full);text(full,'summary','Complete refinement record');text(full,'pre',JSON.stringify(s,null,2))}
'''
