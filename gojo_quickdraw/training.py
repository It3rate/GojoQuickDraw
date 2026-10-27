"""Train and review a bounded local construction ranker without converting data."""
from collections import defaultdict
from datetime import datetime, timezone
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time

from . import dataset, joint_model, training_data, upstream


def log(message):
    print(message, file=sys.stderr, flush=True)


def executable(gojo_root):
    project = upstream.ROOT/'learning/GojoQuickDraw.Learning.csproj'
    result = subprocess.run(['dotnet', 'build', str(project), '-c', 'Release', '--verbosity', 'quiet', f'-p:GojoRoot={gojo_root}'], capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError('Learning adapter build failed:\n'+result.stdout[-4000:]+result.stderr[-2000:])
    return ['dotnet', str(upstream.ROOT/'learning/bin/Release/net10.0/GojoQuickDraw.Learning.dll')]


def native(command, mode, input_path, output_path, stdout_path=None):
    with open(stdout_path, 'w', encoding='utf-8') if stdout_path else open(Path(output_path).parent/'native-last-output.log', 'w', encoding='utf-8') as output:
        worker = subprocess.Popen([*command, mode, str(input_path), str(output_path)], stdout=output, stderr=subprocess.PIPE, text=True, encoding='utf-8')
        for line in worker.stderr:
            log(line.rstrip())
        code = worker.wait()
    if code:
        raise RuntimeError(f'Native learning {mode} failed; see run details')


def run(source, output, gojo_root, *, limit=300, synthetic=2500, epochs=35, seed=31, recognition='all'):
    started = time.perf_counter()
    source, output = Path(source).resolve(), Path(output).resolve()
    if output == source or source in output.parents or output in source.parents:
        raise ValueError('Choose a model output folder separate from the drawings')
    if output.exists() and any(output.iterdir()):
        raise ValueError('Choose a new model run folder to preserve earlier experiments')
    if not 100 <= synthetic <= 20000 or not 1 <= epochs <= 200:
        raise ValueError('Choose 100..20000 controlled patches and 1..200 epochs')
    cohort, warnings = training_data.snapshot(source, limit, seed, recognition)
    if not cohort:
        raise ValueError('No completed drawings match this training selection')
    output.mkdir(parents=True, exist_ok=True)
    state = {'schema': 'gojo.local-joint-training.v1', 'status': 'preparing', 'source': str(source), 'limit': limit,
             'synthetic': synthetic, 'epochs': epochs, 'seed': seed, 'recognition': recognition,
             'createdUtc': datetime.now(timezone.utc).isoformat()}
    dataset._atomic_json(output/'training.json', state)
    log(f'Training snapshot: {len(cohort)} completed drawings. Conversion is not run.')
    requests = output/'inputs.ndjson'
    real_count = 0
    contexts = {}
    degenerate_profiles = source_without_patch = 0
    with requests.open('w', encoding='utf-8') as stream:
        for request in training_data.synthetic_requests(synthetic, seed):
            stream.write(json.dumps(request, separators=(',', ':'))+'\n')
        for folder, row in cohort:
            packet = training_data.load_packet(folder, row)
            request = training_data.real_request(packet, row['sourceSha256'])
            if request:
                real_count += len(request['patches'])
                degenerate_profiles += len(request.get('unresolvedProfiles', []))
                stream.write(json.dumps(request, separators=(',', ':'), allow_nan=False)+'\n')
                contexts[packet['source']['key_id']] = [stroke['points'][::max(1, len(stroke['points'])//80)] + stroke['points'][-1:]
                    for stroke in packet['observation']['strokes'] if stroke['points']]
            else:
                source_without_patch += 1
    timings = {'snapshotAndRequests': time.perf_counter()-started}
    before = time.perf_counter(); command = executable(gojo_root); timings['build'] = time.perf_counter()-before
    before = time.perf_counter(); state['status'] = 'native-patch-reads'; dataset._atomic_json(output/'training.json', state)
    native(command, 'export', requests, output/'native', output/'features.ndjson')
    native(command, 'templates', '-', output/'templates.json')
    timings['nativeFeatureExportAndRestore'] = time.perf_counter()-before
    examples = [json.loads(line) for line in (output/'features.ndjson').read_text(encoding='utf-8').splitlines()]
    controls = [row for row in examples if row['labelBasis'] == 'controlled-construction']
    real = [row for row in examples if row['labelBasis'] != 'controlled-construction']
    if len(controls) != synthetic or len(real) != real_count:
        raise ValueError('Native patch publication lost source examples')
    test = lambda rows: [row for row in rows if training_data.split(row['group'], seed) == 'test']
    valid = lambda rows: [row for row in rows if training_data.split(row['group'], seed) == 'validation']
    before = time.perf_counter(); state['status'] = 'training'; dataset._atomic_json(output/'training.json', state)
    models, ablations = {}, {}
    for mode in joint_model.MODES:
        log(f'Learning baseline: {mode}')
        model = joint_model.fit(controls, mode, epochs, seed, log=log)
        models[mode] = model
        ablations[mode] = {'controlledValidation': joint_model.metrics(model, valid(controls)),
                          'controlledTest': joint_model.metrics(model, test(controls)), 'realWeakTest': joint_model.metrics(model, test(real))}
    selected_name = min(models, key=lambda name: ablations[name]['controlledValidation']['crossEntropy'])
    selected = models[selected_name]
    weak_train = [row for row in real if row['label'] is not None and training_data.split(row['group'], seed) == 'train']
    fine = None
    if len(weak_train) >= 25 and len({row['label'] for row in weak_train}) >= 2:
        log(f'Learning a trial with {len(weak_train)} weak real training patches (weight 0.25)')
        fine = joint_model.fit(examples, selected['mode'], epochs, seed, log=log)
        def criterion(model):
            controlled = joint_model.metrics(model, valid(controls))['crossEntropy']
            weak = joint_model.metrics(model, valid(real))['crossEntropy']
            return controlled + .25*(weak or 0)
        if criterion(fine) < criterion(selected):
            selected = fine; selected_name += '+weak-real'
        dataset._atomic_json(output/'weak-trial-model.json', fine)
    timings['fitAllModels'] = time.perf_counter()-before
    selected['trainingSourceFingerprints'] = sorted(row['sourceSha256'] for _, row in cohort)
    selected['upstream'] = upstream.fingerprint(gojo_root)
    dataset._atomic_json(output/'model.json', selected)
    model_hash = dataset.sha256(output/'model.json')
    predictions = [joint_model.predict(selected, row['bands']) for row in examples]
    grouped = defaultdict(list)
    with gzip.open(output/'predictions.ndjson.gz', 'wt', encoding='utf-8') as stream:
        for row, prediction in zip(examples, predictions):
            stream.write(json.dumps({'patch': row, 'opinion': prediction}, separators=(',', ':'))+'\n')
            grouped[row['archive']].append(prediction)
    opinion_input = output/'opinion-input.ndjson'
    with opinion_input.open('w', encoding='utf-8') as stream:
        for archive, reads in grouped.items():
            stream.write(json.dumps({'sourceArchive': str(output/'native'/archive), 'modelSha256': model_hash, 'predictions': reads}, separators=(',', ':'))+'\n')
    before = time.perf_counter(); state['status'] = 'native-opinions'; dataset._atomic_json(output/'training.json', state)
    native(command, 'opinions', opinion_input, output/'opinions')
    timings['nativeOpinionsAndRestore'] = time.perf_counter()-before
    groups = {name: sorted({row['group'] for row in examples if training_data.split(row['group'], seed) == name}) for name in ('train', 'validation', 'test')}
    report = {'schema': state['schema'], 'sourceDrawings': len(cohort), 'controlledPatches': len(controls), 'realPatches': len(real),
              'realWeakLabels': sum(row['label'] is not None for row in real), 'selectedModel': selected_name,
              'sourceDrawingsWithoutTwoRunPatch': source_without_patch, 'zeroExtentProfilesKeptUnresolved': degenerate_profiles,
              'parameterCount': sum(map(len, selected['weights'])), 'modelSha256': model_hash, 'trained': True,
              'controlledTest': joint_model.metrics(selected, test(controls)), 'realWeakTest': joint_model.metrics(selected, test(real)),
              'ablations': ablations, 'weakTrial': {'trained': fine is not None, 'selected': selected is fine},
              'splitGroups': groups, 'warnings': warnings, 'sourceSnapshot': [{'sourceSha256': row['sourceSha256'], 'evidenceSha256': row['evidenceSha256'], 'key': row['keyId']} for _, row in cohort],
              'timingsSeconds': timings, 'totalSeconds': time.perf_counter()-started, 'sourceOutcomesUsedAsFeatures': False,
              'nativePatchBatches': len(grouped), 'nativeFeatureAndOpinionRestoresVerified': True,
              'qualification': 'Known controlled generators provide synthetic targets. Real labels are dependent authored final correspondences, not human ground truth. No source drawing or conversion stage is changed. Global settlement, part recognition and learned physical priors are not claimed.'}
    dataset._atomic_json(output/'report.json', report)
    from .training_report import build
    templates = json.loads((output/'templates.json').read_text(encoding='utf-8'))
    build(output, report, examples, predictions, templates, seed, contexts)
    # Retain reproducible source inputs compactly; no loose per-example temp files.
    for path in (requests, output/'features.ndjson', opinion_input):
        with path.open('rb') as original, gzip.open(path.with_suffix(path.suffix+'.gz'), 'wb') as compressed:
            import shutil
            shutil.copyfileobj(original, compressed)
        path.unlink()
    state.update(status='completed', modelSha256=model_hash, output=str(output), report='report.html')
    dataset._atomic_json(output/'training.json', state)
    return {'output': str(output), 'report': str(output/'report.html'), 'model': str(output/'model.json'), 'trained': True,
            'sourceDrawings': len(cohort), 'controlledPatches': len(controls), 'realPatches': len(real), 'parameterCount': report['parameterCount'],
            'controlledTest': report['controlledTest'], 'realWeakTest': report['realWeakTest'], 'totalSeconds': report['totalSeconds'], 'selectedModel': selected_name}
