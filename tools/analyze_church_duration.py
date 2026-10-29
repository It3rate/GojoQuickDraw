"""Read-only duration diagnostics and a common-frame milestone comparison.

The old cleanup is run only after inference, as a qualified visual reference.
It is not imported into the native experiment or used as a correctness label.
"""
from collections import Counter
import gzip
import hashlib
import json
from pathlib import Path
from statistics import mean
import sys

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from gojo_quickdraw.cleanup import build as cleanup_reference
from tools.analyze_church_study import analyze, bezier, incidence, layer


def geometry(stage):
    graph=stage['graph']
    # Generated view IDs and list order do not establish a visible movement.
    def point(p):return [round(v,7) or 0 for v in p]
    edges=sorted(([point(p) for p in e['controls']] for e in graph['edges']),key=lambda p:json.dumps(p))
    sites=sorted(((point(s['point']),s['kind']) for s in graph['sites']),key=lambda p:json.dumps(p))
    parts=sorted(((p['id'],[point(v) for v in p['points']],p['segments'],p['prior']) for p in stage['parts']),key=lambda p:p[0])
    return json.dumps(dict(edges=edges,sites=sites,parts=parts),sort_keys=True,separators=(',',':'))


def source_movement(initial,before,after):
    """Compare the same original parameters, even after either graph is split."""
    def at(graph,source,t):
        for e in graph['edges']:
            lo,hi=e['interval']
            if not e['inferred'] and e['source']==source and lo-1e-9<=t<=hi+1e-9 and hi>lo:
                return bezier(e['controls'],max(0,min(1,(t-lo)/(hi-lo))))
        return None
    values=[];missing=0;total=0
    for source in dict.fromkeys(e['source'] for e in initial['edges'] if not e['inferred']):
        for i in range(17):
            total+=1;a=at(before,source,i/16);b=at(after,source,i/16)
            if a is None or b is None:missing+=1
            else:values.append(sum((x-y)**2 for x,y in zip(a,b))**.5)
    return dict(mean=mean(values) if values else None,max=max(values,default=None),total=total,missing=missing,covered=len(values))


def run_diagnostics(run):
    stages=run['stages'];six=next((s for s in stages if s['pass']==6),stages[-1]);final=stages[-1]
    series=[dict(passIndex=s['pass'],rewrites=s['applied'],moved=s['moved'],
                 mapping=layer(s,'mapping'),source=layer(s,'source'),attachment=layer(s,'attachment'),
                 containment=layer(s,'containment'),repetition=layer(s,'repetition'),
                 coverage=s['sourceCoverage'],quantum=sum(r['quantum'] for r in s['transfers']),
                 blocked=sum(r['blocked'] for r in s['transfers']),deferred=sum(r['deferred'] for r in s['transfers'])) for s in stages]
    signatures=[geometry(s) for s in stages]
    old_parts={p['id']:p for p in six['parts']}
    part_move=max((abs(a-b) for p in final['parts'] for a,b in zip(p['bounds'],old_parts[p['id']]['bounds'])),default=0)
    changes=[s['pass'] for i,s in enumerate(stages) if i and signatures[i]!=signatures[i-1]]
    tail_period=None
    if len(signatures)>2 and signatures[-1]!=signatures[-2]:
        for period in range(2,min(8,len(signatures)//2)+1):
            if signatures[-period:]==signatures[-2*period:-period]:tail_period=period;break
    return dict(passes=run['passes'],budget=run['passBudget'],stopReason=run['stopReason'],
                quiet=run['stopReason'].startswith('quiet'),
                graphChangedAfterSix=six['graph']!=final['graph'],
                visibleGeometryChangedAfterSix=geometry(six)!=geometry(final),
                partsChangedAfterSix=six['parts']!=final['parts'],
                maximumShapeBoundChangeAfterSix=part_move,
                incidenceChangedAfterSix=incidence(six['graph'])!=incidence(final['graph']),
                lateRewrites=sum(s['applied'] for s in stages if s['pass']>6),
                lateActuatorMoves=sum(s['moved'] for s in stages if s['pass']>6),
                lateRewritePassesWithoutVisibleChange=sum(s['pass']>6 and s['applied']>0 and signatures[i]==signatures[i-1] for i,s in enumerate(stages) if i),
                lastVisibleChangePass=max(changes,default=0),observedTailPeriod=tail_period,
                sixToFinalSourceSamples=source_movement(stages[0]['graph'],six['graph'],final['graph']),
                mappingAtSix=layer(six,'mapping'),mappingAtFinal=layer(final,'mapping'),
                attachmentAtSix=layer(six,'attachment'),attachmentAtFinal=layer(final,'attachment'),
                finalQuantumRemainder=series[-1]['quantum'],series=series)


def analyze_duration(root):
    root=Path(root);state=json.loads((root/'study.json').read_text())
    if state['status']!='complete':raise ValueError('Analyze a complete, prefix-verified replay')
    verification=json.loads((root/'prefix-verification.json').read_text())
    if not verification['sourceUnchanged']:raise ValueError('Original study changed')
    rows=[];display=[];(root/'reference').mkdir(exist_ok=True)
    order=json.loads((root/'selection.json').read_text())
    for item in order:
        path=root/'drawings'/item['id']/'view.json'
        view=json.loads(path.read_text(encoding='utf-8-sig'))
        with gzip.open(root/'inputs'/(view['id']+'.json.gz'),'rt',encoding='utf-8') as stream:request=json.load(stream)
        reference=cleanup_reference(request['packet']['observation'],request['scaffold'])
        reference['comparisonQualification']='Earlier authored cleanup, generated only after the duration run. Visual reference, not an inference input or a ground-truth label.'
        (root/'reference'/(view['id']+'.json')).write_text(json.dumps(reference,indent=2),encoding='utf-8')
        parts=[]
        for p in reference['parts']:
            geo=p['geometry'];segments=geo['segments']
            if 'displaySegments' in geo:segments=[segments[i] for i in geo['displaySegments']]
            parts.append(dict(id=p['id'],role=p['role'],prior=p['status']=='prior-only',points=geo['points'],segments=segments))
        intervals=[r for r in request['mapping']['sourceCoverage'] if r['status']=='unresolved']
        spans={s['id']:s for s in request['packet']['observation']['spans']}
        unknown=[]
        for item in intervals:
            span=spans[item['span']]
            points=[bezier(span['controls'],item['from']+(item['to']-item['from'])*i/16) for i in range(17)]
            unknown.append(dict(span=item['span'],start=item['from'],end=item['to'],stroke=span['strokeIndex'],points=points,reason=item['reason']))
        runs={r['arm']:run_diagnostics(r) for r in view['results']}
        rows.append(dict(id=view['id'],keyId=view['keyId'],versions=runs,
                         initialUnresolvedRanges=len(intervals),initialUnresolvedParameterParticipation=sum(r['to']-r['from'] for r in intervals),
                         initialParts=Counter(p['role'] for p in request['scaffold']['parts']),
                         referenceParts=Counter(p['role'] for p in reference['parts'])))
        coupled=next(r for r in view['results'] if r['arm']=='bidirectional')
        milestones=[]
        for target in (6,16,32,state['config']['passes']):
            stage=next((s for s in coupled['stages'] if s['pass']==target),coupled['stages'][-1])
            milestones.append(dict(requested=target,stage=stage))
        display.append(dict(id=view['id'],keyId=view['keyId'],source=view['source'],milestones=milestones,
                            reference=parts,unknown=unknown,diagnostic=runs['bidirectional']))
    arms={}
    for arm in rows[0]['versions']:
        values=[r['versions'][arm] for r in rows]
        arms[arm]=dict(drawings=len(values),quiet=sum(r['quiet'] for r in values),
            budgetReached=sum(not r['quiet'] for r in values),
            graphChangedAfterSix=sum(r['graphChangedAfterSix'] for r in values),
            partsChangedAfterSix=sum(r['partsChangedAfterSix'] for r in values),
            incidenceChangedAfterSix=sum(r['incidenceChangedAfterSix'] for r in values),
            lateRewrites=sum(r['lateRewrites'] for r in values),lateActuatorMoves=sum(r['lateActuatorMoves'] for r in values),
            meanPasses=mean(r['passes'] for r in values),
            meanMappingAtSix=mean(r['mappingAtSix'] for r in values if r['mappingAtSix'] is not None) if any(r['mappingAtSix'] is not None for r in values) else None,
            meanMappingAtFinal=mean(r['mappingAtFinal'] for r in values if r['mappingAtFinal'] is not None) if any(r['mappingAtFinal'] is not None for r in values) else None,
            observedRepeatingGeometry=sum(r['observedTailPeriod'] is not None for r in values))
    result=dict(drawings=len(rows),passBudget=state['config']['passes'],versions=arms,
                exactEarlierVersions=verification['versions'],sourceUnchanged=True,
                qualification=f'{len(rows)} diagnostic acquisitions, not a random accuracy sample. Same cached evidence, part proposals, associations, providers, response laws and inner-step count. Only the outer pass budget changes. Quiet does not imply zero strain. Repeated visible geometry, read at 1e-7 drawing units, does not prove a full internal-state cycle. Unresolved intervals use the initial mapper and are not active explanation pressure. Earlier cleanup is a separate authored reference, not a correct label.')
    (root/'duration-analysis.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    (root/'duration-drawings.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
    content=HTML.replace('__DATA__',json.dumps(display,separators=(',',':'),allow_nan=False).replace('<','\\u003c'))
    (root/'comparison.html').write_text(content,encoding='utf-8')
    analyze(root,root/'analysis.json')
    return result


HTML=r'''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Longer church settlement</title>
<style>:root{font:15px Segoe UI,Arial,sans-serif;color:#193e43;background:#f3f5f0}*{box-sizing:border-box}body{margin:0}header{position:sticky;top:0;background:white;border-bottom:1px solid #cdd9d1;padding:10px 18px;display:flex;gap:10px;align-items:center;flex-wrap:wrap;z-index:1}h1{font-size:20px;margin:0 auto 0 0}button,select{font:inherit;padding:7px;border:1px solid #b9cbc3;border-radius:6px;background:white;color:inherit}main{padding:14px 18px;max-width:1650px;margin:auto}.note{font-size:13px;color:#587178;line-height:1.5}.panels{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}.panel{background:white;border:1px solid #ccd8d0;padding:10px;border-radius:9px;min-width:0}.panel h2{font-size:14px;margin:0 0 6px}.panel svg{width:100%;display:block;aspect-ratio:1.45}svg path,svg line,svg polyline{fill:none;stroke-linecap:round;stroke-linejoin:round;vector-effect:non-scaling-stroke}svg text{font-size:8px;paint-order:stroke;stroke:white;stroke-width:2px}.metrics{font-size:12px;line-height:1.5;color:#557078;min-height:35px}#chart{background:white;border:1px solid #cbd8d0;border-radius:8px;padding:7px;margin-top:12px}#chart svg{width:100%;height:135px}a{color:#26756c}details{margin-top:12px}#warnings{line-height:1.5;font-size:13px}@media(max-width:900px){.panels{grid-template-columns:repeat(2,1fr)}}@media(max-width:540px){.panels{grid-template-columns:1fr}header{position:static}}</style>
<header><h1>Longer church settlement</h1><button id="prev">←</button><select id="drawing" aria-label="Drawing"></select><button id="next">→</button><label><input id="ink" type="checkbox" checked> Interpreted ink</label><label><input id="joints" type="checkbox"> Joints</label><label><input id="unknown" type="checkbox"> Unexplained ink</label><a href="review.html">Four-version review</a></header>
<main><p class="note">Same evidence, constructions, models and response rules. The six-pass prefix is checked exactly. All panels share one coordinate frame. Toggle interpreted ink to compare construction shapes; faint acquired ink remains visible. The earlier cleanup is a separate authored reference generated after inference, not a target used by these runs.</p><div class="panels" id="panels"></div><div id="chart"></div><div id="warnings"></div><details><summary>Measurements and limitations</summary><p class="note">Mapping, attachment and containment are separate authored interval violations. Lower strain is not semantic accuracy. A quiet run can retain nonzero errors and requests smaller than one actor tick. Orange source intervals were unresolved by the initial part mapper; they remain fixed during this experiment and do not actively request a new part. Reference additions remain gray and dotted.</p><a href="duration-analysis.json">Duration results</a> · <a href="duration-drawings.json">Per-drawing diagnostics</a> · <a href="prefix-verification.json">Earlier-pass verification</a></details></main>
<script>const DATA=__DATA__,$=id=>document.getElementById(id),colors={building:'#32869e',tower:'#5a7199',roof:'#c67526',cross:'#b54479',door:'#289582',window:'#9167b1',ground:'#718570'};let index=Math.max(0,Math.min(DATA.length-1,Number(location.hash.slice(1))||0));const esc=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;'),num=v=>v==null?'—':Number(v).toFixed(3),path=p=>p.map((v,i)=>(i?'L':'M')+v.join(' ')).join(' ');function curve(p){return p.length===4?'M'+p[0].join(' ')+' C'+p.slice(1).map(x=>x.join(' ')).join(' '):p.length===3?'M'+p[0].join(' ')+' Q'+p.slice(1).map(x=>x.join(' ')).join(' '):path(p)}
function view(row,stage,parts,original,bounds){let out=`<svg role="img" viewBox="${bounds.join(' ')}" aria-label="Church comparison">`;for(const stroke of row.source)out+=`<path d="${path(stroke)}" stroke="#3c534e" stroke-width="${original?1.5:1}" opacity="${original?1:.17}"/>`;for(const p of parts||[]){let c=p.prior?'#929c9e':colors[p.role]||'#728584';for(const [a,b] of p.segments)out+=`<line x1="${p.points[a][0]}" y1="${p.points[a][1]}" x2="${p.points[b][0]}" y2="${p.points[b][1]}" stroke="${c}" stroke-width="1.6" ${p.prior?'stroke-dasharray="2 3"':''}/>`}if(stage&&$('ink').checked){for(const e of stage.graph.edges)out+=`<path d="${curve(e.controls)}" stroke="${e.inferred?'#b94e9b':'#307f73'}" stroke-width="1.2" ${e.inferred?'stroke-dasharray="3 3"':''}/>`;if($('joints').checked)for(const j of stage.graph.sites)out+=`<circle cx="${j.point[0]}" cy="${j.point[1]}" r="1.4" fill="white" stroke="#bd792e"/><text x="${j.point[0]+2}" y="${j.point[1]-2}" fill="#a46c27">${esc(j.kind)}</text>`}if($('unknown').checked)for(const u of row.unknown)out+=`<polyline points="${u.points.map(p=>p.join(',')).join(' ')}" stroke="#df9020" stroke-width="3" opacity=".8"><title>${esc(u.span)} · stroke ${u.stroke} · ${u.start}–${u.end}: ${esc(u.reason)}</title></polyline>`;return out+'</svg>'}
function average(s,name){let v=s.errors.filter(e=>e.resolved&&e.layer===name);return v.length?v.reduce((a,e)=>a+e.demand,0)/v.length:null}
function chart(series){let w=1100,h=110,pad=18,max=Math.max(.01,...series.flatMap(s=>['mapping','attachment','containment'].map(k=>s[k]||0))),end=Math.max(1,series.at(-1).passIndex),out='<svg viewBox="0 0 1140 140" role="img" aria-label="Strain by pass">';for(const [key,color] of [['mapping','#307f73'],['attachment','#b44d7f'],['containment','#c78028']]){let points=series.filter(s=>s[key]!=null).map(s=>[pad+s.passIndex/end*w,h-s[key]/max*(h-12)]);if(points.length)out+=`<polyline points="${points.map(p=>p.join(',')).join(' ')}" stroke="${color}" stroke-width="2"/>`;for(const s of series.filter(s=>s[key]!=null))out+=`<circle cx="${pad+s.passIndex/end*w}" cy="${h-s[key]/max*(h-12)}" r="2" fill="${color}"><title>Pass ${s.passIndex}: ${key} ${num(s[key])}</title></circle>`}out+='<text x="18" y="134">0</text>'+`<text x="${w}" y="134">${end} passes</text></svg>`;return '<div class="note">Authored strain by pass: <span style="color:#307f73">mapping</span> · <span style="color:#b44d7f">attachment</span> · <span style="color:#c78028">containment</span>. Hover a point for its value.</div>'+out}
function render(){let row=DATA[index],points=row.source.flat().concat(row.reference.flatMap(p=>p.points),row.milestones.flatMap(m=>m.stage.parts.flatMap(p=>p.points)),row.milestones.flatMap(m=>m.stage.graph.edges.flatMap(e=>e.controls))),xs=points.map(p=>p[0]),ys=points.map(p=>p[1]),l=Math.min(...xs),t=Math.min(...ys),w=Math.max(1,Math.max(...xs)-l),h=Math.max(1,Math.max(...ys)-t),pad=Math.max(w,h)*.06,bounds=[l-pad,t-pad,w+2*pad,h+2*pad];let panels=[{title:'Original',svg:view(row,null,[],true,bounds),text:'Exact retained acquired drawing.'}];for(const m of row.milestones){let s=m.stage;panels.push({title:`Pass ${m.requested}${s.pass!==m.requested?' · stopped at '+s.pass:''}`,svg:view(row,s,s.parts,false,bounds),text:`Mapping ${num(average(s,'mapping'))} · attachment ${num(average(s,'attachment'))}<br>${s.applied} rewrites · ${s.moved} actuator moves this pass`})}panels.push({title:'Earlier cleanup · reference only',svg:view(row,null,row.reference,false,bounds),text:'Stronger authored construction rules; not ground truth or an inference input.'});$('panels').innerHTML=panels.map(p=>`<section class="panel"><h2>${p.title}</h2>${p.svg}<div class="metrics">${p.text}</div></section>`).join('');$('chart').innerHTML=chart(row.diagnostic.series);let d=row.diagnostic;$('warnings').textContent=`${row.keyId} · ${d.stopReason} at pass ${d.passes}. After pass 6: ${d.lateRewrites} local rewrites, ${d.lateActuatorMoves} actuator moves. Initial unresolved source ranges: ${row.unknown.length}.${d.observedTailPeriod?' Visible tail geometry repeats with observed period '+d.observedTailPeriod+'; this is not proof of a full-state cycle.':''}`;$('drawing').value=index;location.hash=String(index)}$('drawing').innerHTML=DATA.map((r,i)=>`<option value="${i}">${i+1} / ${DATA.length} · ${esc(r.keyId)}</option>`).join('');$('drawing').onchange=()=>{index=Number($('drawing').value);render()};$('prev').onclick=()=>{index=Math.max(0,index-1);render()};$('next').onclick=()=>{index=Math.min(DATA.length-1,index+1);render()};$('ink').onchange=render;$('joints').onchange=render;$('unknown').onchange=render;document.addEventListener('keydown',e=>{if(['INPUT','SELECT','TEXTAREA'].includes(e.target.tagName))return;if(e.key==='ArrowLeft'||e.key==='ArrowRight'){e.preventDefault();index=Math.max(0,Math.min(DATA.length-1,index+(e.key==='ArrowLeft'?-1:1)));render()}});render();</script></html>'''


if __name__=='__main__':
    print(json.dumps(analyze_duration(sys.argv[1]),indent=2))
