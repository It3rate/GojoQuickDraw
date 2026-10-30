"""Measure the same retained request without source fitting, training or build time."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
from time import perf_counter


def run(runtime, source, output, mode, execution='audit', passes=None):
    output = Path(output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    if any(output.iterdir()):
        raise ValueError('Choose an empty benchmark output folder')
    source = Path(source)
    with gzip.open(source, 'rt', encoding='utf-8') if source.suffix == '.gz' else source.open(encoding='utf-8') as stream:
        request = json.load(stream)
    request['archiveMode'] = mode
    request['executionMode'] = execution
    if passes is not None:
        request['passes'] = passes
    packet = json.dumps(request, separators=(',', ':'), allow_nan=False)
    input_path = output/'request.ndjson'
    input_path.write_text(packet+'\n', encoding='utf-8')
    start = perf_counter()
    result = subprocess.run(['dotnet', str(runtime), 'run', str(input_path), str(output/'drawings')],
                            capture_output=True, text=True, encoding='utf-8')
    elapsed = perf_counter()-start
    (output/'worker.log').write_text(result.stdout+'\n'+result.stderr, encoding='utf-8')
    rows = [json.loads(line) for line in result.stdout.splitlines() if line.strip()]
    if result.returncode or len(rows) != 1 or not rows[0].get('ok'):
        raise RuntimeError('Benchmark failed; see '+str(output/'worker.log'))
    folder = output/'drawings'/request['id']
    view = json.loads((folder/'view.json').read_text(encoding='utf-8-sig'))
    arms = []
    for arm in view['results']:
        path = folder/(arm['arm']+'.gojo.bin.gz')
        arms.append(dict(arm=arm['arm'], archiveBytes=path.stat().st_size,
            stageSha256=hashlib.sha256(json.dumps(arm['stages'], sort_keys=True, separators=(',', ':')).encode()).hexdigest(),
            stageCount=len(arm['stages']), timingMs=arm.get('timingMs'), archiveMode=arm.get('archiveMode', 'history'),
            executionMode=arm.get('executionMode', 'audit'), passes=arm['passes'], live=arm.get('live')))
    report = dict(runtime=str(runtime), runtimeSha256=hashlib.sha256(Path(runtime).read_bytes()).hexdigest(),
        sourceRequest=str(source.resolve()), sourceRequestSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
        requestedMode=mode, executionMode=execution, workerSeconds=elapsed, viewBytes=(folder/'view.json').stat().st_size,
        sourceSha256=view['sourceSha256'], modelSha256=view['modelSha256'], importMs=view.get('importMs'), arms=arms,
        scope='One fresh worker process. Includes native import, inference, settlement, archive and independent restore. Excludes build, Python proposal preparation and HTML diagnostics.')
    (output/'benchmark.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', type=Path, required=True)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--mode', choices=('current', 'history'), default='current')
    parser.add_argument('--execution', choices=('live', 'audit'), default='audit')
    parser.add_argument('--passes', type=int)
    args = parser.parse_args()
    run(args.runtime, args.input, args.output, args.mode, args.execution, args.passes)
