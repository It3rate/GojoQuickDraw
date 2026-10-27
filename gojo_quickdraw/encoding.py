"""Paired, read-only applications of selected providers to source evidence."""
from datetime import datetime, timezone
import gzip
import json
import math
from pathlib import Path
import shutil
import time

from . import dataset, joint_model, training, training_data, upstream

SCHEMA = 'gojo.paired-joint-encoding.v1'


def load_model(path):
    model = json.loads(Path(path).read_text(encoding='utf-8-sig'))
    if model.get('schema') != 'gojo.local-joint-model.v1' or model.get('candidateOrder') != list(training_data.NAMES):
        raise ValueError('Choose a local joint model with the five supported constructions')
    columns, weights = model.get('columns', []), model.get('weights', [])
    if not columns or not all(isinstance(c, str) for c in columns) or len(set(columns)) != len(columns):
        raise ValueError('Model feature columns must be distinct text values')
    if len(weights) != 5 or any(not isinstance(row, list) or len(row) != len(columns) or any(type(v) not in (int, float) or not math.isfinite(v) for v in row) for row in weights):
        raise ValueError('Model weights must be a finite five-row matrix matching its columns')
    if model.get('mode') not in joint_model.MODES or type(model.get('temperature', 1)) not in (int, float) or not math.isfinite(model.get('temperature', 1)) or model.get('temperature', 1) <= 0:
        raise ValueError('Model needs a known feature mode and positive finite temperature')
    return model


def run(source, output, gojo_root, *, models=(), limit=25, max_patches=128, seed=31, recognition='all'):
    started = time.perf_counter()
    source, output = Path(source).resolve(), Path(output).resolve()
    if output == source or source in output.parents or output in source.parents:
        raise ValueError('Choose a new encoding run folder separate from the drawings')
    if output.exists() and any(output.iterdir()):
        raise ValueError('Choose a new encoding run folder to preserve earlier comparisons')
    if len(models) > 1:
        raise ValueError('Select one joint provider per comparison; two versions are not independent evidence')
    if not 1 <= max_patches <= 512:
        raise ValueError('Choose 1..512 local patches per drawing')
    model_path = Path(models[0]).resolve() if models else None
    model = load_model(model_path) if model_path else None
    cohort, warnings = training_data.snapshot(source, limit, seed, recognition)
    if not cohort:
        raise ValueError('No completed drawings match this encoding selection')
    output.mkdir(parents=True, exist_ok=True)
    state = {'schema': SCHEMA, 'status': 'preparing', 'source': str(source), 'limit': limit,
             'maxPatches': max_patches, 'seed': seed, 'recognition': recognition,
             'createdUtc': datetime.now(timezone.utc).isoformat(), 'provider': 'joint-model' if model else 'default',
             'upstream': upstream.fingerprint(gojo_root)}
    if model_path:
        (output/'models').mkdir()
        shutil.copyfile(model_path, output/'models/joints.json')
        model = load_model(output/'models/joints.json')
        state['modelSha256'] = dataset.sha256(output/'models/joints.json')
        state['modelSource'] = str(model_path)
    dataset._atomic_json(output/'encoding.json', state)
    contexts, snapshots = [], []
    requests = output/'inputs.ndjson'
    with requests.open('w', encoding='utf-8') as stream:
        for folder, row in cohort:
            packet = training_data.load_packet(folder, row)
            request = training_data.real_request(packet, row['sourceSha256'], max_patches, inference=True) if model else None
            if request is None:
                request = {'sourceKey': packet['source']['key_id'], 'packet': {key: packet[key] for key in
                    ('sourceOriginalJson', 'source', 'observation', 'program', 'annotations')}, 'patches': []}
            stream.write(json.dumps(request, separators=(',', ':'), allow_nan=False)+'\n')
            contexts.append({'sourceKey': packet['source']['key_id'], 'category': row.get('category', packet['source'].get('word', 'drawing')),
                             'strokes': [s['points'] for s in packet['observation']['strokes']],
                             'patchLimitReached': len(request['patches']) >= max_patches})
            snapshots.append({'sourceSha256': row['sourceSha256'], 'evidenceSha256': row['evidenceSha256'],
                              'nativeSha256': row.get('nativeSha256'), 'key': row['keyId']})
    training.log(f'Encoding {len(cohort)} completed drawings. Baseline is retained; selected provider: {state["provider"]}.')
    command = training.executable(gojo_root)
    training.native(command, 'export', requests, output/'baseline', output/'features.ndjson')
    examples = [json.loads(line) for line in (output/'features.ndjson').read_text(encoding='utf-8').splitlines()]
    batches = [[] for _ in cohort]
    for row in examples:
        index = int(Path(row['archive']).name[8:13])
        batches[index].append(row)
    paired_requests = output/'providers.ndjson'
    with paired_requests.open('w', encoding='utf-8') as stream:
        for i, rows in enumerate(batches):
            stream.write(json.dumps({'sourceArchive': str(output/'baseline'/f'patches-{i:05d}.gojo.json.gz'),
                'enabled': model is not None, 'modelSha256': state.get('modelSha256'),
                'predictions': [joint_model.predict(model, row['bands']) for row in rows] if model else []}, separators=(',', ':'))+'\n')
    training.native(command, 'encode', paired_requests, output/'selected', output/'readback.ndjson')
    reads = [json.loads(line) for line in (output/'readback.ndjson').read_text(encoding='utf-8').splitlines()]
    if len(reads) != len(contexts):
        raise ValueError('Paired native readback lost source coverage')
    drawings, changed, proposals, unresolved = [], 0, 0, 0
    (output/'drawings').mkdir()
    for i, (context, read) in enumerate(zip(contexts, reads)):
        context.update(read)
        rows = batches[i]
        if model and len(rows) != len(read['patches']):
            raise ValueError('Paired readback lost patch correspondence')
        for original, patch in zip(rows, context['patches']):
            patch.update(id=original['id'], bands=original['bands'], runs=original['runs'])
            changed += patch['changed']
            proposals += patch['proposed'] is not None
            unresolved += patch['proposed'] is None
        context['baselineArchive'] = f'baseline/patches-{i:05d}.gojo.json.gz'
        context['selectedArchive'] = f'selected/patches-{i:05d}.gojo.json.gz'
        path = f'drawings/drawing-{i:05d}.json'
        dataset._atomic_json(output/path, context)
        drawings.append({'path': path, 'sourceKey': context['sourceKey'], 'category': context['category'],
                         'patches': len(context['patches'])})
    local_patches = sum(row['patches'] for row in drawings)
    state.update(status='completed', drawings=drawings, sourceDrawings=len(drawings), localPatches=local_patches,
                 proposed=proposals, unresolved=unresolved, changedPatches=changed, warnings=warnings,
                 sourceSnapshot=snapshots, nativeRestoresVerified=True, report='report.html',
                 totalSeconds=time.perf_counter()-started,
                 qualification='Paired joint evidence only. The selected provider replaces the local heuristic read, not the source acquisition. Scores are relative model support, not calibrated human probabilities. Placement is algorithmic. Shape/category feedback and whole-drawing settlement are not applied.')
    dataset._atomic_json(output/'encoding.json', state)
    from .encoding_report import build
    build(output, state)
    for path in (requests, output/'features.ndjson', paired_requests, output/'readback.ndjson'):
        with path.open('rb') as original, gzip.open(path.with_suffix(path.suffix+'.gz'), 'wb') as compressed:
            shutil.copyfileobj(original, compressed)
        path.unlink()
    return {'output': str(output), 'report': str(output/'report.html'), 'sourceDrawings': len(drawings),
            'localPatches': local_patches, 'changedPatches': changed, 'proposed': proposals,
            'unresolved': unresolved, 'provider': state['provider'], 'totalSeconds': state['totalSeconds']}
