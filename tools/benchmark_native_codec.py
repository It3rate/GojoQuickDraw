"""Compare native bridge codecs on one retained evidence packet, without fitting."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
from time import perf_counter


def run(runtime, packet_path, output):
    output.mkdir(parents=True, exist_ok=True)
    if any(output.iterdir()):
        raise ValueError('Choose an empty benchmark folder')
    with gzip.open(packet_path, 'rt', encoding='utf-8') as stream:
        packet = json.load(stream)
    started = perf_counter()
    result = subprocess.run(['dotnet', str(runtime), 'encode', '--output', str(output/'native')],
        input=json.dumps(packet, separators=(',', ':'), allow_nan=False)+'\n', capture_output=True, text=True, encoding='utf-8')
    elapsed = perf_counter()-started
    (output/'worker.log').write_text(result.stderr, encoding='utf-8')
    response = json.loads(result.stdout)
    if result.returncode or not response.get('ok'):
        raise RuntimeError(result.stdout+'\n'+result.stderr)
    (output/'response.json').write_text(json.dumps(response, separators=(',', ':')), encoding='utf-8')
    archive = output/'native'/response['archive']
    report = dict(runtimeSha256=hashlib.sha256(runtime.read_bytes()).hexdigest(),
        packet=str(packet_path.resolve()), packetSha256=hashlib.sha256(packet_path.read_bytes()).hexdigest(),
        workerSeconds=elapsed, archiveBytes=archive.stat().st_size, archive=archive.name,
        stageSha256=hashlib.sha256(json.dumps(response.get('stageViews'), sort_keys=True, separators=(',', ':')).encode()).hexdigest(),
        jointsSha256=hashlib.sha256(json.dumps(response.get('jointComparison'), sort_keys=True, separators=(',', ':')).encode()).hexdigest(),
        timingMs={name:response[name] for name in ('importMs','encodeMs','restoreMs') if name in response},
        scope='One fresh native worker process: import retained packet, encode, compress, independently restore, read stage/joint views. Excludes fitting, authored proposal preparation and build.')
    (output/'benchmark.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', type=Path, required=True)
    parser.add_argument('--packet', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    run(args.runtime, args.packet, args.output)
