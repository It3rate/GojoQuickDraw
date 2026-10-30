"""Offline source/baseline/settlement review; ink coverage is a diagnostic read."""
import gzip
import hashlib
import html
import json
from pathlib import Path
from . import layout_mapping
from .layout import COLORS


def boundary_fill(part):
    """Read an enclosing directed cycle from this stage's own boundary.

    Family changes can change point count and ordering. A later prototype's
    indices are not a correspondence to an earlier construction's points.
    This is a display read; it does not close gaps or change segment topology.
    """
    edges=part['segments'];points=part['points'];best=[];area=0
    for edge_index,(start,end) in enumerate(edges):
        path=[start];used={edge_index};current=end
        while current!=start and current not in path:
            path.append(current)
            next_edge=next(((i,b) for i,(a,b) in enumerate(edges) if i not in used and a==current),None)
            if next_edge is None:break
            used.add(next_edge[0]);current=next_edge[1]
        if current!=start or len(path)<3:continue
        magnitude=abs(sum(points[a][0]*points[b][1]-points[b][0]*points[a][1]
                          for a,b in zip(path,path[1:]+path[:1])))
        if magnitude>area:best=path;area=magnitude
    return best


def coverage(observed, scaffold, stage):
    parts=[]; seed={p['id']:p for p in scaffold['parts']}
    for live in stage['parts']:
        if live['id'] not in seed:
            continue
        original=seed[live['id']]
        parts.append({**original, 'bounds':live['bounds'], 'geometry':{**original['geometry'],
                     'points':live['points']}})
    mapping=layout_mapping.build(observed,dict(parts=parts))
    by_id={s['id']:s for s in observed['spans']}
    unknown=[]; missing=[]; total=0; unexplained=0
    for row in mapping['sourceCoverage']:
        s=by_id[row['span']]; a=row['from']; b=row['to']
        pts=[layout_mapping.point(s['controls'],a+(b-a)*i/12) for i in range(13)]
        import math
        length=sum(math.dist(x,y) for x,y in zip(pts,pts[1:]));total+=length
        if row['status']=='unresolved':
            unexplained+=length;unknown.append(dict(points=pts,span=row['span'],interval=[a,b],reason=row['reason']))
    for row in mapping['targets']:
        if not row['targetRemainder']:
            continue
        p=next(p for p in parts if p['id']==row['part']);geo=p['geometry']
        edges=[(geo['points'][geo['segments'][i][0]],geo['points'][geo['segments'][i][1]]) for i in row['segments']]
        import math
        lengths=[math.dist(a,b) for a,b in edges];total_length=sum(lengths)
        for start,end in row['targetRemainder']:
            offset=0
            for (a,b),length in zip(edges,lengths):
                low=max(offset,start*total_length);high=min(offset+length,end*total_length)
                if high>low and length>0:
                    segment=[[a[k]+(b[k]-a[k])*(d-offset)/length for k in (0,1)] for d in (low,high)]
                    missing.append(dict(points=segment,part=p['id'],reason='Expected boundary without a selected ink correspondence; not proof the shape is absent.'))
                offset+=length
    return dict(unknown=unknown,missing=missing,unexplainedFraction=unexplained/max(.001,total))


def write(folder):
    folder=Path(folder); rows=[]
    state=json.loads((folder/'study.json').read_text())
    for ident in state['config']['selection']:
        path=folder/'drawings'/ident/'view.json'
        if not path.is_file():
            continue
        view=json.loads(path.read_text(encoding='utf-8-sig')); baseline=json.loads((folder/'baseline'/(ident+'.json')).read_text(encoding='utf-8-sig'))
        with gzip.open(folder/'inputs'/(ident+'.json.gz'),'rt',encoding='utf-8') as stream:
            request=json.load(stream)
        new=view['results'][0];old=next(r for r in baseline['results'] if r['arm']=='bidirectional')
        panels=[]
        earlier=[('Earlier · pass 6',old,6,None),('Earlier · pass 64',old,64,None)] if old['passBudget']>=64 else [
            ('Earlier · initial',old,0,None),('Earlier · final',old,old['passes'],None)]
        if state['config'].get('baselineKind') == 'saved-studio':
            earlier=[('Saved Studio areas · reference',old,0,None),('Saved Studio cleanup · reference',old,1,None)]
        for title,result,wanted,scaffold in earlier+[
            ('Proposed families · before settlement',new,0,request['scaffold']),
            ('Added experts · pass 6',new,6,request['scaffold']),('Added experts · final',new,10**6,request['scaffold'])]:
            stage=min(result['stages'],key=lambda s:abs(s['pass']-wanted)) if wanted<10**6 else result['stages'][-1]
            diagnostic=coverage(request['packet']['observation'],scaffold,stage) if scaffold else dict(unknown=[],missing=[])
            panels.append(dict(title=title,stage=stage,**diagnostic))
        plan=request['heuristics']
        label='Connected constructions' if state['config'].get('connections') else 'Added experts'
        if state['config'].get('cad'):label='CAD relations'
        if state['config'].get('recoverMissing'):label='Residual recovery'
        if state['config'].get('connections'):
            panels[3]['title']=label+' · pass 6'
            panels[4]['title']=label+' · final'
        steps=[dict(title=label+' · selected pass',stage=stage,
                    **coverage(request['packet']['observation'],request['scaffold'],stage)) for stage in new['stages']]
        for panel in panels+steps:
            for part in panel['stage']['parts']:
                part['fillIndices']=boundary_fill(part)
        rows.append(dict(id=ident,keyId=view['keyId'],source=view['source'],panels=panels,
                         runtime=dict(archiveMode=new.get('archiveMode','history'),
                             timingMs={'sourceImport':view['importMs'], **new.get('timingMs',{})}
                                 if 'importMs' in view else new.get('timingMs',{}),
                             lossContract=new.get('lossContract'),
                             archiveBytes=(folder/'drawings'/ident/'bidirectional.gojo.bin.gz').stat().st_size
                                 if (folder/'drawings'/ident/'bidirectional.gojo.bin.gz').is_file() else None),
                         steps=steps,
                         proposals=dict(rules=len(plan['rules']),knots=len(plan['knots']),
                             addedWindows=sum(d['accepted'] for d in plan['discoveries']),view=plan['view'],
                             connections=len(plan.get('connections',{}).get('recipes',[]))),
                         openingChanges=plan.get('openingContext',{}).get('changes',[]),
                         recovery=plan.get('recovery'),cad=plan.get('cad'),cadGuides=[r for r in plan.get('connections',{}).get('recipes',[]) if r.get('family') in ('collinearity','shared assembly axis')],
                         layers=sorted({r['layer'] for r in plan['rules']})))
    payload=json.dumps(rows,separators=(',',':'),allow_nan=False).replace('<','\\u003c')
    page=PAGE.replace('__DATA__',payload).replace('__COLORS__',json.dumps(COLORS)).replace('__STATUS__',html.escape(state['status']))
    page=page.replace('__TITLE__','Church experts and CAD relations' if state['config'].get('cad') else 'Church experts, connected')
    if state['config'].get('recoverMissing'):page=page.replace('Church experts and CAD relations','Church experts and residual recovery')
    if state['config'].get('baselineKind') == 'saved-studio':
        page=page.replace('Same drawings and local models. More authored construction proposals and native error relations.',
            'Saved Studio shapes beside a new connected refinement. Blank reference panels mean that stage was not saved. References do not guide inference.')
    (folder/'review.html').write_text(page,encoding='utf-8')
    metadata=dict(schema='gojo.church-review-read.v1',rendererSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        calculationAdapterSha256=state['config']['adapterSha256'],
        views=[dict(id=row['id'],sha256=hashlib.sha256((folder/'drawings'/row['id']/'view.json').read_bytes()).hexdigest()) for row in rows],
        qualification='Regenerable observation of saved native result views; rebuilding the page does not rerun or mutate settlement.')
    (folder/'review-metadata.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
    summary=[]
    for row in rows:
        initial,final=row['panels'][2],row['panels'][-1]
        layer={};before_layer={}
        for e in initial['stage']['errors']:
            before_layer.setdefault(e['layer'],[]).append(e['demand'])
        for e in final['stage']['errors']:
            layer.setdefault(e['layer'],[]).append(e['demand'])
        summary.append(dict(id=row['id'],keyId=row['keyId'],**row['proposals'],layers=row['layers'],
            initialUnexplained=initial['unexplainedFraction'],finalUnexplained=final['unexplainedFraction'],
            initialMeanMismatch={k:sum(v)/len(v) for k,v in before_layer.items()},
            remainingMeanMismatch={k:sum(v)/len(v) for k,v in layer.items()},
            parts={role:sum(p['role']==role and bool(p['segments']) for p in final['stage']['parts']) for role in COLORS},
            captured=sum(c['active'] for c in final['stage'].get('captures',[])),
            remainingConnections=final['stage'].get('captures',[]),openingChanges=row['openingChanges'],
            cad=row['cad'],recovery=row['recovery'],
            finalPass=final['stage']['pass']))
    (folder/'analysis.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
    return rows


PAGE='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Church experts · native settlement</title><style>
:root{color-scheme:light;font:16px system-ui;color:#193e49;background:#f3f5ef}*{box-sizing:border-box}body{margin:0;padding:18px}h1{font-size:24px;margin:0 0 5px}p{margin:5px 0 12px;color:#536e74}button,select{font:inherit;padding:7px;border:1px solid #c4d3cc;border-radius:7px;background:white;color:inherit}header{display:flex;gap:10px;align-items:center;flex-wrap:wrap}.toolbar{display:flex;gap:16px;flex-wrap:wrap;margin:12px 0}#panels{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px}.panel{background:white;border:1px solid #cbd9d2;border-radius:12px;padding:12px;min-width:0}h2{font-size:17px;margin:0 0 5px}.panel svg{width:100%;height:340px}.caption{font-size:13px;color:#536e74;min-height:34px}details{margin-top:16px}table{border-collapse:collapse;font-size:14px}td,th{padding:5px 12px;text-align:left;border-bottom:1px solid #d1dad5}.legend{font-size:14px;color:#526d73}#facts{font-size:14px;margin:8px 0}a{color:#236c72}@media(max-width:1000px){#panels{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:600px){body{padding:10px}#panels{grid-template-columns:1fr}.panel svg{height:300px}select{max-width:100%}}
#connections{max-width:100%;overflow-x:auto}#revisions p{overflow-wrap:anywhere}
</style><h1>__TITLE__</h1><p>Same drawings and local models. More authored construction proposals and native error relations. __STATUS__.</p>
<header><button id="prev" aria-label="Previous drawing">←</button><select id="drawing" aria-label="Drawing"></select><button id="next" aria-label="Next drawing">→</button><span id="count"></span></header>
<div class="toolbar"><label>New pass <select id="pass" aria-label="Settlement pass"></select></label><label><input id="ink" type="checkbox"> Green interpreted ink</label><label><input id="unknown" type="checkbox"> Unexplained ink</label><label><input id="missing" type="checkbox"> Missing expected ink</label><label><input id="joints" type="checkbox"> Interpreted joints</label><label><input id="guides" type="checkbox"> Alignment guides</label></div>
<div class="legend">Faint ink = original · colors = proposed shapes · gray dotted = prior additions · orange = unexplained ink · gray dashes = missing boundary support.</div>
<div id="facts"></div><div id="panels"></div><details><summary>Remaining mismatch by relationship</summary><p>Mean normalized port demand in drawing units, not recognition accuracy or physical energy. Source strain can rise while the conceptual shape improves.</p><div id="errors"></div></details>
<details><summary>Construction capture and proposal revisions</summary><p>Capture substitutes an actual shared construction. It preserves exact relations during parameter settlement; strong opposing requests can release it on a later pass. Distances and pressure are authored diagnostic reads, not physical forces.</p><div id="connections"></div><div id="revisions"></div><div id="cad"></div><div id="recovery"></div></details>
<details><summary>Processing and saved native state</summary><div id="runtime"></div></details>
<p>← / → drawings. Families, supports and pairings are authored inference proposals; source bounds seed the machines. Interior corners expose additional source-interval sites. Native carrier tension moves parameters. Unknown ink is never erased, and a missing boundary is never labeled acquired ink.</p>
<script>const DATA=__DATA__,COLORS=__COLORS__;const $=id=>document.getElementById(id);let index=0;let selectedPass=Number.MAX_SAFE_INTEGER;
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const path=p=>p.length===2?`M${p[0]}L${p[1]}`:p.length===3?`M${p[0]}Q${p[1]} ${p[2]}`:p.length===4?`M${p[0]}C${p[1]} ${p[2]} ${p[3]}`:'';
function choose(n){index=(n+DATA.length)%DATA.length;location.hash=index;render()}
function render(){if(!DATA.length){$('facts').textContent='Waiting for the first completed drawing.';return}const row=DATA[index];$('drawing').value=index;$('count').textContent=`${index+1} of ${DATA.length}`;
 const chosen=row.steps.reduce((a,b)=>Math.abs(a.stage.pass-selectedPass)<Math.abs(b.stage.pass-selectedPass)?a:b);$('pass').innerHTML=row.steps.map(p=>`<option value="${p.stage.pass}">${p.stage.pass}</option>`).join('');$('pass').value=chosen.stage.pass;const panels=[...row.panels.slice(0,-1),chosen];
 const all=[...row.source.flat(),...[...row.panels,...row.steps].flatMap(p=>p.stage.parts.flatMap(s=>s.points)),...[...row.panels,...row.steps].flatMap(p=>p.stage.graph.curves.flat())];const xs=all.map(p=>p[0]),ys=all.map(p=>p[1]);const minX=Math.min(...xs)-20,minY=Math.min(...ys)-20;const frame=[minX,minY,Math.max(...xs)-minX+20,Math.max(...ys)-minY+20].join(' ');
 const original=opacity=>row.source.map(p=>`<polyline points="${p.map(p=>p.join(',')).join(' ')}" fill="none" stroke="#465b55" stroke-width="1.4" opacity="${opacity}"/>`).join('');
 function svg(panel){let out=original(panel ? .18 : 1);if(panel){const s=panel.stage;
 for(const part of s.parts){const c=part.prior?'#9ca4a5':COLORS[part.role]||'#7a8386';if(!part.segments.length)continue;
 if(part.fillIndices?.length>2)out+=`<polygon points="${part.fillIndices.map(i=>part.points[i].join(',')).join(' ')}" fill="${c}" fill-opacity=".045"/>`;
 const edges=part.segments.map(e=>`M${part.points[e[0]]}L${part.points[e[1]]}`).join('');out+=`<path class="shape" d="${edges}" fill="none" stroke="${c}" stroke-width="1.8" ${part.prior?'stroke-dasharray="3 3"':''}><title>${esc(part.role+' · '+part.id+(part.prior?' · expectation only':''))}</title></path>`;}
 if($('ink').checked)out+=s.graph.curves.map(p=>`<path class="working" d="${path(p)}" stroke="#307f73" stroke-width="1.2" fill="none"/>`).join('');
 if($('joints').checked)out+=s.graph.sites.map(j=>`<circle cx="${j.point[0]}" cy="${j.point[1]}" r="2.5" fill="#dc9450"><title>${esc(j.kind||'joint')}</title></circle>`).join('');
 if($('unknown').checked)out+=panel.unknown.map(u=>`<polyline class="unresolved" points="${u.points.map(p=>p.join(',')).join(' ')}" stroke="#df9020" stroke-width="2.5" fill="none"><title>${esc(u.span+' · '+u.reason)}</title></polyline>`).join('');
 if($('missing').checked)out+=panel.missing.map(u=>`<polyline class="missing" points="${u.points.map(p=>p.join(',')).join(' ')}" stroke="#7d8588" stroke-dasharray="4 3" stroke-width="2" fill="none"><title>${esc(u.part+' · '+u.reason)}</title></polyline>`).join('');}
 if(panel&&$('guides').checked)for(const guide of row.cadGuides||[]){const child=panel.stage.parts.find(p=>p.id===guide.bindings[0].target.part);const owner=panel.stage.parts.find(p=>p.id===guide.bindings[0].terms[0].part);if(!child||!owner)continue;
 const k=guide.bindings[0].target.coordinate;const x=guide.family==='collinearity'?child.bounds[k]:(child.bounds[0]+child.bounds[2])/2;const y=guide.family==='collinearity'?owner.bounds[k]:(owner.bounds[0]+owner.bounds[2])/2;const top=Math.min(child.bounds[1],owner.bounds[1])-12,bottom=Math.max(child.bounds[3],owner.bounds[3])+12;
 for(const [position,color] of [[x,'#c24b91'],[y,'#337b87']])out+=`<path class="alignment-guide" d="M${position},${top}L${position},${bottom}" stroke="${color}" stroke-dasharray="4 4" stroke-width="1"><title>${esc(guide.family+' · current separation '+Math.abs(x-y).toFixed(2))}</title></path>`;}
 return `<svg viewBox="${frame}" role="img">${out}</svg>`}
 $('panels').innerHTML=`<section class="panel"><h2>Original drawing</h2>${svg(null)}<div class="caption">Exact acquisition retained. ${esc(row.keyId)}</div></section>`+panels.map(p=>`<section class="panel"><h2>${esc(p.title)}</h2>${svg(p)}<div class="caption">Pass ${p.stage.pass} · ${p.stage.parts.filter(s=>s.segments.length).length} visible parts${p.unexplainedFraction!==undefined?' · unexplained '+(p.unexplainedFraction*100).toFixed(1)+'%':''}<br>${p.stage.applied} rewrites · ${p.stage.moved} actuator responses</div></section>`).join('');
 const captures=chosen.stage.captures||[];
 $('facts').textContent=`${row.proposals.rules} relationship opinions · ${row.proposals.knots} interior corner proposals · ${row.proposals.addedWindows} residual-ink window proposals · ${row.proposals.view?.selected||'view open'}${captures.length?' · '+captures.filter(c=>c.active).length+'/'+captures.length+' constructions captured':''}`;
 $('connections').innerHTML=captures.length?'<table><tr><th>Construction</th><th>State</th><th>Entry distance</th><th>Opposing request</th></tr>'+captures.map(c=>`<tr><td>${esc(c.id)}</td><td>${esc(c.action)}${c.releasePending?' · release pending':''}</td><td>${c.distance.toFixed(2)}</td><td>${c.pressure.toFixed(2)}</td></tr>`).join('')+'</table>':'No connection captures in this pass.';
 $('revisions').innerHTML=(row.openingChanges||[]).map(c=>`<p>${esc(c.part+' · '+c.action+(c.selected?' → '+c.selected:''))}<br>${esc(c.basis)}</p>`).join('');
 $('cad').innerHTML=(row.cad?.decisions||[]).map(c=>`<p>${esc(c.kind+' · '+(c.proposed?'proposed':'left open')+' · '+c.parts.join(' / '))}<br>${esc(c.basis)}</p>`).join('')+(row.cad?.groups||[]).map(g=>`<p>${esc(g.kind+' · '+g.parts.join(' / '))}<br>${esc(g.basis)}</p>`).join('');
 $('recovery').innerHTML=(row.recovery?.decisions||[]).map(c=>`<p>${esc(c.kind+' recovered · '+c.spans.length+' source profiles · '+c.parts.join(' / '))}<br>${esc(c.basis)}<br>Source support is retained; inferred joins and ideal boundaries are proposals.</p>`).join('');
 const end=chosen.stage.errors;const start=row.panels[2].stage.errors;const layers=[...new Set(end.map(e=>e.layer))];const mean=(rows,key)=>{const x=rows.filter(e=>e.layer===key);return x.length?x.reduce((a,b)=>a+b.demand,0)/x.length:0};
 $('errors').innerHTML='<table><tr><th>Relationship</th><th>Before</th><th>After</th></tr>'+layers.map(l=>`<tr><td>${esc(l)}</td><td>${mean(start,l).toFixed(3)}</td><td>${mean(end,l).toFixed(3)}</td></tr>`).join('')+'</table>';
 const rt=row.runtime, compact=rt.archiveMode==='current';
 $('runtime').innerHTML=`<p>${compact?'Current executable Gojo state. Keeps active machines, shared connections and error relations. Full proposal and response history is omitted; this is not an exact continuation of the adaptive controller.':'Full native audit history, including prior worlds and response witnesses.'} Visual passes remain available above.</p>`+
 (rt.archiveBytes===null?'<p>Native archive unavailable.</p>':`<p><a href="drawings/${encodeURIComponent(row.id)}/bidirectional.gojo.bin.gz">Native ${compact?'checkpoint':'history'}</a> · ${(rt.archiveBytes/1048576).toFixed(2)} MiB</p>`)+
 (Object.keys(rt.timingMs).length?'<table><tr><th>Work</th><th>Seconds</th></tr>'+Object.entries(rt.timingMs).map(([k,v])=>`<tr><td>${esc(k)}</td><td>${(v/1000).toFixed(3)}</td></tr>`).join('')+'</table>':'<p>Separate timings were not recorded in this older run.</p>');
}
DATA.forEach((r,i)=>$('drawing').add(new Option(`${i+1}. church · ${r.keyId}`,i)));$('drawing').onchange=()=>choose(+$('drawing').value);$('prev').onclick=()=>choose(index-1);$('next').onclick=()=>choose(index+1);$('pass').onchange=()=>{selectedPass=+$('pass').value;render()};['ink','unknown','missing','joints','guides'].forEach(id=>$(id).onchange=render);document.onkeydown=e=>{if(e.target.matches('input,select,textarea'))return;if(e.key==='ArrowLeft'||e.key==='ArrowRight'){e.preventDefault();choose(index+(e.key==='ArrowLeft'?-1:1))}if(e.key==='ArrowUp'||e.key==='ArrowDown'){e.preventDefault();selectedPass=Math.max(0,Math.min(DATA[index].steps.at(-1).stage.pass,+$('pass').value+(e.key==='ArrowUp'?-1:1)));render()}};window.onhashchange=()=>{index=Math.max(0,Math.min(DATA.length-1,parseInt(location.hash.slice(1))||0));render()};index=Math.max(0,Math.min(DATA.length-1,parseInt(location.hash.slice(1))||0));render();
</script></html>'''
