"""Portable inspection of actual native neighborhood graph readbacks."""
import html
import json


def write(output,title,summary,drawings):
    data=json.dumps(drawings,separators=(',',':'),allow_nan=False).replace('<','\\u003c')
    document='''<!doctype html><html lang="en"><meta charset="utf-8"><title>__TITLE__</title>
<style>
*{box-sizing:border-box}body{font:15px system-ui;color:#183f49;background:#f1f5f0;margin:18px}button,select{font:inherit;padding:7px 10px;background:white;border:1px solid #bacbc4;border-radius:6px;color:inherit;cursor:pointer}header{display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin-bottom:12px}h1{font-size:24px;margin:0}p{line-height:1.5}details{margin:8px 0}#grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}.card{background:white;border:1px solid #c6d6cf;border-radius:12px;padding:12px;cursor:pointer}.pair{display:grid;grid-template-columns:1fr 1fr;gap:8px}.label{font-size:12px;color:#617c82}svg{width:100%;height:195px}.key{font-weight:600;font-size:13px;margin-bottom:8px}dialog{width:min(1250px,95vw);max-height:95vh;border:1px solid #b8cac1;border-radius:12px;color:inherit;padding:18px}dialog svg{height:51vh}dialog::backdrop{background:#15333599}pre{font:12px ui-monospace;white-space:pre-wrap;overflow-wrap:anywhere}.hint{font-size:12px;color:#617c82}@media(max-width:900px){#grid{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:600px){#grid{grid-template-columns:1fr}}
</style><header><h1>__TITLE__</h1><button id="prev">← Page</button><span id="page"></span><button id="next">Page →</button><label>Stage <select id="stage"></select></label><label><input type="checkbox" id="joints" checked>Joints</label></header>
<details><summary>Experiment and results</summary>__SUMMARY__<p class="hint">The left panel is the source fit; the right is an independently restored interpreted graph. Thin gray ink keeps the source visible. Colored points are actual shared sites. Hover for edge and far-endpoint references. Model scores are relative support. A kept or unresolved proposal is not a confirmed negative.</p></details>
<div id="grid"></div><dialog id="detail"><header><strong id="name"></strong><button id="back">← Drawing</button><button id="forward">Drawing →</button><select id="detailStage" aria-label="Drawing pass"></select><button id="close">Close</button></header><div id="large"></div><details open><summary>Local decisions and alternatives</summary><div id="decisions"></div></details><p class="hint">← / → drawing · ↑ / ↓ pass · Shift + ← / → page</p></dialog>
<script>
const data=__DATA__;let offset=0,chosen=0,stage=2;const $=s=>document.getElementById(s),esc=v=>String(v).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const maxStage=Math.max(0,...data.map(d=>d.stages.length-1));stage=maxStage;for(let i=0;i<=maxStage;i++)for(const id of ['stage','detailStage'])$(id).add(new Option(i===0?'Source fitted graph':`Neighborhood pass ${i}`,i));$('stage').value=$('detailStage').value=stage;
function path(c){if(c.length===2)return `M${c[0]} L${c[1]}`;if(c.length===3)return `M${c[0]} Q${c[1]} ${c[2]}`;if(c.length===4)return `M${c[0]} C${c[1]} ${c[2]} ${c[3]}`;return 'M'+c.join(' L')}
function svg(d,g){const all=[...d.initial.curves.flat(),...g.curves.flat()],xs=all.map(p=>p[0]),ys=all.map(p=>p[1]);if(!all.length)return '<svg></svg>';const x=Math.min(...xs)-15,y=Math.min(...ys)-15,w=Math.max(...xs)-x+15,h=Math.max(...ys)-y+15,sw=Math.max(w,h)/230;
let ink=d.initial.curves.map(c=>`<path d="${path(c)}" fill="none" stroke="#bbc8c3" stroke-width="${sw*.7}"/>`).join('');ink+=g.curves.map(c=>`<path d="${path(c)}" fill="none" stroke="#267e73" stroke-width="${sw}"/>`).join('');
if($('joints').checked)ink+=g.sites.filter(s=>s.degree>1).map(s=>`<g><title>${esc(s.kind+' '+s.id+'; edges '+s.edges.join(', ')+'; far endpoints '+s.farEndpoints.join(', '))}</title><circle cx="${s.point[0]}" cy="${s.point[1]}" r="${sw*2.1}" fill="white" stroke="${s.kind==='T'?'#258c85':s.kind==='X'?'#be4683':'#bd771e'}" stroke-width="${sw*.8}"/><text x="${s.point[0]+sw*3}" y="${s.point[1]-sw*3}" font-size="${sw*8}" fill="#304f53">${esc(s.kind)}</text></g>`).join('');
return `<svg viewBox="${x} ${y} ${w} ${h}">${ink}</svg>`}
function pair(d){const selected=d.stages[Math.min(stage,d.stages.length-1)];return `<div class="pair"><div><div class="label">Source fitted graph</div>${svg(d,d.initial)}</div><div><div class="label">${esc(selected.title)}</div>${svg(d,selected.graph)}</div></div>`}
function render(){const grid=$('grid');grid.innerHTML='';$('stage').value=$('detailStage').value=stage;data.slice(offset,offset+12).forEach((d,j)=>{const a=document.createElement('article');a.className='card';a.innerHTML=`<div class="key">${offset+j+1}. ${esc(d.sourceKey||d.kind)}</div>${pair(d)}<div class="hint">${d.accepted} applied local changes · click to inspect</div>`;a.onclick=()=>open(offset+j);grid.appendChild(a)});$('page').textContent=`${Math.floor(offset/12)+1} / ${Math.max(1,Math.ceil(data.length/12))}`;$('prev').disabled=offset===0;$('next').disabled=offset+12>=data.length;if($('detail').open)detail()}
function detail(){const d=data[chosen];$('name').textContent=d.sourceKey||d.kind;$('large').innerHTML=pair(d);$('decisions').innerHTML=d.decisions.filter(r=>r.pass<=stage).map(r=>`<details><summary>Pass ${r.pass}: ${esc(r.operation)} — ${esc(r.status)}</summary><p class="hint">Repair ${Math.round(r.repairSupport*100)}% · straight ${Math.round(r.lineSupport*100)}% · ${r.neighborhoodSites} sites / ${r.neighborhoodEdges} edges · radius ${r.radius.toFixed(2)} / reach ${r.reach.toFixed(2)}</p><pre>${esc(JSON.stringify({sources:r.sources,removedIntervals:r.removedIntervals,issues:r.issues,alternatives:r.alternatives},null,2))}</pre></details>`).join('')||'<p>No local changes at this stage.</p>'}
function open(i){chosen=Math.max(0,Math.min(data.length-1,i));detail();if(!$('detail').open)$('detail').showModal()}
function page(delta){offset=Math.max(0,Math.min(Math.max(0,Math.floor((data.length-1)/12)*12),offset+delta*12));render()}
$('prev').onclick=()=>page(-1);$('next').onclick=()=>page(1);$('back').onclick=()=>open(chosen-1);$('forward').onclick=()=>open(chosen+1);$('close').onclick=()=>$('detail').close();for(const id of ['stage','detailStage'])$(id).onchange=()=>{stage=+$(id).value;render()};$('joints').onchange=render;
document.addEventListener('keydown',e=>{if(e.target.matches('input,select,textarea'))return;if(e.key==='ArrowUp'||e.key==='ArrowDown'){e.preventDefault();stage=Math.max(0,Math.min(maxStage,stage+(e.key==='ArrowDown'?1:-1)));render()}else if(e.key==='ArrowLeft'||e.key==='ArrowRight'){e.preventDefault();const n=e.key==='ArrowRight'?1:-1;if(e.shiftKey){page(n);if($('detail').open)open(Math.max(0,Math.min(data.length-1,chosen+n*12)))}else if(!$('detail').open)page(n);else open(chosen+n)}});render();
</script></html>'''
    document=document.replace('__TITLE__',html.escape(title)).replace('__SUMMARY__',summary).replace('__DATA__',data)
    (output/'report.html').write_text(document,encoding='utf-8')


def training_report(output,report,examples):
    summary='<pre>'+html.escape(json.dumps({k:report[k] for k in ('sourceDrawings','controlledNeighborhoods','parameterCount','lineTest','repairTest','realWeakTest','graphTest','qualification')},indent=2))+'</pre>'
    write(output,'Neighborhood models — held-out repairs',summary,examples)


def encoding_report(output,state):
    drawings=[json.loads((output/row['path']).read_text(encoding='utf-8')) for row in state['drawings']]
    summary='<p>'+html.escape(f'{state["sourceDrawings"]} drawings; {state["changedPatches"]} applied local changes. {state["qualification"]}')+'</p>'
    write(output,'Neighborhood consolidation',summary,drawings)
