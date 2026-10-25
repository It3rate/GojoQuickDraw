"""Inspection projections only; correspondence is computed in layout_mapping."""
from html import escape
from .layout_mapping import point, SHARED_COLOR, UNRESOLVED_COLOR


def color(row, parts):
    return (UNRESOLVED_COLOR if not row['parts'] else SHARED_COLOR if len(row['parts']) > 1
            else parts[row['parts'][0]]['color'])


def overlay(packet):
    mapping=packet.get('boundaryMapping')
    if not mapping:return ''
    spans={s['id']:s for s in packet['observation']['spans']}
    parts={p['id']:p for p in packet['layout']['parts']}; result=[]
    for i,row in enumerate(mapping['sourceCoverage']):
        controls=spans[row['span']]['controls']
        points=[point(controls,row['from']+(row['to']-row['from'])*j/24) for j in range(25)]
        coords=' '.join(','.join(format(v,'.12g') for v in p) for p in points)
        label=escape(row['status']+' · '+', '.join(p.replace('layout-','') for p in row['parts']))
        result.append(f'<polyline data-mapping-range="{i}" points="{coords}" stroke="{color(row,parts)}" stroke-width="3" opacity=".85"><title>{label}</title></polyline>')
    for row in mapping['unprofiledSamples']:
        stroke=next(s for s in packet['observation']['strokes'] if s['index']==row['stroke'])
        x,y=stroke['points'][row['sample']]
        result.append(f'<circle data-unprofiled="1" cx="{x}" cy="{y}" r="3" fill="{UNRESOLVED_COLOR}"><title>Unresolved acquired sample without a fitted profile</title></circle>')
    return ''.join(result)


SCRIPT=r'''
function isMapping(){return $('readMode').value==='mapping'}
function mappingColor(r){return !r.parts.length?'#a65368':r.parts.length>1?'#98712b':packet().layout.parts.find(p=>p.id===r.parts[0])?.color||'#a65368'}
function gatherMapping(){
 const m=packet().boundaryMapping;entries=[];
 for(const row of m.targets){const p=packet().layout.parts.find(p=>p.id===row.part);entries.push({group:'mapping',row:{...row,label:p.id.replace('layout-','').replaceAll('-',' ')+' · '+row.name,spans:[...new Set(row.ink.map(i=>i.span))]}})}
 m.sourceCoverage.forEach((row,i)=>entries.push({group:row.status==='unresolved'?'unresolved':'sourceMapping',row:{...row,id:'source-range-'+i,label:row.status+' · '+row.span+' '+Math.round(100*row.from)+'–'+Math.round(100*row.to)+'%',spans:[row.span],rangeIndex:i}}));
 m.unprofiledSamples.forEach((row,i)=>entries.push({group:'unresolved',row:{...row,id:'unprofiled-'+i,label:'Unresolved sample · stroke '+row.stroke+' / point '+row.sample,unprofiled:true}}));
 entries.forEach((e,i)=>e.key=String(i));
}
function mappingDetail(e){
 const box=$('detail'),r=e.row;box.replaceChildren();text(box,'span',e.group==='mapping'?'Boundary association hypothesis':'Source coverage','badge seed');text(box,'h3',label(e),'details-title');text(box,'p',r.reason||'No selected boundary explains this acquired sample.','subtle');
 if(e.group==='mapping'){
  text(box,'p',r.status.replaceAll('-',' '));
  text(box,'p',r.ink.length+' drawn interval(s) · '+r.endpointSupport.length+' endpoint reference(s)');
  for(const m of r.ink){text(box,'h4',m.span+' · '+Math.round(100*m.from)+'–'+Math.round(100*m.to)+'%');text(box,'p','Stroke '+m.sourceOrder.stroke+' · acquired samples '+m.sourceOrder.startIndex+' → '+m.sourceOrder.endIndex,'subtle');text(box,'p','Distance '+readable(m.features.meanDistance)+' / allowance '+readable(m.features.allowance)+' · direction alignment '+readable(m.features.alignment)+' · stroke-order separation '+m.features.strokeOrderGap,'subtle');text(box,'p','Association cost '+readable(m.cost)+' (heuristic, not confidence).','subtle')}
  if(!r.ink.length)text(box,'p','No physical line is claimed for this boundary.');
  if(r.endpointSupport.length){text(box,'h4','Indirect support');for(const p of r.endpointSupport)text(box,'p',p.span+' · '+(p.at===0?'start':'end')+' endpoint; this supports placement, not a drawn connecting line.','subtle')}
  if(r.targetRemainder.length){text(box,'h4','Completion from expectations');text(box,'p',r.targetRemainder.map(([a,b])=>Math.round(100*a)+'–'+Math.round(100*b)+'%').join(', ')+' of the proposed boundary has no assigned line support.','subtle')}
  text(box,'h4','Related construction');for(const id of r.relatedParts)text(box,'p',id.replace('layout-','').replaceAll('-',' '),'subtle');
 }else if(r.targets?.length){text(box,'h4','Selected boundaries');for(const id of r.targets){const target=entries.find(e=>e.group==='mapping'&&e.row.id===id);if(target){const b=text(box,'button',label(target));b.style.margin='3px';b.onclick=()=>choose(target)}}if(r.parts.length>1)text(box,'p','Shared support: several boundaries select one source interval. This is one acquisition cause.','subtle')}
 text(box,'p','Ranges are positions along a fitted profile, not exact sample or time intervals. The source stroke, its ordered samples and any supplied timestamps remain intact.','subtle');
 const full=document.createElement('details');box.append(full);text(full,'summary','Complete mapping record');text(full,'pre',JSON.stringify(r,null,2));
}
function mappingHighlight(e){
 const targets=new Set(e?.group==='mapping'?[e.row.id]:e?.row.targets||[]),direct=e?.row.rangeIndex;
 document.querySelectorAll('[data-map-range]').forEach(n=>{const i=Number(n.dataset.mapRange),r=packet().boundaryMapping.sourceCoverage[i],active=i===direct||r.targets.some(t=>targets.has(t));n.setAttribute('stroke',active?'#e0a12a':mappingColor(r));n.setAttribute('stroke-width',active?5:2.8);n.setAttribute('opacity',e&&!active?.3:.9)});
 document.querySelectorAll('[data-map-target]').forEach(n=>{const active=targets.has(n.dataset.mapTarget);n.setAttribute('stroke-width',active?4:1.9);n.setAttribute('opacity',e&&!active?.28:1)});
 for(const svg of [$('raw'),$('fit')]){svg.querySelectorAll('.endpoint-support').forEach(n=>n.remove());if(e?.group==='mapping')for(const ref of e.row.endpointSupport){const span=packet().observation.spans.find(s=>s.id===ref.span),p=pointAt(span.controls,ref.at);node('circle',{class:'endpoint-support',cx:p[0],cy:p[1],r:5,fill:'white',stroke:'#e0a12a','stroke-width':2.5,'pointer-events':'none'},svg)}}
}
function mappingBind(n,e){bind(n,e);n.addEventListener('mouseenter',()=>mappingHighlight(e));n.addEventListener('focus',()=>mappingHighlight(e));n.addEventListener('mouseleave',()=>mappingHighlight(selected))}
function renderMapping(){
 const raw=$('raw'),fit=$('fit'),p=packet(),parts=p.layout.parts,m=p.boundaryMapping;raw.replaceChildren();fit.replaceChildren();
 const points=[...p.observation.strokes.flatMap(s=>s.points),...parts.flatMap(s=>s.geometry.points)],xs=points.map(p=>p[0]),ys=points.map(p=>p[1]),l=Math.min(0,...xs),t=Math.min(0,...ys),r=Math.max(512,...xs),b=Math.max(512,...ys),pad=.035*Math.max(r-l,b-t);
 for(const svg of [raw,fit])svg.setAttribute('viewBox',[l-pad,t-pad,r-l+2*pad,b-t+2*pad].join(' '));
 for(const part of parts){const g=part.geometry;if(g.fillIndices)node('polygon',{points:g.fillIndices.map(i=>g.points[i].join(',')).join(' '),fill:part.color,'fill-opacity':.05,'pointer-events':'none'},fit)}
 for(const e of entries.filter(e=>e.group==='mapping')){const part=parts.find(p=>p.id===e.row.part),g=part.geometry;for(const i of e.row.segments){const[a,b]=g.segments[i],n=node('line',{'data-map-target':e.row.id,x1:g.points[a][0],y1:g.points[a][1],x2:g.points[b][0],y2:g.points[b][1],stroke:part.color,'stroke-width':1.9,'stroke-dasharray':e.row.ink.length?'3 4':'1 6','stroke-linecap':'round'},fit);mappingBind(n,e)}}
 for(const svg of [raw,fit])for(const s of p.observation.strokes){node('polyline',{'data-stroke':s.id,points:s.points.map(p=>p.join(',')).join(' '),fill:'none',stroke:'#465852','stroke-width':1.7,'pointer-events':'none'},svg);if(s.points.length===1)node('circle',{cx:s.points[0][0],cy:s.points[0][1],r:2,fill:'#465852'},svg)}
 for(const e of entries.filter(e=>e.row.rangeIndex!==undefined)){const r=e.row,s=p.observation.spans.find(s=>s.id===r.span),pts=Array.from({length:25},(_,i)=>pointAt(s.controls,r.from+(r.to-r.from)*i/24));for(const svg of [raw,fit]){const n=node('polyline',{'data-map-range':r.rangeIndex,points:pts.map(p=>p.join(',')).join(' '),fill:'none',stroke:mappingColor(r),'stroke-width':2.8,opacity:.9},svg);mappingBind(n,e)}}
 for(const e of entries.filter(e=>e.row.unprofiled)){const s=p.observation.strokes.find(s=>s.index===e.row.stroke),q=s.points[e.row.sample];for(const svg of [raw,fit])mappingBind(node('circle',{cx:q[0],cy:q[1],r:4,fill:'#a65368','data-unprofiled':'1'},svg),e)}
 $('objectPanel').hidden=true;$('fitTitle').textContent='Shape boundaries and source support';$('fitStats').textContent=m.summary.mappedIntervals+' associations · '+m.summary.sharedSourceRanges+' shared ranges · '+m.summary.unresolvedSourceRanges+' unresolved ranges';$('modeNote').textContent='Part color = proposed support · ochre = shared · rose = unresolved · dotted = expected geometry';$('strokeValue').textContent='All';list();mappingHighlight(selected);if(selected)detail(selected);
}
'''
