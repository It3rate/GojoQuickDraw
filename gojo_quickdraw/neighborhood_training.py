"""Bounded training of source-local repair and line interpretation providers."""
from datetime import datetime, timezone
import gzip
import hashlib
import json
from pathlib import Path
import shutil
import time

from . import dataset, neighborhood_data, neighborhood_models, training, training_data, upstream

SCHEMA = 'gojo.neighborhood-training.v1'


def compact(paths):
    for path in paths:
        with path.open('rb') as source, gzip.open(path.with_suffix(path.suffix+'.gz'), 'wb') as out:
            shutil.copyfileobj(source, out)
        path.unlink()


def new_output(source, output):
    source, output = Path(source).resolve(), Path(output).resolve()
    if output == source or source in output.parents or output in source.parents:
        raise ValueError('Choose a new experiment folder separate from the drawings')
    if output.exists() and any(output.iterdir()):
        raise ValueError('Choose an empty experiment folder to retain earlier runs')
    return source, output


def source_packet(packet):
    return {key: packet[key] for key in ('sourceOriginalJson','source','observation','program','annotations')}


def adapter_fingerprint():
    digest=hashlib.sha256()
    paths=sorted([*upstream.ROOT.joinpath('learning').glob('*.cs'),*upstream.ROOT.joinpath('gojo_quickdraw').glob('neighborhood*.py'),
                  upstream.ROOT/'gojo_quickdraw/consolidation.py',upstream.ROOT/'gojo_quickdraw/training_data.py',upstream.ROOT/'shared/JointGraph.cs'])
    for path in paths:
        digest.update(path.relative_to(upstream.ROOT).as_posix().encode());digest.update(b'\0');digest.update(path.read_bytes());digest.update(b'\0')
    return {'sha256':digest.hexdigest(),'files':len(paths),'basis':'Exact adapter source bytes and ordered relative paths; build outputs excluded'}


def run(source, output, gojo_root, *, limit=60, synthetic=1800, epochs=35, seed=43, recognition='all'):
    started = time.perf_counter()
    source, output = new_output(source, output)
    if not 100 <= synthetic <= 20000 or not 1 <= epochs <= 200:
        raise ValueError('Choose 100..20000 controlled neighborhoods and 1..200 epochs')
    cohort, warnings = training_data.snapshot(source,limit,seed,recognition)
    if not cohort:
        raise ValueError('No completed drawings match this training selection')
    output.mkdir(parents=True,exist_ok=True)
    state = {'schema':SCHEMA,'status':'preparing','source':str(source),'sourceDrawings':len(cohort),'synthetic':synthetic,
             'seed':seed,'epochs':epochs,'recognition':recognition,'createdUtc':datetime.now(timezone.utc).isoformat(),
             'upstream':upstream.fingerprint(gojo_root),'adapter':adapter_fingerprint()}
    dataset._atomic_json(output/'training.json',state)
    requests=output/'inputs.ndjson'; snapshots=[]; skipped=0
    controls=list(neighborhood_data.controls(synthetic,seed))
    with requests.open('w',encoding='utf-8') as stream:
        for request in controls:
            stream.write(json.dumps(request,separators=(',',':'))+'\n')
        for folder,row in cohort:
            packet=training_data.load_packet(folder,row);goals,rejected=neighborhood_data.weak_goals(packet);skipped+=rejected
            stream.write(json.dumps({'packet':source_packet(packet),'sourceHash':row['sourceSha256'],
                'evidenceSha256':row['evidenceSha256'],'goals':goals},separators=(',',':'),allow_nan=False)+'\n')
            snapshots.append({'key':row['keyId'],'sourceSha256':row['sourceSha256'],'evidenceSha256':row['evidenceSha256']})
    command=training.executable(gojo_root)
    training.log(f'Neighborhood training: {synthetic} controlled cases; {len(cohort)} read-only church snapshots')
    state['status']='native-neighborhood-reads';dataset._atomic_json(output/'training.json',state)
    training.native(command,'neighborhood-export',requests,output/'native',output/'features.ndjson')
    batches=[json.loads(line) for line in (output/'features.ndjson').read_text(encoding='utf-8').splitlines()]
    lines=[dict(row,archive=b['archive']) for b in batches for row in b['lines']]
    repairs=[dict(row,archive=b['archive']) for b in batches for row in b['repairs']]
    state['status']='training';dataset._atomic_json(output/'training.json',state)
    line_model=neighborhood_models.fit(lines,batches[0]['lineNames'],epochs=epochs,seed=seed)
    # Select the controlled-only or weak-assisted trial using validation only.
    controlled=[r for r in repairs if not r['basis'].startswith('weak')]
    repair_model=neighborhood_models.fit(controlled,batches[0]['repairNames'],epochs=epochs,seed=seed)
    weak=[r for r in repairs if r['basis'].startswith('weak')]
    selected_weak=False
    if any(training_data.split(r['group'],seed)=='train' for r in weak):
        trial=neighborhood_models.fit(repairs,batches[0]['repairNames'],epochs=epochs,seed=seed)
        validation=[r for r in controlled if training_data.split(r['group'],seed)=='validation']
        weak_validation=[r for r in weak if training_data.split(r['group'],seed)=='validation']
        def criterion(model):
            return neighborhood_models.metrics(model,validation)['crossEntropy']+.15*(neighborhood_models.metrics(model,weak_validation)['crossEntropy'] or 0)
        selected_weak=criterion(trial)<criterion(repair_model)
        if selected_weak:repair_model=trial
        dataset._atomic_json(output/'weak-trial.json',trial)
    bundle={'schema':'gojo.neighborhood-models.v1','line':line_model,'repair':repair_model,'seed':seed,
            'sourceSnapshot':snapshots,'upstream':state['upstream'],'adapter':state['adapter'],'weakTrialSelected':selected_weak,
            'qualification':'Relative support over source-local candidates; authored compatibility gates apply. No category or whole-church model.'}
    dataset._atomic_json(output/'model.json',bundle)
    model_hash=dataset.sha256(output/'model.json')
    held=lambda rows:[r for r in rows if training_data.split(r['group'],seed)=='test']
    # Apply the learned providers to held-out complete constructions, retaining
    # actual outputs. Targets are evaluation-only and never enter Execute().
    selected=[]; selected_groups=set()
    for request in controls:
        for case in request['cases']:
            if training_data.split(case['group'],seed)=='test' and case['group'] not in selected_groups and len(selected)<60:
                selected.append(case);selected_groups.add(case['group'])
    evaluation_input=output/'evaluation-input.ndjson'
    with evaluation_input.open('w',encoding='utf-8') as stream:
        for case in selected:
            stream.write(json.dumps({**case,'modelPath':str(output/'model.json'),'useRepair':True,'useLine':True,'passes':2,'maxCandidates':128},separators=(',',':'))+'\n')
    training.native(command,'neighborhood-run',evaluation_input,output/'evaluation-native',output/'evaluation.ndjson')
    outputs=[json.loads(line) for line in (output/'evaluation.ndjson').read_text(encoding='utf-8').splitlines()]
    evaluation=[{'kind':c['kind'],'group':c['group'],'source':c['source'],'target':c['target'],**r} for c,r in zip(selected,outputs)]
    if len(outputs)!=len(selected):raise ValueError('Held-out graph evaluation lost a construction')
    dataset._atomic_json(output/'graph-evaluation.json',evaluation)
    graph_metrics={'examples':len(outputs),'improved':sum(r['evaluation']['after']<r['evaluation']['before']-1e-5 for r in outputs),
                   'worsened':sum(r['evaluation']['after']>r['evaluation']['before']+1e-5 for r in outputs),
                   'unchanged':sum(abs(r['evaluation']['after']-r['evaluation']['before'])<=1e-5 for r in outputs),
                   'independentTemplates':len(selected_groups),
                   'neededRepair':sum(r['evaluation']['before']>1e-5 for r in outputs),
                   'correctlyUnchanged':sum(r['evaluation']['before']<=1e-5 and r['evaluation']['after']<=1e-5 for r in outputs)}
    report={**state,'status':'completed','trained':True,'controlledNeighborhoods':synthetic,'lineExamples':len(lines),'repairExamples':len(repairs),
            'weakRepairExamples':len(weak),'weakGoals':sum(b['goals'] for b in batches),
            'unmappedOrIncompleteFinalJointsSkipped':skipped,'unsupportedWeakCandidatesSkipped':sum(b['unsupported'] for b in batches),
            'ambiguousWeakCandidatesSkipped':sum(b['ambiguous'] for b in batches),
            'lineTest':neighborhood_models.metrics(line_model,held(lines)),
            'repairTest':neighborhood_models.metrics(repair_model,held(controlled)),
            'realWeakTest':neighborhood_models.metrics(repair_model,held(weak)),
            'graphTest':graph_metrics,'parameterCount':len(line_model['weights'])+len(repair_model['weights']),
            'weakTrialSelected':selected_weak,'modelSha256':model_hash,'sourceSnapshot':snapshots,'warnings':warnings,
            'splitGroups':{s:sorted({r['group'] for r in lines+repairs if training_data.split(r['group'],seed)==s}) for s in ('train','validation','test')},
            'nativeRestoresVerified':True,'sourceOutcomesUsedAsFeatures':False,'totalSeconds':time.perf_counter()-started,
            'qualification':'Synthetic local constructions supply known targets. Church finals supply weak targets only where required rays have mapped source support. Tests do not establish human correctness or whole-church reconstruction.'}
    dataset._atomic_json(output/'report.json',report)
    from .neighborhood_report import training_report
    training_report(output,report,evaluation)
    compact([requests,output/'features.ndjson',evaluation_input,output/'evaluation.ndjson'])
    state.update(status='completed',modelSha256=model_hash,report='report.html');dataset._atomic_json(output/'training.json',state)
    return {**report,'output':str(output),'report':str(output/'report.html'),'model':str(output/'model.json')}
