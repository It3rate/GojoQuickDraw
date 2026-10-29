"""Replay a bounded selection of exact retained church inputs with more passes.

Only the pass budget changes. Cached fitting, scaffold hypotheses, associations,
models, inner-step count and candidate budget are reused. Every earlier pass is
compared with the original review before the replay is called complete.
"""
from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timezone
import gzip
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

from . import dataset, upstream
from .church_study import ARMS, MAX_PASSES, build, execute_batch, fingerprint
from .church_study_review import write


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def selection_rows(selection):
    rows = json.loads(Path(selection).read_text(encoding='utf-8-sig'))
    if not isinstance(rows, list) or not 1 <= len(rows) <= 32:
        raise ValueError('Select 1..32 retained drawings explicitly')
    rows = [dict(id=row) if isinstance(row, str) else row for row in rows]
    ids = [row.get('id') if isinstance(row, dict) else None for row in rows]
    if any(not isinstance(v, str) or len(v) != 40 or any(c not in '0123456789abcdef' for c in v) for v in ids):
        raise ValueError('Each selected drawing needs its retained forty-character identity')
    if len(set(ids)) != len(ids):
        raise ValueError('A replay must not duplicate an acquisition')
    return rows


def compare_prefix(baseline, replay):
    for key in ('id', 'keyId', 'sourceSha256', 'modelSha256', 'sourceProfiles', 'unrepresentedProfiles', 'source'):
        if baseline.get(key) != replay.get(key):
            raise ValueError('Replay changed retained source or model field: ' + key)
    old = {r['arm']: r for r in baseline['results']}
    new = {r['arm']: r for r in replay['results']}
    if set(old) != set(ARMS) or set(new) != set(old):
        raise ValueError('Replay must retain the same four comparison versions')
    checks = []
    for arm, original in old.items():
        stages = original['stages']
        if new[arm]['stages'][:len(stages)] != stages:
            raise ValueError('Earlier pass changed in replay: ' + baseline['id'] + '/' + arm)
        checks.append(dict(id=baseline['id'], arm=arm, earlierPasses=len(stages)-1, exact=True))
    return checks


def run(args):
    source = args.source_study.resolve(); output = args.output.resolve()
    if source == output or source in output.parents or output in source.parents:
        raise ValueError('Choose a separate output folder; the source study stays read-only')
    if not 1 <= args.passes <= MAX_PASSES or not 1 <= args.workers <= 4:
        raise ValueError('Choose bounded pass and worker budgets')
    rows = selection_rows(args.selection)
    original = json.loads((source/'study.json').read_text(encoding='utf-8-sig'))
    if original.get('status') != 'complete' or args.passes <= original['config']['passes']:
        raise ValueError('Replay a completed study with a larger pass budget')
    model_path = source/'models/local-providers.json'
    model_bytes = model_path.read_bytes()
    if hashlib.sha256(model_bytes).hexdigest() != original['config']['modelSha256']:
        raise ValueError('Original provider bytes no longer match the retained study')
    cohort = {r['id']: r for r in json.loads((source/'cohort.json').read_text())}
    plans = []
    for row in rows:
        ident = row['id']
        if ident not in cohort:
            raise ValueError('Selected drawing is not in the source cohort: ' + ident)
        packet_path = source/'inputs'/(ident+'.json.gz')
        view_path = source/'drawings'/ident/'view.json'
        with gzip.open(packet_path, 'rt', encoding='utf-8') as stream:
            request = json.load(stream)
        baseline = json.loads(view_path.read_text(encoding='utf-8-sig'))
        source_hash = hashlib.sha256(request['packet']['sourceOriginalJson'].encode()).hexdigest()
        if request['id'] != ident or baseline['id'] != ident or source_hash != cohort[ident]['sourceSha256'] or baseline['sourceSha256'] != source_hash:
            raise ValueError('A retained acquisition no longer matches its cohort identity')
        plans.append(dict(id=ident, keyId=cohort[ident]['keyId'], reason=row.get('reason'),
                          inputSha256=digest(packet_path), baselineSha256=digest(view_path)))
    gojo = upstream.resolve(args.gojo_root)
    config = dict(sourceStudy=str(source), sourceStudySha256=digest(source/'study.json'),
                  selection=plans, passes=args.passes, steps=original['config']['steps'],
                  candidates=original['config']['candidates'], seed=original['config']['seed'],
                  arms=list(ARMS), modelSha256=original['config']['modelSha256'],
                  adapterSha256=fingerprint(), upstream=upstream.fingerprint(gojo))
    if output.exists() and any(output.iterdir()):
        previous_path = output/'study.json'
        if not previous_path.is_file() or json.loads(previous_path.read_text())['config'] != config:
            raise ValueError('This output retains a different experiment; choose a new folder')
    output.mkdir(parents=True, exist_ok=True)
    work = Path(args.work or Path('D:/_quickDraw/_work')/output.name).resolve()
    if work == source or source in work.parents or work in source.parents:
        raise ValueError('Build and temporary files must stay separate from the source study')
    work.mkdir(parents=True, exist_ok=True)
    state = dict(schema='gojo.church-duration-study.v1', config=config, selected=len(plans),
                 createdUtc=datetime.now(timezone.utc).isoformat(), status='preparing',
                 preparationErrors=0, nativeErrors=0,
                 adapterRevision=subprocess.run(['git','-C',str(upstream.ROOT),'rev-parse','HEAD'],check=True,capture_output=True,text=True).stdout.strip(),
                 qualification='Exact retained inputs and providers; only the outer pass budget changes. No new fitting, shape discovery, model training or final-cleanup target. Repeated opinions are not new evidence.')
    with dataset._writer_lock(output):
        dataset._atomic_json(output/'study.json',state)
        for name in ('inputs', 'models', 'baseline', 'drawings'):
            (output/name).mkdir(exist_ok=True)
        (output/'models/local-providers.json').write_bytes(model_bytes)
        dataset._atomic_json(output/'cohort.json', [cohort[p['id']] for p in plans])
        dataset._atomic_json(output/'selection.json', plans)
        requests = []
        for p in plans:
            ident = p['id']
            shutil.copyfile(source/'inputs'/(ident+'.json.gz'), output/'inputs'/(ident+'.json.gz'))
            shutil.copyfile(source/'drawings'/ident/'view.json', output/'baseline'/(ident+'.json'))
            if (output/'drawings'/ident/'view.json').is_file():
                continue
            with gzip.open(output/'inputs'/(ident+'.json.gz'), 'rt', encoding='utf-8') as stream:
                request = json.load(stream)
            request.update(modelPath=str(output/'models/local-providers.json'), arms=list(ARMS),
                           passes=args.passes, steps=config['steps'], candidates=config['candidates'])
            path = work/(ident+'.ndjson')
            path.write_text(json.dumps(request,separators=(',',':'),allow_nan=False)+'\n',encoding='utf-8')
            requests.append((path,work/(ident+'.log')))
        command = build(gojo,work) if requests else None
        state['status'] = 'running'; dataset._atomic_json(output/'study.json',state); write(output)
        completed = []
        with ThreadPoolExecutor(max_workers=args.workers) as pool:
            futures = [pool.submit(execute_batch,command,path,output,log) for path,log in requests]
            for future in as_completed(futures):
                completed.extend(future.result())
                state.update(completed=sum(1 for _ in (output/'drawings').glob('*/view.json')),
                             nativeErrors=sum(not r['ok'] for r in completed))
                dataset._atomic_json(output/'study.json',state); write(output)
                print(f'Completed {state["completed"]}/{len(plans)} longer drawing runs; errors {state["nativeErrors"]}',flush=True)
        dataset._atomic_json(output/'native-results.json',completed)
        checks = []
        for p in plans:
            ident = p['id']
            if (output/'drawings'/ident/'view.json').is_file():
                try:
                    checks.extend(compare_prefix(json.loads((output/'baseline'/(ident+'.json')).read_text(encoding='utf-8-sig')),
                                                 json.loads((output/'drawings'/ident/'view.json').read_text(encoding='utf-8-sig'))))
                except ValueError as error:
                    state.update(status='prefix-mismatch',error=str(error))
                    dataset._atomic_json(output/'study.json',state)
                    raise
            if digest(source/'inputs'/(ident+'.json.gz')) != p['inputSha256'] or digest(source/'drawings'/ident/'view.json') != p['baselineSha256']:
                raise ValueError('The source study changed during the replay')
        if digest(model_path) != config['modelSha256'] or digest(source/'study.json') != config['sourceStudySha256']:
            raise ValueError('The original model or study settings changed during replay')
        dataset._atomic_json(output/'prefix-verification.json',dict(versions=len(checks),checks=checks,sourceUnchanged=True))
        state.update(completed=sum(1 for _ in (output/'drawings').glob('*/view.json')),
                     status='complete' if len(checks)==len(plans)*len(ARMS) and not state['nativeErrors'] else 'complete-with-errors',
                     finishedUtc=datetime.now(timezone.utc).isoformat())
        dataset._atomic_json(output/'study.json',state); write(output)
    return state


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-study',type=Path,required=True)
    parser.add_argument('--selection',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--passes',type=int,default=64)
    parser.add_argument('--workers',type=int,default=2)
    parser.add_argument('--gojo-root',type=Path)
    parser.add_argument('--work',type=Path)
    result = run(parser.parse_args())
    if result['status'] != 'complete':
        raise SystemExit('Replay has explicit incomplete or failed versions; inspect its retained outcomes')


if __name__ == '__main__':
    main()
