"""Portable paged read of saved paired native encoding projections."""
import html
import json
from pathlib import Path


def build(output, manifest, page_size=12):
    output = Path(output)
    pages = max(1, (len(manifest['drawings'])+page_size-1)//page_size)
    for page in range(pages):
        drawings = [json.loads((output/row['path']).read_text(encoding='utf-8')) for row in manifest['drawings'][page*page_size:(page+1)*page_size]]
        name = 'report.html' if page == 0 else f'page-{page+1:05d}.html'
        def link(index):
            return 'report.html' if index == 0 else f'page-{index+1:05d}.html'
        navigation = ' '.join(f'<a href="{link(index)}">{label}</a>' for index, label in
                              ((0, 'First'), (max(0, page-1), 'Previous'), (min(pages-1, page+1), 'Next'), (pages-1, 'Last')))
        payload = json.dumps(drawings, separators=(',', ':'), allow_nan=False).replace('<', '\\u003c')
        header = f'''<h1>Joint encoding comparison</h1><p>{manifest['sourceDrawings']} drawings · {manifest['localPatches']} local patches ·
            {manifest['changedPatches']} patch opinions differ from / extend the default · provider: {html.escape(manifest['provider'])}</p>
            <nav>{navigation} <span>Page {page+1} of {pages}</span></nav>'''
        body = '''<p>Left: current source heuristics. Right: selected joint provider replaces covered two-run opinions.
            Gray marks are unresolved; unsupported and uncovered source joints remain. Hover for alternatives.
            These are conditional joint opinions, not newly confirmed source connections. Orientation comes from source hints.</p>
            <label><input id="weak" type="checkbox"> Show unresolved model choices</label>
            <label><input id="rays" type="checkbox" checked> Show directions</label>
            <main id="gallery"></main><dialog id="detail"><button id="close">Close</button><div id="content"></div></dialog>'''
        script = SCRIPT.replace('__DRAWINGS__', payload)
        (output/name).write_text('<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Joint encoding comparison</title><style>'+STYLE+'</style>'+header+body+'<script>'+script+'</script></html>', encoding='utf-8')


STYLE = '''body{font:15px system-ui;background:#f1f5f0;color:#183d43;margin:18px}h1{font-size:24px;margin:0}p{max-width:1050px}nav{display:flex;gap:16px;align-items:center;margin:16px 0}a{color:#247d72}label{display:inline-block;margin:0 18px 12px 0}main{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}.card{background:white;border:1px solid #ccdcd3;border-radius:12px;padding:12px;cursor:pointer}.pair{display:grid;grid-template-columns:1fr 1fr;gap:8px}.pair>div{min-width:0}.caption{font-size:12px;color:#647a79}.pair svg{width:100%;height:210px}h2{font-size:16px}dialog{border:1px solid #ccdcd3;border-radius:12px;width:1100px;max-width:94vw;max-height:92vh;overflow:auto;background:#f1f5f0}dialog .pair svg{height:450px}button,select{font:inherit;padding:7px;margin:6px}#choices{display:grid;grid-template-columns:repeat(3,1fr);gap:10px}#choices svg{width:100%;height:150px;background:white}.choice{padding:8px;background:white;border:1px solid #ccdcd3}svg text{font:10px system-ui;paint-order:stroke;stroke:white;stroke-width:3px;stroke-linejoin:round;fill:currentColor}svg g{cursor:help}@media(max-width:1100px){main{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:650px){main{grid-template-columns:1fr}#choices{grid-template-columns:1fr}}'''

STYLE += 'svg text{font-size:18px}svg path,svg line{vector-effect:non-scaling-stroke}'

SCRIPT = r'''
const drawings=__DRAWINGS__, colors={L:'#c27620',T:'#247d72',X:'#aa4676',I:'#537cab',separate:'#899398'};
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const path=run=>run.map((p,i)=>(i?'L':'M')+p.join(',')).join(' ');
function frame(d){const p=d.strokes.flat();if(!p.length)return[-10,-10,532,532];const xs=p.map(p=>p[0]),ys=p.map(p=>p[1]);const x=Math.min(...xs)-24,y=Math.min(...ys)-24;return[x,y,Math.max(...xs)-x+24,Math.max(...ys)-y+24]}
function svg(d,selected,focus=-1){let f=frame(d),out='<svg viewBox="'+f.join(' ')+'">';out+=d.strokes.map(r=>'<path d="'+path(r)+'" fill="none" stroke="#465852" opacity=".45" stroke-width="1.4"/>').join('');
const patch=d.patches[focus];if(patch?.runs)out+=patch.runs.map(r=>'<path d="'+path(r)+'" fill="none" stroke="#183d43" stroke-width="2"/>').join('');
const nodes=selected?d.selected:d.baseline;for(const n of nodes){const focused=selected?n.id==='model-p'+focus:patch?.baseline.includes(n.id),opinion=n.id.startsWith('model-p')?d.patches[+n.id.slice(7)]:null,unresolved=opinion?.proposed===null;if(['endpoint','I'].includes(n.kind)&&!n.id.startsWith('model-'))continue;if(unresolved&&!document.querySelector('#weak').checked&&!focused)continue;
if(n.kind==='separate'&&!d.patches[+n.id.slice(7)]?.changed&&!focused)continue;
const [x,y]=n.point,c=unresolved?'#899398':colors[n.kind]||'#7963a4';out+='<g style="color:'+c+'"><title>'+esc(n.basis)+'</title>';
if(document.querySelector('#rays').checked)for(const angle of n.directions){const a=angle*Math.PI/180;out+='<line x1="'+x+'" y1="'+y+'" x2="'+(x+12*Math.cos(a))+'" y2="'+(y+12*Math.sin(a))+'" stroke="'+c+'" stroke-width="1.6"/>'}
if(focused)out+='<circle cx="'+x+'" cy="'+y+'" r="8" fill="none" stroke="#c27620" stroke-width="2"/>';
out+='<circle cx="'+x+'" cy="'+y+'" r="3" fill="white" stroke="'+c+'"/><text x="'+(x+4)+'" y="'+(y-4)+'">'+esc(n.kind==='separate'?'none':n.kind)+'</text></g>'}return out+'</svg>'}
function pair(d,focus=-1){return '<div class="pair"><div><div class="caption">Default joint evidence</div>'+svg(d,false,focus)+'</div><div><div class="caption">Selected provider + same source</div>'+svg(d,true,focus)+'</div></div>'}
let current=-1;
function render(){document.querySelector('#gallery').innerHTML=drawings.map((d,i)=>'<article class="card" data-i="'+i+'"><h2>'+esc(d.category)+' · '+esc(d.sourceKey)+'</h2>'+pair(d)+'<div class="caption">'+d.patches.length+' local opinions · click to inspect alternatives'+(d.patchLimitReached?' · patch cap reached':'')+'</div></article>').join('');document.querySelectorAll('[data-i]').forEach(card=>card.onclick=()=>detail(+card.dataset.i));if(current>=0)detail(current)}
function detail(index){current=index;const d=drawings[index];document.querySelector('#content').innerHTML='<h2>'+esc(d.category)+' · '+esc(d.sourceKey)+'</h2><button id="prev">← Drawing</button><button id="next">Drawing →</button><div id="comparison">'+pair(d)+'</div><p>All five alternatives are saved. The three highest are shown below. Scores are relative support, not calibrated human probabilities.</p><select id="patch">'+d.patches.map((p,i)=>'<option value="'+i+'">Patch '+(i+1)+' · '+p.profiles.join(' / ')+' · '+p.status+'</option>').join('')+'</select><div id="choices"></div><p><a href="'+esc(d.baselineArchive)+'">Default native archive</a> · <a href="'+esc(d.selectedArchive)+'">Selected native archive</a></p>';
document.querySelector('#prev').onclick=()=>detail(Math.max(0,index-1));document.querySelector('#next').onclick=()=>detail(Math.min(drawings.length-1,index+1));document.querySelector('#patch').onchange=choices;choices();if(!document.querySelector('#detail').open)document.querySelector('#detail').showModal();}
function choices(){const d=drawings[current],index=+document.querySelector('#patch').value,p=d.patches[index];if(!p){document.querySelector('#choices').innerHTML='<p>No model checked: both reads share the same baseline.</p>';return}document.querySelector('#comparison').innerHTML=pair(d,index);document.querySelector('#choices').innerHTML=p.ranking.slice(0,3).map(k=>{const c=p.candidates[k],points=c.points,x=Math.min(...points.map(p=>p[0]))-8,y=Math.min(...points.map(p=>p[1]))-8,w=Math.max(...points.map(p=>p[0]))-x+8,h=Math.max(...points.map(p=>p[1]))-y+8;return '<div class="choice"><b>'+esc(c.kind)+' · '+(100*c.score).toFixed(1)+'%</b><svg viewBox="'+[x,y,w,h].join(' ')+'">'+c.segments.map(([a,b])=>'<path d="'+path([points[a],points[b]])+'" fill="none" stroke="#247d72" stroke-width="1.3"/>').join('')+'</svg><span class="caption">Source-oriented construction hypothesis</span></div>'}).join('')}
document.querySelector('#close').onclick=()=>{current=-1;document.querySelector('#detail').close()};document.querySelector('#detail').onclose=()=>current=-1;for(const id of ['weak','rays'])document.querySelector('#'+id).onchange=render;render();
'''
