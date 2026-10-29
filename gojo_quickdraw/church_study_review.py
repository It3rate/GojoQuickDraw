"""Paged, reloadable review of the independent connected-church study."""
from __future__ import annotations

from collections import defaultdict
import json
import math
from pathlib import Path
from statistics import mean

from . import dataset


def summarize(views):
    arms = defaultdict(list)
    for view in views:
        for run in view['results']:
            final, initial = run['stages'][-1], run['stages'][0]
            layers = defaultdict(list)
            for error in final['errors']:
                if error['resolved']:
                    layers[error['layer']].append(error['demand'])
            initial_layers = defaultdict(list)
            for error in initial['errors']:
                if error['resolved']:
                    initial_layers[error['layer']].append(error['demand'])
            arms[run['arm']].append(dict(applied=run['accepted'], moved=run['moved'], passes=run['passes'],
                components=final['components'], initialComponents=initial['components'], cycles=final['cycles'],
                sourceEdges=final['sourceEdges'], inferredEdges=final['inferredEdges'],
                sourceCoverage=final.get('sourceCoverage'),
                sourceStrain=mean(layers['source']) if layers['source'] else None,
                mappingStrain=mean(layers['mapping']) if layers['mapping'] else None,
                initialMappingStrain=mean(initial_layers['mapping']) if initial_layers['mapping'] else None,
                attachmentStrain=mean(layers['attachment']) if layers['attachment'] else None,
                containmentStrain=mean(layers['containment']) if layers['containment'] else None,
                changesAfterFirst=sum(s['applied'] for s in run['stages'][2:]),
                sourceMovement=movement(initial['graph'],final['graph'])))
    result={}
    for arm,rows in arms.items():
        keys=rows[0]
        result[arm]=dict(drawings=len(rows),**{key:mean(r[key] for r in rows if r[key] is not None)
                                            if any(r[key] is not None for r in rows) else None for key in keys})
    return result


def movement(initial, final):
    """Source-interval endpoint deviation; not visual or semantic accuracy."""
    original={e['source']:e['controls'] for e in initial['edges'] if not e['inferred']}
    values=[]
    for edge in final['edges']:
        controls=original.get(edge['source'])
        if controls is None or edge['inferred']:
            continue
        for p,t in zip((edge['controls'][0],edge['controls'][-1]),edge['interval']):
            row=controls
            while len(row)>1:
                row=[[(1-t)*a[k]+t*b[k] for k in (0,1)] for a,b in zip(row,row[1:])]
            values.append(math.dist(p,row[0]))
    return mean(values) if values else 0


def write(output):
    output=Path(output);pages=output/'pages';pages.mkdir(exist_ok=True)
    views=[json.loads(path.read_text(encoding='utf-8-sig')) for path in sorted((output/'drawings').glob('*/view.json'))]
    state=json.loads((output/'study.json').read_text(encoding='utf-8')) if (output/'study.json').is_file() else {}
    try:
        summary=summarize(views)
    except KeyError:
        # A review should expose missing fields during development, never silently
        # publish an apparently successful scientific summary.
        raise
    dataset._atomic_json(output/'summary.json',dict(drawings=len(views),arms=summary,
        qualification='Descriptive authored strain and graph changes, not accuracy or evidence of correct church interpretation. Source strain compares different surviving interval endpoints. No learned improvement claim follows from a lower scaffold error.'))
    page_count=max(1,math.ceil(len(views)/12))
    for page in range(page_count):
        rows=views[page*12:(page+1)*12]
        text='window.CHURCH_PAGE='+json.dumps(rows,separators=(',',':'),allow_nan=False).replace('<','\\u003c')+';\n'
        target=pages/f'page-{page+1:05d}.js';temp=target.with_suffix('.js.tmp');temp.write_text(text,encoding='utf-8');temp.replace(target)
    manifest=dict(drawings=len(views),pages=page_count,state=state.get('status','pilot'),selected=state.get('selected',len(views)),summary=summary,failures=state.get('preparationErrors',0)+state.get('nativeErrors',0),reason=state.get('supersededReason'))
    content=HTML.replace('__MANIFEST__',json.dumps(manifest,separators=(',',':')).replace('<','\\u003c'))
    target=output/'review.html';temp=output/'review.html.tmp';temp.write_text(content,encoding='utf-8');temp.replace(target)


HTML=r'''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Church evidence settlement</title><style>
:root{font-family:Segoe UI,Arial,sans-serif;color:#1d3e47;background:#f3f5f0;color-scheme:light}*{box-sizing:border-box}body{margin:0}header{padding:12px 20px;background:#fff;border-bottom:1px solid #ccd7d1;position:sticky;top:0;z-index:2;display:flex;gap:12px;align-items:center;flex-wrap:wrap}h1{font-size:21px;margin:0 auto 0 0}button,select{font:inherit;padding:7px 10px;border:1px solid #bbc9c2;background:white;border-radius:6px;color:inherit}button{cursor:pointer}main{padding:16px 20px}#status{font-size:13px;color:#5f7277}#grid{display:grid;grid-template-columns:repeat(3,minmax(260px,1fr));gap:12px}.card{background:white;border:1px solid #ccd7d1;border-radius:10px;padding:10px;cursor:pointer;min-width:0}.card h2{font-size:14px;display:flex;justify-content:space-between;margin:0 0 5px}.card small{font-weight:400;color:#687b80}svg{width:100%;display:block;background:white;aspect-ratio:1.25}text{font-size:9px;paint-order:stroke;stroke:white;stroke-width:2px;stroke-linejoin:round}svg path,svg line,svg polyline{vector-effect:non-scaling-stroke;fill:none;stroke-linecap:round;stroke-linejoin:round}.pair{display:grid;grid-template-columns:1fr 1fr;gap:8px}.caption{font-size:12px;color:#63797f}.meta{font-size:12px;color:#597178;margin-top:6px;min-height:30px}details{margin:0 0 14px;border:1px solid #cdd8d2;border-radius:7px;padding:9px;background:white}summary{cursor:pointer}table{font-size:13px;border-collapse:collapse;width:100%;margin:10px 0}td,th{padding:7px;text-align:left;border-bottom:1px solid #dce5df}.explain{font-size:13px;line-height:1.5;max-width:1150px}#dialog{width:96vw;max-width:1600px;max-height:95vh;border:1px solid #becbc4;border-radius:12px;padding:16px;background:#f7f8f3;color:inherit}#dialog::backdrop{background:#152e3d77}.dialog-head{display:flex;gap:9px;align-items:center;flex-wrap:wrap}.dialog-head h2{margin:0 auto 0 0;font-size:19px}.views{display:grid;grid-template-columns:repeat(4,minmax(190px,1fr));gap:10px;margin:12px 0}.view{background:white;border-radius:7px;padding:8px}.view h3{margin:3px;font-size:13px}.view svg{aspect-ratio:1}.bars{height:75px;display:flex;align-items:flex-end;gap:4px}.bar{flex:1;background:#3b9187;min-width:5px}.legend{display:flex;gap:10px;flex-wrap:wrap;font-size:12px}.legend span::before{content:'● ';color:var(--c)}.active{outline:2px solid #39a193}input[type=range]{width:160px}a{color:#217467}@media(max-width:1050px){#grid{grid-template-columns:repeat(2,1fr)}.views{grid-template-columns:repeat(2,1fr)}}@media(max-width:640px){#grid,.views{grid-template-columns:1fr}header{position:static}}
</style><header><h1>Church evidence settlement</h1><span id="status"></span><button id="prev" aria-label="Previous page">←</button><select id="page" aria-label="Page"></select><button id="next" aria-label="Next page">→</button><select id="arm" aria-label="Comparison"><option value="bidirectional">Bidirectional</option><option value="fixed-scaffold">Fixed scaffold</option><option value="shapes-only">Shapes around fixed ink</option><option value="local-models">Local models only</option></select><label>Pass <input id="step" type="range" min="0" max="6" value="6"><b id="stepLabel"></b></label><label><input id="joints" type="checkbox"> Joints</label><label><input id="shapes" type="checkbox" checked> Shapes</label></header><main>
<details><summary>What this experiment tests · measurements and limitations</summary><div class="explain"><p>The original is on the left. On the right, green ink is the interpreted source graph; pale color constructions are source-supported, provisional church parts. Gray dotted parts are prior-only. Magenta connectors are inferred closure, not acquired strokes. Joint letters are recalculated from the current actual graph.</p><p>Local models repair joins, line/curve choices and closure. Shape-only holds the ink still. Fixed scaffold holds the shapes still while ink responds. Bidirectional allows both to respond through native port relations and explicit error intervals. Later passes reread the changed graph; they are not new independent evidence.</p><p>Errors are authored compatibility measures, not semantic accuracy or physical truth. The final hand-authored church cleanup is excluded from inference and evaluation. Existing local models are reused; no new model is fitted in this study. Recognition means the QuickDraw game model accepted the drawing, not a human judgment.</p><div id="table"></div><p><a href="study.json">Run settings and fingerprints</a> · <a href="summary.json">Summary</a> · <a href="cohort.json">Whole-drawing cohort</a> · <a href="preparation.json">Preparation outcomes</a></p></div></details>
<div class="legend"><span style="--c:#32869e">building</span><span style="--c:#5a7199">tower</span><span style="--c:#c67526">roof</span><span style="--c:#b54479">cross</span><span style="--c:#289582">door</span><span style="--c:#9167b1">window</span><span style="--c:#307f73">interpreted ink</span></div><p class="caption" id="scope"></p><div id="grid"></div></main>
<dialog id="dialog"><div class="dialog-head"><h2 id="title"></h2><button id="drawingPrev">← Drawing</button><button id="drawingNext">Drawing →</button><button id="stagePrev">↑ Pass</button><button id="stageNext">Pass ↓</button><label><input id="detailJoints" type="checkbox"> Joints</label><label><input id="detailShapes" type="checkbox" checked> Shapes</label><button id="close">Close</button></div><div id="detailViews" class="views"></div><div id="detailMetrics"></div><p class="caption">← / → drawing · Shift + ← / → page · ↑ / ↓ pass · Escape close. Selected pass and comparison remain selected between pages.</p><a id="archive">Open native archive</a></dialog>
<script>
const MANIFEST=__MANIFEST__, $=id=>document.getElementById(id), colors={building:'#32869e',tower:'#5a7199',roof:'#c67526',cross:'#b54479',door:'#289582',window:'#9167b1'},labels={'local-models':'Local models','shapes-only':'Shapes around fixed ink','fixed-scaffold':'Fixed scaffold','bidirectional':'Bidirectional'};
let rows=[],page=1,selected=0,loading=false,script;
const params=new URLSearchParams(location.hash.slice(1));page=Math.max(1,Math.min(MANIFEST.pages,Number(params.get('page'))||1));$('arm').value=params.get('arm')||'bidirectional';$('step').max=MANIFEST.state==='pilot'?6:16;$('step').value=params.get('pass')||6;
const esc=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
const num=x=>x==null?'—':Number(x).toFixed(2);
const path=points=>points.map((p,i)=>(i?'L':'M')+p[0].toFixed(3)+' '+p[1].toFixed(3)).join(' ');
function curve(points){if(points.length===4)return 'M'+points[0].join(' ')+' C'+points.slice(1).map(p=>p.join(' ')).join(' ');if(points.length===3)return 'M'+points[0].join(' ')+' Q'+points.slice(1).map(p=>p.join(' ')).join(' ');return path(points)}
function svg(row,stage,original=false){const ink=row.source||[],all=ink.flat();if(!all.length)return '<svg></svg>';let [l,t,r,b]=[Math.min(...all.map(p=>p[0])),Math.min(...all.map(p=>p[1])),Math.max(...all.map(p=>p[0])),Math.max(...all.map(p=>p[1]))];if(stage&&!original)for(const p of stage.parts)for(const q of p.points){l=Math.min(l,q[0]);t=Math.min(t,q[1]);r=Math.max(r,q[0]);b=Math.max(b,q[1])}let w=Math.max(1,r-l),h=Math.max(1,b-t),pad=Math.max(w,h)*.06;let out=`<svg role="img" aria-label="${original?'Original drawing':'Interpreted church graph'}" viewBox="${l-pad} ${t-pad} ${w+pad*2} ${h+pad*2}">`;for(const run of ink)out+=`<path d="${path(run)}" stroke="#3c534e" stroke-width="${original?1.4:1}" opacity="${original?1:.16}"/>`;if(stage&&!original){if($('shapes').checked)for(const p of stage.parts){let c=p.prior?'#929c9e':colors[p.role]||'#89979c';for(const e of p.segments)out+=`<line x1="${p.points[e[0]][0]}" y1="${p.points[e[0]][1]}" x2="${p.points[e[1]][0]}" y2="${p.points[e[1]][1]}" stroke="${c}" stroke-width="1.4" opacity=".9" ${p.prior?'stroke-dasharray="2 3"':''}/>`}for(const e of stage.graph.edges)out+=`<path d="${curve(e.controls)}" stroke="${e.inferred?'#b94e9b':'#307f73'}" stroke-width="1.25" ${e.inferred?'stroke-dasharray="3 3"':''}/>`;if($('joints').checked)for(const j of stage.graph.sites){let c=j.degree<2?'#7c8a8e':j.kind==='X'?'#bb467a':'#c57d27';out+=`<circle cx="${j.point[0]}" cy="${j.point[1]}" r="1.7" fill="white" stroke="${c}" stroke-width="1"/><text x="${j.point[0]+2}" y="${j.point[1]-2}" fill="${c}">${esc(j.kind)}</text>`}}return out+'</svg>'}
function get(row,arm){return row.results.find(r=>r.arm===arm)}
function current(run){return run.stages[Math.min(Number($('step').value),run.stages.length-1)]}
function average(errors,layer){let list=errors.filter(e=>e.resolved&&(!layer||e.layer===layer));return list.length?list.reduce((n,e)=>n+e.demand,0)/list.length:null}
function state(){location.hash=new URLSearchParams({page,arm:$('arm').value,pass:$('step').value}).toString()}
function render(){if(loading)return;let max=Math.max(1,...rows.flatMap(r=>r.results.map(v=>v.passBudget)));$('step').max=max;$('stepLabel').textContent=$('step').value;$('scope').textContent=`Page ${page} of ${MANIFEST.pages} · drawings ${(page-1)*12+1}–${Math.min(page*12,MANIFEST.drawings)} of ${MANIFEST.drawings}. Click a drawing to compare all four versions.`;$('grid').innerHTML=rows.map((row,i)=>{let run=get(row,$('arm').value),s=current(run);return `<article class="card" data-index="${i}"><h2>${(page-1)*12+i+1}. church <small>${esc(row.keyId)}</small></h2><div class="pair"><div><div class="caption">Original</div>${svg(row,null,true)}</div><div><div class="caption">${labels[run.arm]} · pass ${s.pass}</div>${svg(row,s)}</div></div><div class="meta">${s.applied} local rewrites this pass · ${s.moved} actuator moves · ${s.graph.sites.length} sites · ${s.components} components<br>Mapping strain ${num(average(s.errors,'mapping'))} · source strain ${num(average(s.errors,'source'))}</div></article>`}).join('');for(const card of document.querySelectorAll('.card'))card.onclick=()=>open(Number(card.dataset.index));state();if($('dialog').open)detail()}
function detail(){let row=rows[selected];if(!row)return;$('title').textContent='church · '+row.keyId;$('detailJoints').checked=$('joints').checked;$('detailShapes').checked=$('shapes').checked;$('detailViews').innerHTML=row.results.map(run=>{let s=current(run);return `<div class="view ${run.arm===$('arm').value?'active':''}"><h3>${labels[run.arm]} · pass ${s.pass}</h3>${svg(row,s)}<div class="meta">${s.graph.sites.length} sites · ${s.sourceEdges} source spans · ${s.inferredEdges} inferred<br>${s.applied} rewrites · ${s.moved} actuator moves<br>Mapping ${num(average(s.errors,'mapping'))} · source ${num(average(s.errors,'source'))}</div></div>`}).join('');$('detailMetrics').innerHTML='<table><tr><th>Version</th><th>Mapping strain by pass (same authored mapping family)</th><th>Final native accounting</th></tr>'+row.results.map(run=>{let values=run.stages.map(s=>average(s.errors,'mapping')||0),max=Math.max(.01,...values),s=current(run),x=s.transfers;return `<tr><td>${labels[run.arm]}</td><td><div class="bars">${values.map((v,i)=>`<div class="bar" style="height:${Math.max(1,v/max*65)}px" title="Pass ${i}: ${v.toFixed(4)}"></div>`).join('')}</div></td><td>Accepted ${num(x.reduce((n,r)=>n+r.accepted,0))}<br>Capacity deferred ${num(x.reduce((n,r)=>n+r.deferred,0))} · finite quantum ${num(x.reduce((n,r)=>n+r.quantum,0))}</td></tr>`}).join('')+'</table>';$('archive').href='drawings/'+row.id+'/'+$('arm').value+'.gojo.bin.gz';$('archive').textContent='Native '+labels[$('arm').value]+' archive'}
function open(i){selected=i;detail();if(!$('dialog').open)$('dialog').showModal()}
function load(target){if(loading)return;page=Math.max(1,Math.min(MANIFEST.pages,target));$('page').value=page;loading=true;if(script)script.remove();script=document.createElement('script');script.src='pages/page-'+String(page).padStart(5,'0')+'.js';script.onload=()=>{rows=window.CHURCH_PAGE;loading=false;render()};script.onerror=()=>{loading=false;$('scope').textContent='This page is not available yet. Reload after the run progresses.'};document.body.append(script)}
function drawing(delta){let n=selected+delta;if(n>=0&&n<rows.length){selected=n;detail()}else if(n<0&&page>1){selected=11;load(page-1)}else if(n>=rows.length&&page<MANIFEST.pages){selected=0;load(page+1)}}
function stage(delta){$('step').value=Math.max(0,Math.min(Number($('step').max),Number($('step').value)+delta));render()}
$('prev').onclick=()=>load(page-1);$('next').onclick=()=>load(page+1);$('page').onchange=()=>load(Number($('page').value));$('arm').onchange=render;$('step').oninput=render;$('joints').onchange=render;$('shapes').onchange=render;$('detailJoints').onchange=()=>{$('joints').checked=$('detailJoints').checked;render()};$('detailShapes').onchange=()=>{$('shapes').checked=$('detailShapes').checked;render()};$('close').onclick=()=>$('dialog').close();$('drawingPrev').onclick=()=>drawing(-1);$('drawingNext').onclick=()=>drawing(1);$('stagePrev').onclick=()=>stage(-1);$('stageNext').onclick=()=>stage(1);
document.addEventListener('keydown',e=>{if(['INPUT','SELECT','TEXTAREA'].includes(e.target.tagName))return;if(e.key==='ArrowLeft'||e.key==='ArrowRight'){e.preventDefault();let d=e.key==='ArrowLeft'?-1:1;e.shiftKey?load(page+d):$('dialog').open?drawing(d):load(page+d)}if(e.key==='ArrowUp'||e.key==='ArrowDown'){e.preventDefault();stage(e.key==='ArrowUp'?-1:1)}});
$('status').textContent=`${MANIFEST.drawings}/${MANIFEST.selected} drawings · ${MANIFEST.state}${MANIFEST.failures?' · '+MANIFEST.failures+' errors':''}`;$('page').innerHTML=Array.from({length:MANIFEST.pages},(_,i)=>`<option value="${i+1}">${i+1} / ${MANIFEST.pages}</option>`).join('');$('table').innerHTML='<table><tr><th>Version</th><th>Drawings</th><th>Local rewrites</th><th>Mapping strain</th><th>Source strain</th><th>Later-pass rewrites</th></tr>'+Object.entries(MANIFEST.summary).map(([arm,m])=>`<tr><td>${labels[arm]}</td><td>${m.drawings}</td><td>${num(m.applied)}</td><td>${num(m.mappingStrain)}</td><td>${num(m.sourceStrain)}</td><td>${num(m.changesAfterFirst)}</td></tr>`).join('')+'</table>';load(page);
</script></html>'''
