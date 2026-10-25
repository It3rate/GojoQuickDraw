"""Portable baseline evidence inspector. Geometry here is an adapter read.

The review never repairs source geometry or infers contact from proximity.
Observed profiles and seeded expectations have separate visual treatments.
"""
from __future__ import annotations

import json
import hashlib
import os
from pathlib import Path
from urllib.parse import quote


def build(dataset: Path, output: Path, limit: int = 50, *, order="category", seed=0,
          paged=False, offset=0, pagination=None) -> dict:
    """Embed a bounded sample of cached records in one offline HTML document."""
    from .dataset import load_records
    from .object_display import read as object_read

    if isinstance(limit, bool) or not isinstance(limit, int) or limit < 1:
        raise ValueError("Review limit must be a positive integer")
    dataset, output = Path(dataset).resolve(), Path(output).resolve()
    if paged:
        return _pages(dataset, output, limit, order, seed)
    records = [{"index": row, "packet": packet}
               for row, packet in load_records(dataset, limit=limit, order=order, seed=seed,
                                                **({"offset": offset} if offset else {}))]
    if not records:
        raise ValueError("No successfully encoded drawings to review")
    if output.exists() and output.is_dir():
        raise ValueError("Review output must name an HTML file")
    for record in records:
        record["objectView"] = object_read(record["packet"])
        # A review destination can never replace a cached source or archive.
        for key in ("evidencePath", "nativePath"):
            path = record["index"].get(key)
            folder = Path(record["index"].get("datasetPath", dataset))
            if path and output == (folder / path).resolve():
                raise ValueError("Review output cannot overwrite a dataset record")
    if output in ((dataset / "manifest.json").resolve(), (dataset / "index.ndjson").resolve()):
        raise ValueError("Review output cannot overwrite the dataset index")
    if output.suffix.lower() not in (".html", ".htm"):
        raise ValueError("Review output must have an .html or .htm extension")
    data = {"schema": "gojo.quickdraw-dataset-review.v1", "records": records,
            "pagination": pagination,
            "qualification": "Fitted profiles and measured annotation reads are shown beside explicitly seeded hypotheses. Proximity and equal coordinates do not establish shared native identity."}
    encoded = json.dumps(data, ensure_ascii=False, separators=(",", ":"), allow_nan=False)
    encoded = encoded.replace("<", "\\u003c").replace("\u2028", "\\u2028").replace("\u2029", "\\u2029")
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(output.suffix + ".tmp")
    temporary.write_text(_HTML.replace("__DATA__", encoded), encoding="utf-8")
    temporary.replace(output)
    return {"drawings": len(records), "output": str(output), "htmlBytes": output.stat().st_size,
            "portable": True, "externalRequests": 0}


def collection_stamp(dataset):
    """Identify the journals behind a presentation without changing their evidence."""
    from .selection import datasets
    return [[str(folder), (folder / "index.ndjson").stat().st_size,
             (folder / "index.ndjson").stat().st_mtime_ns] for folder, _ in datasets(dataset)]


def _pages(dataset, output, size, order, seed):
    """Publish small offline documents; the entry page changes only after all exist."""
    from .selection import catalog, selected_rows
    if output.suffix.lower() not in (".html", ".htm"):
        raise ValueError("Review output must have an .html or .htm extension")
    selected = catalog(dataset, 1, order=order, seed=seed)
    total = selected["storedRecords"]
    if not total:
        raise ValueError("No successfully encoded drawings to review")
    stamps = collection_stamp(dataset)
    identity = hashlib.sha256(json.dumps([stamps, size, order, seed]).encode()).hexdigest()[:16]
    directory = output.parent / (output.stem + "-pages") / identity
    count = (total + size - 1) // size
    paths = [output] + [directory / f"page-{i+1:05}.html" for i in range(1, count)]
    bytes_written = 0
    # The existing entry remains usable if a later page fails or work is stopped.
    for number in [*range(1, count), 0]:
        path = paths[number]
        pages = [quote(os.path.relpath(target, path.parent).replace(os.sep, "/"), safe="/.-_") for target in paths]
        nav = {"page": number + 1, "pageSize": size, "pages": pages, "total": total,
               "start": number * size + 1, "end": min(total, (number + 1) * size),
               "order": order, "seed": seed}
        result = build(dataset, path, size, order=order, seed=seed, offset=number * size, pagination=nav)
        bytes_written += result["htmlBytes"]
    # A small map lets comparison galleries link to the exact page without
    # loading every full evidence packet into the gallery or its browser.
    from .dataset import _atomic_json
    links = [{"selectionId": row["selectionId"], "itemId": row["itemId"], "page": i // size}
             for i, (_, row) in enumerate(selected_rows(dataset, limit=total, order=order, seed=seed))]
    _atomic_json(output.with_suffix(".pages.json"), {
        "schema": "gojo.quickdraw-review-pages.v1", "dataset": str(dataset),
        "journals": stamps, "pageSize": size,
        "pages": [quote(os.path.relpath(path, output.parent).replace(os.sep, "/"), safe="/.-_") for path in paths],
        "records": links})
    return {"drawings": total, "pages": count, "pageSize": size, "output": str(output),
            "htmlBytes": bytes_written, "portable": True, "externalRequests": 0}


_HTML = r'''<!doctype html>
<html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Gojo QuickDraw · Evidence</title>
<style>
:root{color-scheme:light;--ink:#183b38;--muted:#637770;--paper:#f1f5f0;--line:#d9e3dd;--green:#287b6f;--seed:#b26a26;--joint:#8864b5;--mirror:#b54d83}*{box-sizing:border-box}body{margin:0;background:var(--paper);color:var(--ink);font:15px/1.5 system-ui,sans-serif}header,main{max-width:1700px;margin:auto;padding:24px 30px}header{padding-bottom:10px}h1{font-size:28px;margin:0 0 6px}h2{font-size:16px;margin:0}p{margin:8px 0}.subtle{color:var(--muted);font-size:13px}.toolbar,.layers,.legend{display:flex;gap:12px;align-items:center;flex-wrap:wrap}.toolbar{padding:15px 0}.toolbar select{max-width:65vw}button,select{font:inherit;border:1px solid var(--line);border-radius:7px;background:white;padding:7px 10px;color:var(--ink)}button{cursor:pointer}button:disabled{opacity:.4;cursor:default}button:focus-visible,select:focus-visible,[tabindex]:focus-visible{outline:3px solid #dcac47;outline-offset:2px}.layout{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr) 330px;gap:16px;align-items:start}.card{background:white;border:1px solid var(--line);border-radius:12px;overflow:hidden;min-width:0}.cardhead{padding:13px 16px;border-bottom:1px solid var(--line)}svg{width:100%;height:auto;display:block;background:#fff;touch-action:manipulation}.detail{padding:15px;height:620px;min-height:480px;max-height:680px;overflow:auto}.badge{display:inline-block;font-size:12px;padding:3px 7px;border-radius:6px;background:#edf6f2;margin-bottom:10px}.badge.seed{color:#8d5019;background:#fff1de}.legend{font-size:13px;padding:8px 0 16px}.dot{display:inline-block;width:10px;height:10px;border-radius:50%;margin-right:5px}.details-title{font-size:18px;margin:0 0 8px}.metric{padding:7px 0;border-bottom:1px solid #edf0ee;overflow-wrap:anywhere}.metric strong{font-weight:550}.value{color:#61736e}.listcard{margin-top:16px}.evidence-list{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:6px;padding:12px;max-height:340px;overflow:auto}.evidence-list button{text-align:left;font-size:13px;padding:8px;overflow-wrap:anywhere}.evidence-list button.active{background:#ecf5e9;border-color:#7aaa85}.tiny{font-size:11px;color:var(--muted)}.listhead{display:flex;justify-content:space-between;align-items:center;gap:10px}.qualify{padding:12px 0;color:var(--muted);font-size:13px;max-width:1200px}input[type=range]{width:150px;accent-color:var(--green)}input[type=checkbox]{accent-color:var(--green)}.layers label{white-space:nowrap;font-size:13px}.bottom{display:flex;justify-content:space-between;gap:10px;align-items:center;flex-wrap:wrap;padding-top:16px}pre{white-space:pre-wrap;overflow-wrap:anywhere;font-size:11px;background:#f5f7f5;padding:10px;border-radius:6px}.summary{padding:10px 16px;border-top:1px solid var(--line);font-size:12px;color:var(--muted)}@media(max-width:1200px){.layout{grid-template-columns:1fr 1fr}.detail{grid-column:1/-1;height:300px;min-height:160px;max-height:350px}.evidence-list{grid-template-columns:1fr 1fr}}@media(max-width:650px){header,main{padding:16px 12px}.layout{grid-template-columns:1fr}.evidence-list{grid-template-columns:1fr}.toolbar{gap:6px}.toolbar select{max-width:75vw}.detail{height:360px;max-height:450px}.layers{gap:8px}h1{font-size:24px}}
</style>
<header><div class="subtle">GOJO / QUICKDRAW</div><h1>From ink to evidence</h1>
<p class="subtle">Inspect the acquired drawing, fitted intervals, measured relationships, and explicitly seeded expectations.</p>
<div class="toolbar"><button id="prev" aria-label="Previous drawing">←</button><select id="drawing" aria-label="Drawing"></select><button id="next" aria-label="Next drawing">→</button><span id="position" class="subtle"></span></div>
<div class="toolbar" id="pageControls" hidden><button id="firstPage">First page</button><button id="prevPage">Previous page</button><label>Page <select id="page" aria-label="Page"></select></label><button id="nextPage">Next page</button><button id="lastPage">Last page</button><span id="pageSummary" class="subtle"></span></div>
<div class="toolbar"><label>Read <select id="readMode" aria-label="Evidence view"><option value="observed-fit">Fitted source</option></select></label><label><input type="checkbox" id="directionLayer"> Start / end and direction</label><span id="modeNote" class="subtle"></span></div>
<div class="layers"><label><input type="checkbox" id="rawOverlay"> Ink under fit</label><label><input type="checkbox" id="jointLayer" checked> Joints</label><label><input type="checkbox" id="mirrorLayer"> Symmetry</label><label><input type="checkbox" id="seedLayer"> Seeded shapes</label><label><input type="checkbox" id="orderColor"> Stroke colors</label><label>Draw order <input id="strokeLimit" type="range" min="1" value="1"><span id="strokeValue"></span></label></div>
</header>
<main><div id="evidenceLegend" class="legend"><span><i class="dot" style="background:#9ca9a4"></i>Acquired ink</span><span><i class="dot" style="background:var(--green)"></i>Fitted source</span><span><i class="dot" style="background:var(--joint)"></i>Joint candidate</span><span><i class="dot" style="background:var(--mirror)"></i>Reflection read</span><span><i class="dot" style="background:var(--seed)"></i>Seeded expectation · dashed</span></div>
<div id="layoutLegend" class="legend" style="display:none"></div>
<section id="objectPanel" class="card" hidden style="margin-bottom:16px"><div class="cardhead"><div class="layers"><h2>Church object candidates</h2><label><input id="objectColor" type="checkbox" checked> Color by provisional role</label><span id="objectSummary" class="subtle"></span></div><div id="objectLegend" class="legend" style="padding:8px 0 0"></div><p class="subtle">Select a candidate to inspect its source intervals and related parts. Gray = unknown or competing roles. Copies keep separate identities; building assemblies can share a wall’s outline.</p></div><div id="objectList" class="evidence-list" style="max-height:180px"></div></section>
<div class="layout"><section class="card"><div class="cardhead"><h2>Original drawing</h2></div><svg id="raw" viewBox="0 0 512 512" role="img" aria-label="Acquired strokes in drawing order"></svg><div id="sourceStats" class="summary"></div></section>
<section class="card"><div class="cardhead"><h2 id="fitTitle">Fitted evidence and hypotheses</h2></div><svg id="fit" viewBox="0 0 512 512" role="img" aria-label="Fitted evidence with selectable annotations"></svg><div id="fitStats" class="summary"></div></section>
<aside class="card detail" id="detail" aria-live="polite"></aside></div>
<section class="card listcard"><div class="cardhead listhead"><h2>Evidence and open questions</h2><select id="filter" aria-label="Evidence kind"><option value="all">All evidence</option><option value="lines">Lines and arcs</option><option value="joints">Joints</option><option value="symmetries">Symmetry</option><option value="relations">Other measured relations</option><option value="objects">Church object candidates</option><option value="parts">Part hypotheses</option><option value="contextRelations">Containment and part relations</option><option value="claims">Dependent opinions / object groups</option><option value="proposals">Current interpretation profiles</option><option value="assumptions">Open assumptions</option><option value="views">View candidates</option></select></div><div id="list" class="evidence-list"></div></section>
<div class="bottom"><span id="counts" class="subtle"></span><div><button id="download">Download evidence JSON</button> <button id="downloadSource">Download original JSON</button></div></div>
<p class="qualify">Measured proximity, crossing, closure, and symmetry are candidate reads of the fitted source. They do not merge acquisition identities. Dashed constructions are seeded expectations, including proposed hidden support; they are never additional observed ink. View, gravity, scale, and part labels can remain unresolved. Later interpretation stages are generated separately, on demand.</p>
</main>
<script id="dataset" type="application/json">__DATA__</script>
<script>
'use strict';
const DATA=JSON.parse(document.getElementById('dataset').textContent), NS='http://www.w3.org/2000/svg', $=id=>document.getElementById(id);
const PAGING=DATA.pagination;
let requestedMode=new URL(location.href).searchParams.get('view')||'observed-fit';
$('objectColor').checked=new URL(location.href).searchParams.get('objects')!=='0';
function goPage(i){if(PAGING&&i>=0&&i<PAGING.pages.length){const url=new URL(PAGING.pages[i],location.href);url.searchParams.set('view',requestedMode);url.searchParams.set('objects',$('objectColor').checked?'1':'0');location.href=url.href}}
function rememberView(){const url=new URL(location.href);url.searchParams.set('view',requestedMode);url.searchParams.set('objects',$('objectColor').checked?'1':'0');url.hash=encodeURIComponent(DATA.records[index].index.itemId);history.replaceState(null,'',url.href)}
if(PAGING){$('pageControls').hidden=false;$('pageControls').style.display='flex';PAGING.pages.forEach((_,i)=>{const o=document.createElement('option');o.value=i;o.textContent=(i+1)+' of '+PAGING.pages.length;$('page').append(o)});$('page').value=String(PAGING.page-1);$('page').onchange=()=>goPage(Number($('page').value));$('firstPage').onclick=()=>goPage(0);$('prevPage').onclick=()=>goPage(PAGING.page-2);$('nextPage').onclick=()=>goPage(PAGING.page);$('lastPage').onclick=()=>goPage(PAGING.pages.length-1);$('firstPage').disabled=$('prevPage').disabled=PAGING.page===1;$('lastPage').disabled=$('nextPage').disabled=PAGING.page===PAGING.pages.length;$('pageSummary').textContent=`Drawings ${PAGING.start}–${PAGING.end} of ${PAGING.total} · ${PAGING.pageSize} per page`}
else $('pageControls').style.display='none';
let index=0, selected=null, entries=[];
const COLORS={lines:'#287b6f',joints:'#8864b5',symmetries:'#b54d83',relations:'#4a86a7',parts:'#b26a26',contextRelations:'#b26a26',assumptions:'#b26a26',views:'#b26a26',claims:'#526fb1',proposals:'#b26a26'};
function node(tag,attrs={},parent=null){const n=document.createElementNS(NS,tag);for(const [k,v]of Object.entries(attrs))n.setAttribute(k,String(v));if(parent)parent.append(n);return n}
function text(parent,tag,value,cls=''){const n=document.createElement(tag);n.textContent=value;if(cls)n.className=cls;parent.append(n);return n}
function pointAt(c,t){let a=c.map(p=>[...p]);while(a.length>1)a=a.slice(1).map((p,i)=>p.map((v,k)=>v*t+a[i][k]*(1-t)));return a[0]}
function curve(c){if(!c.length)return '';const start=`M${c[0].join(' ')}`;if(c.length===2)return `${start} L${c[1].join(' ')}`;if(c.length===3)return `${start} Q${c.slice(1).flat().join(' ')}`;if(c.length===4)return `${start} C${c.slice(1).flat().join(' ')}`;return start+' '+Array.from({length:32},(_,i)=>'L'+pointAt(c,(i+1)/32).join(' ')).join(' ')}
function packet(){return DATA.records[index].packet}
function objectView(){return DATA.records[index].objectView||{available:false,colors:{},spans:{}}}
function objectColor(ids,fallback){if(!$('objectColor').checked||!objectView().available)return fallback;const roles=ids.map(id=>objectView().spans[id]?.role||'unknown');return objectView().colors[roles.length&&new Set(roles).size===1?roles[0]:'unknown']}
function objectEntries(){return entries.filter(e=>e.group==='objects')}
function objectLinks(host,ids,title){if(!ids?.length)return;text(host,'h4',title);for(const id of ids){const e=objectEntries().find(e=>e.row.id===id)||entries.find(e=>e.group==='parts'&&e.row.id===id);const b=text(host,'button',e?label(e):id);b.style.margin='2px';if(e)b.onclick=()=>choose(e);else b.disabled=true}}
function objectsPanel(){const view=objectView();$('objectPanel').hidden=!view.available;if(!view.available)return;$('objectSummary').textContent=`${view.candidates} candidates · ${view.unknown} unassigned intervals · ${view.ambiguous} ambiguous intervals`;$('objectLegend').replaceChildren();for(const [role,color]of Object.entries(view.colors)){const item=text($('objectLegend'),'span','');const dot=text(item,'i','','dot');dot.style.background=color;text(item,'span',role==='unknown'?'unknown / ambiguous':role)}const host=$('objectList');host.replaceChildren();for(const e of objectEntries().filter(visible)){const b=text(host,'button','',selected===e?'active':'');b.dataset.object=e.row.id;const dot=text(b,'i','','dot');dot.style.background=view.colors[e.row.role]||view.colors.unknown;text(b,'span',label(e));text(b,'div',`${spansOf(e).length} intervals · ${e.row.status||'candidate'}${e.row.parentIds?.length?' · '+e.row.parentIds.length+' parent candidate(s)':''}`,'tiny');b.onclick=()=>choose(e);b.onmouseenter=()=>detail(e)}}
function annotation(p){const mode=$('readMode').value,a=p.analysis?.annotationModes?.[mode]||p.annotations||{};if(a.lines)return a;if(a['observed-fit'])return a['observed-fit'];return Object.values(a).find(v=>v&&v.lines)||{}}
function proposalStep(){const mode=$('readMode').value;return mode.startsWith('step:')?packet().analysis?.proposal?.steps?.[Number(mode.slice(5))]:null}
function modes(){const select=$('readMode');select.replaceChildren();const choices=[['observed-fit','Fitted source']];if(packet().analysis){choices.push(['gentle-priors','Looser source relations'],['stronger-priors','Strong interpretive relations']);(packet().analysis.proposal?.steps||[]).forEach((s,i)=>choices.push(['step:'+i,`Step ${i+1}: ${s.title}`]))}if(packet().layout)choices.push(['layout','Area estimates over original']);for(const [value,title]of choices){const o=text(select,'option',title);o.value=value}select.value=choices.some(c=>c[0]===requestedMode)?requestedMode:'observed-fit'}
function label(e){return e.row.label||e.row.kind||e.row.id||e.group}
function spansOf(e){return e.row.spans||e.row.sourceSpanIds||[]}
function seeded(e){return ['parts','objects','contextRelations','assumptions','views','proposals'].includes(e.group)||(e.group==='claims'&&e.row.context!=='observations')}
function gather(){const p=packet(),a=annotation(p),ctx=p.context||{};entries=[];if($('readMode').value==='layout'){for(const row of ctx.parts||[])if(row.id.startsWith('layout-'))entries.push({group:'parts',row:{...row,label:row.id.replace(/^layout-/,'').replaceAll('-',' ')+(row.shape?' / '+row.shape:'')}});for(const row of ctx.relations||[])if(row.id.startsWith('layout-'))entries.push({group:'contextRelations',row});entries.forEach((e,i)=>e.key=String(i));return}for(const g of ['lines','joints','symmetries','relations'])for(const row of a[g]||[])entries.push({group:g,row});for(const g of ['parts','assumptions','views'])for(const row of ctx[g]||[])if(!row.id.startsWith('layout-'))entries.push({group:g,row});for(const row of ctx.relations||[])if(!row.id.startsWith('layout-'))entries.push({group:'contextRelations',row});for(const row of p.opinions?.claims||[])entries.push({group:'claims',row});const ordinal={};for(const row of p.objects?.candidates||[]){ordinal[row.role]=(ordinal[row.role]||0)+1;entries.push({group:'objects',row:{...row,label:row.role+' '+ordinal[row.role]}})}for(const row of proposalStep()?.edges||[])entries.push({group:'proposals',row:{...row,label:row.primitive?.name||row.id,qualification:'Cached authored proposal, not an executed native rung. Use Drawing steps for native replay.'}});entries.forEach((e,i)=>e.key=String(i))}
function supportCenter(e){if(Array.isArray(e.row.point))return e.row.point;if(e.row.bounds){const b=e.row.bounds;return [(b[0]+b[2])/2,(b[1]+b[3])/2]}const spans=packet().observation.spans.filter(s=>spansOf(e).includes(s.id));if(spans.length){const ps=spans.map(s=>pointAt(s.controls,.5));return [0,1].map(j=>ps.reduce((s,p)=>s+p[j],0)/ps.length)}return null}
function bind(n,e){n.style.cursor='pointer';n.setAttribute('tabindex','0');n.setAttribute('role','button');n.setAttribute('aria-label',(seeded(e)?'Seeded: ':'Measured: ')+label(e));const title=node('title',{},n);title.textContent=label(e)+' · '+(e.row.qualification||e.row.status||e.group);n.addEventListener('click',()=>choose(e));n.addEventListener('mouseenter',()=>detail(e));n.addEventListener('focus',()=>detail(e));n.addEventListener('keydown',ev=>{if(ev.key==='Enter'||ev.key===' '){ev.preventDefault();choose(e)}})}
function currentSpans(){const max=Number($('strokeLimit').value);return packet().observation.spans.filter(s=>s.strokeIndex<max)}
function visible(e){if($('readMode').value==='layout')return true;const ids=new Set(currentSpans().map(s=>s.id));return spansOf(e).every(id=>ids.has(id))}
function drawInk(svg,opacity=1){const max=Number($('strokeLimit').value);for(const s of packet().observation.strokes.filter(s=>s.index<max)){const color=$('orderColor').checked?`hsl(${s.index*61%360} 45% 44%)`:'#9ca9a4';node('polyline',{'data-stroke':s.id,points:s.points.map(p=>p.join(',')).join(' '),fill:'none',stroke:color,'stroke-width':2.3,'stroke-linecap':'round','stroke-linejoin':'round',opacity},svg)}}
function drawEntry(svg,e,active=false){const color=e.group==='objects'?(objectView().colors[e.row.role]||'#92999e'):COLORS[e.group],group=node('g',{'data-entry':e.key},svg),r=e.row,seed=seeded(e);if(r.axis&&r.axis.from&&r.axis.to)node('line',{x1:r.axis.from[0],y1:r.axis.from[1],x2:r.axis.to[0],y2:r.axis.to[1],stroke:color,'stroke-width':active?2:1.2,'stroke-dasharray':'6 5'},group);const geo=r.geometry;if(seed&&geo&&Array.isArray(geo.points)){for(const pair of geo.segments||[])if(geo.points[pair[0]]&&geo.points[pair[1]])node('line',{x1:geo.points[pair[0]][0],y1:geo.points[pair[0]][1],x2:geo.points[pair[1]][0],y2:geo.points[pair[1]][1],stroke:color,'stroke-width':active?2.5:1.5,'stroke-dasharray':'6 4'},group)}else if(seed&&r.bounds){const b=r.bounds;node('rect',{x:b[0],y:b[1],width:Math.max(0,b[2]-b[0]),height:Math.max(0,b[3]-b[1]),fill:'none',stroke:color,'stroke-width':1.5,'stroke-dasharray':'6 4'},group)}const p=supportCenter(e);if(p){node(seed?'rect':'circle',seed?{x:p[0]-4,y:p[1]-4,width:8,height:8,fill:'white',stroke:color,'stroke-width':2}:{cx:p[0],cy:p[1],r:active?6:4,fill:'white',stroke:color,'stroke-width':2},group);if(e.group==='joints'){const t=node('text',{x:p[0]+7,y:p[1]-6,fill:color,'font-size':11,'paint-order':'stroke',stroke:'white','stroke-width':3},group);t.textContent=r.label}}bind(group,e)}
function direction(svg,c){if(!c.length)return;const a=c[0],b=c[c.length-1],p=pointAt(c,.48),q=pointAt(c,.57),theta=Math.atan2(q[1]-p[1],q[0]-p[0]);node('circle',{cx:a[0],cy:a[1],r:3.4,fill:'#238b61',stroke:'white','stroke-width':1},svg);node('rect',{x:b[0]-3,y:b[1]-3,width:6,height:6,fill:'#bf5b36',stroke:'white','stroke-width':1},svg);if(Math.hypot(q[0]-p[0],q[1]-p[1])>.01){const points=[q,[q[0]-7*Math.cos(theta-.5),q[1]-7*Math.sin(theta-.5)],[q[0]-7*Math.cos(theta+.5),q[1]-7*Math.sin(theta+.5)]];node('polygon',{points:points.map(p=>p.join(',')).join(' '),fill:'#365e7e'},svg)}}
function layoutShape(svg,e){const r=e.row,g=r.geometry;if(!g)return;const color=r.color||'#8d7961',host=node('g',{'data-layout':r.id},svg);if(g.fillIndices)node('polygon',{points:g.fillIndices.map(i=>g.points[i].join(',')).join(' '),fill:color,'fill-opacity':.07,stroke:'none'},host);for(const [a,b]of g.segments||[])node('line',{x1:g.points[a][0],y1:g.points[a][1],x2:g.points[b][0],y2:g.points[b][1],stroke:color,'stroke-width':selected===e?3:2,'stroke-dasharray':'2 5','stroke-linecap':'round'},host);bind(host,e)}
function renderLayout(){const raw=$('raw'),fit=$('fit'),p=packet();raw.replaceChildren();fit.replaceChildren();const parts=p.layout.parts||[],points=[...p.observation.strokes.flatMap(s=>s.points),...parts.flatMap(p=>p.geometry.points)];const xs=points.map(p=>p[0]),ys=points.map(p=>p[1]),l=Math.min(0,...xs),t=Math.min(0,...ys),r=Math.max(512,...xs),b=Math.max(512,...ys),pad=.035*Math.max(r-l,b-t);for(const svg of [raw,fit])svg.setAttribute('viewBox',[l-pad,t-pad,r-l+pad*2,b-t+pad*2].join(' '));for(const e of entries.filter(e=>parts.some(p=>p.id===e.row.id)||e===selected&&e.row.kind==='area-layout-alternative'))layoutShape(fit,e);for(const svg of [raw,fit])for(const s of p.observation.strokes)node('polyline',{'data-stroke':s.id,points:s.points.map(p=>p.join(',')).join(' '),fill:'none',stroke:'#465852','stroke-width':2.1,'stroke-linecap':'round','stroke-linejoin':'round','pointer-events':'none'},svg);$('objectPanel').hidden=true;$('fitTitle').textContent='Area estimates over original';$('fitStats').textContent=parts.length+' proposed areas / levels · original ink always visible';$('modeNote').textContent='Pale fill + dotted outline = ideal estimate · solid ink = original';$('strokeValue').textContent='All';list();if(selected)detail(selected)}
function render(){
 const area=$('readMode').value==='layout';$('evidenceLegend').style.display=area?'none':'flex';$('layoutLegend').style.display=area?'flex':'none';if(area){$('layoutLegend').replaceChildren();for(const [role,color]of Object.entries({ground:'#63735f',building:'#277da8',tower:'#56729a',roof:'#bf712a',cross:'#ae3f6d',door:'#23846c',window:'#8c68aa'})){const item=text($('layoutLegend'),'span','');const dot=text(item,'i','','dot');dot.style.background=color;text(item,'span',role)}}for(const id of ['rawOverlay','jointLayer','mirrorLayer','seedLayer','orderColor','directionLayer','strokeLimit'])$(id).disabled=area;if(area){renderLayout();return}for(const id of ['raw','fit'])$(id).setAttribute('viewBox','0 0 512 512');
 const raw=$('raw'),fit=$('fit');raw.replaceChildren();fit.replaceChildren();drawInk(raw);if($('rawOverlay').checked)drawInk(fit,.24);
 const p=packet(),active=selected?new Set(spansOf(selected)):new Set(),step=proposalStep();
 if(objectView().available&&$('objectColor').checked||active.size){for(const s of currentSpans()){const stroke=p.observation.strokes.find(v=>v.index===s.strokeIndex);if(!stroke)continue;const points=stroke.points.slice(s.startIndex,s.endIndex+1).map(v=>v.join(',')).join(' ');if(active.has(s.id))node('polyline',{points,fill:'none',stroke:'#ecc56a','stroke-width':9,opacity:.65},raw);node('polyline',{'data-object-span':s.id,points,fill:'none',stroke:objectColor([s.id],'#9ca9a4'),'stroke-width':2.8,'stroke-linecap':'round','stroke-linejoin':'round'},raw)}}
 if(step){for(const e of step.residueEdges||[])node('path',{d:curve(e.controls),fill:'none',stroke:'#9ca9a4','stroke-width':1.5,opacity:.35},fit);
  for(const e of entries.filter(e=>e.group==='proposals'&&visible(e))){const c=e.row.controls,highlight=spansOf(e).some(id=>active.has(id));if(highlight)node('path',{d:curve(c),fill:'none',stroke:'#ecc56a','stroke-width':9,opacity:.65},fit);const n=node('path',{'data-proposal':e.row.id,d:curve(c),fill:'none',stroke:objectColor(spansOf(e),'#287b6f'),'stroke-width':2.6,'stroke-linecap':'round'},fit);bind(n,e);if($('directionLayer').checked)direction(fit,c)}
 }else{for(const s of currentSpans()){if(active.has(s.id))node('path',{d:curve(s.controls),fill:'none',stroke:'#ecc56a','stroke-width':9,'stroke-linecap':'round',opacity:.65},fit);const n=node('path',{'data-span':s.id,d:curve(s.controls),fill:'none',stroke:objectColor([s.id],$('orderColor').checked?`hsl(${s.strokeIndex*61%360} 45% 40%)`:'#287b6f'),'stroke-width':2.6,'stroke-linecap':'round'},fit);const e=entries.find(e=>e.group==='lines'&&spansOf(e).includes(s.id));if(e)bind(n,e);if($('directionLayer').checked)direction(fit,s.controls)}}
 for(const e of entries){if(!visible(e))continue;const show=!step&&((e.group==='joints'&&$('jointLayer').checked)||(e.group==='symmetries'&&$('mirrorLayer').checked)||(seeded(e)&&$('seedLayer').checked&&e.group!=='views'&&e.group!=='claims'&&e.group!=='objects'));if(show||selected===e&&e.group!=='proposals'&&!(step&&e.group==='objects'))drawEntry(fit,e,selected===e)}
 $('fitTitle').textContent=step?'Interpretation proposal':'Fitted evidence and hypotheses';
 const warning=step&&(p.analysis?.warnings||[]).some(w=>w.stepId===step.id);
 $('modeNote').textContent=step?'Authored proposal preview · source annotations retain their original basis'+(warning?' · some curves extend outside the frame':''):$('directionLayer').checked?'Green circle = start · orange square = end · arrow = traversal':'';
 $('fitStats').textContent=step?`${step.edges.length} proposed profiles · ${step.title} · native replay on demand`:p.observation.spans.length+' fitted intervals · '+(annotation(p).mode||'observed-fit');
 $('strokeValue').textContent=`${$('strokeLimit').value}/${p.observation.strokes.length}`;objectsPanel();list();if(selected)detail(selected)
}
function readable(value){if(value===null)return 'Unresolved';if(typeof value==='number')return Number.isInteger(value)?String(value):String(Number(value.toFixed(4)));if(typeof value==='object')return JSON.stringify(value);return String(value)}
function detail(e){
 const box=$('detail');box.replaceChildren();const seed=seeded(e);text(box,'span',seed?'Seeded / provisional':'Measured source read','badge'+(seed?' seed':''));text(box,'h3',label(e),'details-title');text(box,'p',e.row.qualification||(seed?'An authored expectation connected to source support; it is not newly observed geometry.':'A tolerance read of the acquired profiles; no source geometry is changed.'),'subtle');if(e.row.status)text(box,'p',e.row.status,'subtle');text(box,'p',spansOf(e).length+' supporting intervals','subtle');
 if(e.row.meaning)text(box,'p',e.row.meaning);
 if(e.group==='objects'){text(box,'p','Instance support: '+(e.row.instanceId||e.row.id),'tiny');objectLinks(box,e.row.parentIds,'Parent / attachment candidates');objectLinks(box,objectEntries().filter(v=>v.row.parentIds?.includes(e.row.id)).map(v=>v.row.id),'Related child candidates');for(const [title,rows]of [['Supporting reads',e.row.reasons],['Competing reads',e.row.conflicts]])if(rows?.length){text(box,'h4',title);for(const row of rows)text(box,'p',readable(row),'subtle')}}
 else if(e.row.id.startsWith('layout-')){objectLinks(box,e.row.participants,'Construction references');if(e.row.lowerBoundary==='inferred-level')text(box,'p','Lower edge is an inferred level; no drawn divider is required.','subtle');if(e.row.missingHorizontalArm)text(box,'p','Only the upright supports this marker. The horizontal arm is assumed.','subtle');if(e.row.costTerms){text(box,'h4','Regional placement costs');for(const [key,value]of Object.entries(e.row.costTerms))text(box,'p',key.replace(/([a-z])([A-Z])/g,'$1 $2')+': '+readable(value),'subtle')}}
 else if(spansOf(e).length){const ids=new Set(spansOf(e).flatMap(id=>objectView().spans[id]?.candidates||[]));objectLinks(box,[...ids],'Object memberships')}
 if(e.group==='claims')objectLinks(box,e.row.contextParts,'Connected object candidates');
 if(e.group==='claims'){text(box,'p',`Support: ${e.row.support} · opposition: ${e.row.opposition}`);text(box,'p',`Context: ${e.row.context} · provider: ${e.row.provider}`,'subtle');if(e.row.dependencies?.length){text(box,'h4','Earlier claim dependencies');for(const id of e.row.dependencies){const dep=entries.find(v=>v.group==='claims'&&v.row.id===id);const b=text(box,'button',dep?label(dep):id);if(dep)b.onclick=()=>choose(dep)}}}
 if(e.group==='lines'){const r=e.row.sourceRange,c=e.row.controls;text(box,'h4','Ordered endpoints');if(c){text(box,'p','Start (t=0): '+readable(c[0]));text(box,'p','End (t=1): '+readable(c[c.length-1]))}if(r){text(box,'p',`Stroke ${r.strokeIndex}, samples ${r.startIndex} → ${r.endIndex}`,'subtle');const t=packet().observation.strokes.find(s=>s.index===r.strokeIndex)?.times;text(box,'p',t?`Source time: ${t[r.startIndex]} → ${t[r.endIndex]}`:'Point timing was not supplied for this source.','subtle')}}
 for(const [name,values]of [['Measured',e.row.measured],['Expected',e.row.expected],['Thresholds',e.row.thresholds],['Comparisons',e.row.comparisons]]){if(!values||!Object.keys(values).length)continue;text(box,'h4',name);for(const [k,v]of Object.entries(values)){const item=text(box,'div','','metric');text(item,'strong',k.replace(/([a-z])([A-Z])/g,'$1 $2')+': ');text(item,'span',readable(v),'value')}}
 if(e.row.observedSharedSite!==undefined)text(box,'p',e.row.observedSharedSite?'A shared source sample is recorded.':'Separate source sites: this candidate does not establish contact.','subtle');if(e.row.residue&&e.row.residue.length){text(box,'h4','Still unresolved');for(const r of e.row.residue)text(box,'p',readable(r),'subtle')}const full=document.createElement('details');box.append(full);text(full,'summary','Complete evidence record');text(full,'pre',JSON.stringify(e.row,null,2))
}
function welcome(){const box=$('detail');box.replaceChildren();text(box,'span','Inspect this drawing','badge');text(box,'h3','Select a line, joint, or hypothesis','details-title');text(box,'p','Hover to preview its evidence. Click to keep its supporting intervals highlighted. The list also includes hypotheses that have no drawn location.','subtle');text(box,'p','Dashed outlines are seeded expectations. Original strokes and their acquired order remain available on the left.','subtle');const c=packet().context||{};text(box,'p',c.qualification||'No category or physics claim follows from a fitted line alone.','subtle')}
function choose(e){selected=e;render();detail(e)}
function list(){const host=$('list'),filter=$('filter').value;host.replaceChildren();const chosen=entries.filter(e=>(filter==='all'||e.group===filter)&&visible(e));for(const e of chosen){const b=text(host,'button','',selected===e?'active':'');b.dataset.evidence=e.key;text(b,'div',label(e));text(b,'div',(seeded(e)?'Seeded · ':'Measured · ')+e.group+' · '+spansOf(e).length+' intervals','tiny');b.addEventListener('click',()=>choose(e));b.addEventListener('mouseenter',()=>detail(e))}if(!chosen.length)text(host,'p','No records in this view.','subtle');$('counts').textContent=`${entries.length} evidence records · ${chosen.length} shown · offline review`}
function selectDrawing(i){index=(i+DATA.records.length)%DATA.records.length;selected=null;$('drawing').value=String(index);const r=DATA.records[index],p=r.packet;modes();gather();$('strokeLimit').min=p.observation.strokes.length?'1':'0';$('strokeLimit').max=String(p.observation.strokes.length);$('strokeLimit').value=String(p.observation.strokes.length);$('strokeLimit').disabled=!p.observation.strokes.length;$('sourceStats').textContent=p.observation.strokes.length+' strokes · '+p.observation.strokes.reduce((n,s)=>n+s.points.length,0)+' acquired samples · '+(p.observation.strokes.some(s=>s.times)?'point times retained':'no point timing supplied');$('position').textContent=(PAGING?PAGING.start+index:index+1)+' of '+(PAGING?PAGING.total:DATA.records.length);$('prev').disabled=DATA.records.length<2;$('next').disabled=DATA.records.length<2;rememberView();render();welcome()}
function download(name,body){const url=URL.createObjectURL(new Blob([body],{type:'application/json'})),a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)}
DATA.records.forEach((r,i)=>{const o=text($('drawing'),'option',(r.index.category||r.packet.source.word||'Drawing')+' · '+r.index.keyId);o.value=String(i)});
$('objectColor').onchange=()=>{rememberView();render()};
$('drawing').addEventListener('change',()=>selectDrawing(Number($('drawing').value)));$('prev').addEventListener('click',()=>selectDrawing(index-1));$('next').addEventListener('click',()=>selectDrawing(index+1));for(const id of ['rawOverlay','jointLayer','mirrorLayer','seedLayer','orderColor','filter','directionLayer'])$(id).addEventListener('change',render);$('readMode').onchange=()=>{requestedMode=$('readMode').value;rememberView();selected=null;gather();render();welcome()};$('strokeLimit').addEventListener('input',()=>{if(selected&&!visible(selected)){selected=null;welcome()}render()});$('download').addEventListener('click',()=>download(DATA.records[index].index.itemId+'.evidence.json',JSON.stringify(packet(),null,2)));$('downloadSource').addEventListener('click',()=>download(DATA.records[index].index.keyId+'.source.json',packet().sourceOriginalJson));document.addEventListener('keydown',e=>{if(['INPUT','SELECT','BUTTON'].includes(e.target.tagName))return;if(e.key==='ArrowRight')selectDrawing(index+1);if(e.key==='ArrowLeft')selectDrawing(index-1)});let hash;try{hash=decodeURIComponent(location.hash.slice(1))}catch{hash=''}const initial=DATA.records.findIndex(r=>r.index.itemId===hash||r.index.keyId===hash);selectDrawing(initial<0?0:initial);
</script></html>'''
