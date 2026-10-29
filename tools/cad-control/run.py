"""Prepare, execute and review a native-only repeated-frame control."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

REPO=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(REPO))
from gojo_quickdraw.church_cad import extend


def part(ident,x,y,w,h,parent=None):
    return dict(id=ident,role='window' if parent else 'building',status='authored-control',
        shape='rectangle',parent=parent,spans=[ident+'-authored-support'],bounds=[x,y,x+w,y+h],
        geometry=dict(points=[[x,y],[x+w,y],[x+w,y+h],[x,y+h]],segments=[[0,1],[1,2],[2,3],[3,0]]))


def run(output,work):
    output=output.resolve();work=work.resolve()
    if output==work or output in work.parents or work in output.parents:
        raise ValueError('Keep temporary build work separate from the retained control')
    output.mkdir(parents=True,exist_ok=True);work.mkdir(parents=True,exist_ok=True)
    parts=[part('frame',0,100,500,300)]+[part('window-'+str(i+1),*spec,'frame') for i,spec in enumerate(
        [(50,180,50,50),(155,192,70,65),(280,171,45,45),(390,186,65,60)])]
    rules=[];cad=extend(parts,{p['id']:p for p in parts},{p['id']:p['id'] for p in parts},
        dict(view=dict(selected='front-like'),symmetries=[]),rules,dict(pairs=[]),dict(recipes=[]))
    request=dict(scaffold=dict(parts=parts),plan=dict(rules=rules),cad=cad)
    source=output/'input.json';source.write_text(json.dumps(request,indent=2),encoding='utf-8')
    with (work/'build.log').open('w',encoding='utf-8') as log:
        subprocess.run(['dotnet','build',str(Path(__file__).with_name('CadControl.csproj')),'-c','Release',
                        '-o',str(work/'bin'),'--nologo','-v','quiet'],cwd=REPO,stdout=log,stderr=log,check=True)
    subprocess.run(['dotnet',str(work/'bin/CadControl.dll'),str(source),str(output)],cwd=REPO,check=True)
    result=json.loads((output/'result.json').read_text(encoding='utf-8-sig'))
    first,last=result['snapshots'][0],result['snapshots'][-1]
    assert result['sourceUnchanged'] and result['restoredMaxDifference']==0
    assert result['baseline']['parts']==first['parts']
    demand=lambda stage:sum(e['demand'] for e in stage['errors'] if e['layer'].startswith('cad'))
    assert demand(last)<demand(first)
    metadata=dict(opinions=len(rules),initialDiscrepancySum=demand(first),finalDiscrepancySum=demand(last),
        sourceUnchanged=True,baselineUnchanged=True,restoredMaxDifference=result['restoredMaxDifference'],
        inputSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
        nativeSha256=hashlib.sha256((output/'world.gojo.bin.gz').read_bytes()).hexdigest(),
        qualification='Authored four-member native control. Discrepancy sum is a diagnostic of normalized error reads, not conserved energy or recognition accuracy. Small requests can remain below actuator resolution.')
    (output/'checks.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
    data=json.dumps(result,separators=(',',':')).replace('<','\\u003c')
    (output/'review.html').write_text(PAGE.replace('__DATA__',data),encoding='utf-8')
    print(json.dumps(metadata))


PAGE='''<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Native frame repetition</title><style>
body{font:16px system-ui;color:#193e49;background:#f3f5ef;max-width:1400px;margin:20px auto;padding:0 16px}h1{font-size:25px;margin-bottom:6px}p{max-width:1000px;line-height:1.5}
.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}.panel{border:1px solid #cbd9d3;border-radius:12px;padding:16px;background:white}h2{font-size:18px}svg{width:100%;display:block}input{max-width:60vw;width:400px;vertical-align:middle}
table{border-collapse:collapse;margin:22px 0;background:white}td,th{padding:9px 18px;text-align:right;border-bottom:1px solid #d4ded8}td:first-child,th:first-child{text-align:left}small{color:#557078}button{padding:8px 16px}@media(max-width:800px){.grid{grid-template-columns:1fr}td,th{padding:8px}}
</style><h1>Repetition between native frames</h1>
<p>Four imperfectly spaced rectangles already exist as Gojo constructions. Eleven connected opinions ask them to share widths, heights, head levels and adjacent centre intervals. No sketch fitting, classifier or drawn target is involved.</p>
<p><button id="prev">Previous</button> <input id="round" type="range" min="0" max="16" value="16"> <button id="next">Next</button> <b id="label"></b></p>
<div class="grid"><div class="panel"><h2>Initial native frames</h2><div id="initial"></div></div><div class="panel"><h2>Without the row relations</h2><div id="baseline"></div></div><div class="panel"><h2>With the row relations</h2><div id="selected"></div></div></div>
<table><thead><tr><th>Remaining discrepancy</th><th>Initial</th><th>Selected round</th></tr></thead><tbody id="metrics"></tbody></table>
<p>Grey dotted outlines retain the initial frames. Each displayed state is a saved native settlement read, without interpolated movement. The unconnected control stays unchanged. The relations remain soft opinions: damping, finite resolution and competing responses can leave visible residue.</p>
<p><small>The same connected scene is reused for all sixteen rounds. Its complete final native structure is retained in <a href="world.gojo.bin.gz">world.gojo.bin.gz</a>; independently restored bounds exactly match these saved views. <a href="input.json">Input</a> · <a href="result.json">Saved rounds</a> · <a href="checks.json">Checks</a></small></p>
<script>const DATA=__DATA__,initial=DATA.snapshots[0],slider=document.querySelector('#round');
function rect(p,ghost=false){const b=p.bounds;return `<rect x="${b[0]}" y="${b[1]}" width="${b[2]-b[0]}" height="${b[3]-b[1]}" fill="${ghost?'none':p.id==='frame'?'#2d8caa0b':'#9265b416'}" stroke="${ghost?'#aab2b1':p.id==='frame'?'#2d8caa':'#9265b4'}" stroke-width="1.5" ${ghost?'stroke-dasharray="4 4"':''}/>`}
function draw(stage,ghost){return `<svg viewBox="-15 70 530 360" role="img" aria-label="Five native rectangular frames">${ghost?initial.parts.map(p=>rect(p,true)).join(''):''}${stage.parts.map(p=>rect(p)).join('')}</svg>`}
function metrics(stage){const b=stage.parts.slice(1).map(p=>p.bounds),spread=a=>Math.max(...a)-Math.min(...a),centres=b.map(p=>(p[0]+p[2])/2);return [spread(b.map(p=>p[2]-p[0])),spread(b.map(p=>p[3]-p[1])),spread(b.map(p=>p[1])),spread(centres.slice(1).map((c,i)=>c-centres[i])),stage.errors.filter(e=>e.layer.startsWith('cad')).reduce((s,e)=>s+e.demand,0)]}
function render(){const n=Number(slider.value),stage=DATA.snapshots[n];document.querySelector('#label').textContent='Round '+n+' of 16';document.querySelector('#initial').innerHTML=draw(initial,false);document.querySelector('#baseline').innerHTML=draw(DATA.baseline,false);document.querySelector('#selected').innerHTML=draw(stage,true);const a=metrics(initial),b=metrics(stage);document.querySelector('#metrics').innerHTML=['Width spread','Height spread','Head-level spread','Centre-interval spread','Normalized discrepancy sum'].map((name,i)=>`<tr><td>${name}</td><td>${a[i].toFixed(2)}</td><td>${b[i].toFixed(2)}</td></tr>`).join('')}
slider.oninput=render;document.querySelector('#prev').onclick=()=>{slider.value=Math.max(0,Number(slider.value)-1);render()};document.querySelector('#next').onclick=()=>{slider.value=Math.min(16,Number(slider.value)+1);render()};render();</script>'''


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--output',type=Path,required=True);p.add_argument('--work',type=Path,required=True)
    args=p.parse_args();run(args.output,args.work)
