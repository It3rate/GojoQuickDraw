"""Apply neighborhood providers to saved sources, preserving every local pass."""
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import shutil
import time

from . import dataset, neighborhood_models, training, training_data, upstream
from .neighborhood_training import compact, new_output, source_packet

SCHEMA='gojo.neighborhood-consolidation.v1'


def load_model(path):
    bundle=json.loads(Path(path).read_text(encoding='utf-8-sig'))
    if bundle.get('schema')!='gojo.neighborhood-models.v1':
        raise ValueError('Choose a neighborhood repair/line model bundle')
    for name,count in (('line',8),('repair',24)):
        m=bundle.get(name,{})
        columns,weights=m.get('columns',[]),m.get('weights',[])
        if m.get('schema')!='gojo.neighborhood-provider.v1' or m.get('featureCount')!=count or len(m.get('featureNames',[]))!=count:
            raise ValueError('Model has an incompatible neighborhood feature interface')
        if not columns or any(not isinstance(v,str) for v in columns) or len(set(columns))!=len(columns) or 'bias' not in columns:
            raise ValueError('Model needs distinct feature columns and a bias')
        if len(weights)!=len(columns) or any(type(v) not in (int,float) or not math.isfinite(v) for v in weights):
            raise ValueError('Neighborhood weights must be finite and match their columns')
    return bundle


def run(source,output,gojo_root,*,model,limit=25,max_candidates=192,passes=4,seed=43,recognition='all',use_repair=True,use_line=True):
    started=time.perf_counter();source,output=new_output(source,output)
    load_model(model)
    if not 1<=passes<=8 or not 1<=max_candidates<=512:raise ValueError('Choose 1..8 passes and 1..512 candidates')
    cohort,warnings=training_data.snapshot(source,limit,seed,recognition)
    if not cohort:raise ValueError('No completed drawings match this selection')
    output.mkdir(parents=True,exist_ok=True);(output/'models').mkdir();shutil.copyfile(model,output/'models/neighborhoods.json')
    state={'schema':SCHEMA,'status':'preparing','source':str(source),'limit':limit,'seed':seed,'recognition':recognition,
           'createdUtc':datetime.now(timezone.utc).isoformat(),'provider':'neighborhood repair + line' if use_repair and use_line else 'repair only' if use_repair else 'line only' if use_line else 'unchanged control',
           'modelSha256':dataset.sha256(output/'models/neighborhoods.json'),'useRepair':use_repair,'useLine':use_line,'passes':passes,
           'maxCandidates':max_candidates,'upstream':upstream.fingerprint(gojo_root)}
    dataset._atomic_json(output/'encoding.json',state)
    contexts=[];snapshots=[];requests=output/'inputs.ndjson'
    with requests.open('w',encoding='utf-8') as stream:
        for folder,row in cohort:
            packet=training_data.load_packet(folder,row)
            # Intentionally omit final shape graphs, correspondences, and weak labels.
            request={'packet':source_packet(packet),'modelPath':str(output/'models/neighborhoods.json'),'useRepair':use_repair,'useLine':use_line,'passes':passes,'maxCandidates':max_candidates}
            stream.write(json.dumps(request,separators=(',',':'),allow_nan=False)+'\n')
            contexts.append({'sourceKey':packet['source']['key_id'],'category':row.get('category',packet['source'].get('word','drawing')),
                             'strokes':[s['points'] for s in packet['observation']['strokes']]})
            snapshots.append({'key':row['keyId'],'sourceSha256':row['sourceSha256'],'evidenceSha256':row['evidenceSha256']})
    training.log(f'Consolidating {len(cohort)} source drawings; {passes} bounded local passes; {state["provider"]}')
    command=training.executable(gojo_root)
    try:
        training.native(command,'neighborhood-run',requests,output/'native',output/'readback.ndjson')
    except RuntimeError as error:
        state.update(status='failed',reason=str(error));dataset._atomic_json(output/'encoding.json',state)
        raise
    reads=[json.loads(line) for line in (output/'readback.ndjson').read_text(encoding='utf-8').splitlines()]
    if len(reads)!=len(contexts):raise ValueError('Neighborhood run lost source coverage')
    items=[];(output/'drawings').mkdir();accepted=0;decisions=0
    for i,(context,read) in enumerate(zip(contexts,reads)):
        context.update(read);context['nativeArchive']=f'native/drawing-{i:05d}.gojo.json.gz'
        path=f'drawings/drawing-{i:05d}.json';dataset._atomic_json(output/path,context)
        items.append({'path':path,'sourceKey':context['sourceKey'],'category':context['category'],'patches':len(context['decisions'])})
        accepted+=context['accepted'];decisions+=len(context['decisions'])
    state.update(status='completed',sourceDrawings=len(items),drawings=items,localPatches=decisions,changedPatches=accepted,proposed=accepted,
                 unresolved=decisions-accepted,sourceSnapshot=snapshots,warnings=warnings,nativeRestoresVerified=True,
                 sourceOutcomesUsedAtInference=False,totalSeconds=time.perf_counter()-started,report='report.html',
                 qualification='Source-only local repair. Changed graphs are independently restored native structures. Shape/category feedback is not applied; model support is not a calibrated human probability.')
    dataset._atomic_json(output/'encoding.json',state)
    from .neighborhood_report import encoding_report
    encoding_report(output,state)
    compact([requests,output/'readback.ndjson'])
    return {**state,'output':str(output),'report':str(output/'report.html')}
