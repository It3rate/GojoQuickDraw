"""Run connected church refinement from an existing fitted dataset, read-only."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import gzip
import hashlib
import heapq
import json
from pathlib import Path
import shutil
import sys
from time import perf_counter

from . import dataset, upstream
from .church_study import build, execute_batch, fingerprint
from .church_heuristics import compile_plan
from .church_refine_review import write
from .neighborhood_training import source_packet
from .selection import selected_rows

SCHEMA = 'gojo.church-dataset-refinement.v1'


def select(source, limit, order='category', seed=0):
    if isinstance(limit, bool) or not isinstance(limit, int) or not 1 <= limit <= 10000:
        raise ValueError('Choose an explicit count of 1..10000 churches')
    if order not in ('category', 'recent', 'random'):
        raise ValueError('Unknown inspection order')
    def candidates():
        seen = set()
        for folder, row in selected_rows(source, recognition='recognized'):
            if row.get('category', '').casefold() != 'church' or row['sourceSha256'] in seen:
                continue
            seen.add(row['sourceSha256'])
            yield folder, row
    if order == 'category':
        import itertools
        return list(itertools.islice(candidates(), limit))
    def rank(entry):
        folder, row = entry
        if order == 'random':
            return hashlib.sha256(f"{seed}:{row['sourceSha256']}".encode()).digest()
        return -datetime.fromisoformat(row['convertedUtc'].replace('Z', '+00:00')).timestamp(), row['sourceSha256']
    return heapq.nsmallest(limit, candidates(), key=rank)


def reference_stage(parts, index):
    """Display saved authored shapes only; never used as inference inputs."""
    return {'pass': index, 'graph': {'curves': [], 'sites': []},
        'parts': [dict(id=p['id'], role=p['role'], bounds=p['bounds'],
            points=p['geometry']['points'], segments=p['geometry']['segments'],
            prior=p.get('status') == 'prior-only' or bool(p.get('priorOnly')))
            for p in parts if p.get('geometry')], 'errors': [], 'applied': 0, 'moved': 0}


def prepare(packet, ident, model):
    from .layout import estimate
    if packet['source'].get('recognized') is not True:
        raise ValueError('Connected refinement currently requires game-recognized churches')
    # Only the fitted source enters the proposal. A saved final interpretation is
    # a separate comparison and cannot influence inference or its parameters.
    initial = estimate(packet['observation'])
    scaffold, mapping, plan, proposal = compile_plan(packet['observation'], initial,
        connections=True, cad=True, recover_missing=True)
    request = dict(id=ident, packet=source_packet(packet), scaffold=scaffold, mapping=mapping,
        heuristics=plan, arms=['bidirectional'], candidates=48, modelPath=str(model))
    baseline = dict(id=ident, sourceSha256=hashlib.sha256(packet['sourceOriginalJson'].encode()).hexdigest(),
        results=[dict(arm='bidirectional', passBudget=1, passes=1, stages=[
            reference_stage(packet.get('layout', {}).get('parts', []), 0),
            reference_stage(packet.get('cleanup', {}).get('parts', []), 1)])])
    return request, baseline, proposal


def run(source, output, gojo_root, *, limit=12, passes=8, steps=2, order='category', seed=0, model=None, work=None, archive_mode='current', execution_mode='live'):
    if archive_mode not in ('current', 'history'):
        raise ValueError('Choose current native state or full audit history')
    if execution_mode not in ('live', 'audit') or execution_mode == 'live' and archive_mode != 'current':
        raise ValueError('Live settlement keeps current state only; choose audit execution for historical inspection')
    source, output = Path(source).resolve(), Path(output).resolve()
    if source == output or source in output.parents or output in source.parents:
        raise ValueError('Keep connected results separate from the source dataset')
    if not 1 <= passes <= 64 or not 1 <= steps <= 8:
        raise ValueError('Choose 1..64 passes and 1..8 responses per pass')
    cohort = select(source, limit, order, seed)
    if not cohort:
        raise ValueError('No completed, game-recognized church drawings in the selected folder')
    model = Path(model or upstream.ROOT/'models/neighborhood-closure-2026-10-28.json').resolve()
    model_hash = dataset.sha256(model)
    gojo = upstream.resolve(gojo_root)
    config = dict(sourceDataset=str(source), limit=limit, passes=passes, steps=steps, candidates=48,
        order=order, seed=seed, recognition='recognized', connections=True, cad=True, recoverMissing=True,
        baselineKind='saved-studio', archiveMode=archive_mode, executionMode=execution_mode, selection=[row['sourceSha256'][:40] for _, row in cohort],
        sourceRecords=[dict(folder=str(folder), itemId=row['itemId'], sourceSha256=row['sourceSha256'],
            evidenceSha256=row['evidenceSha256']) for folder, row in cohort],
        modelSha256=model_hash, adapterSha256=fingerprint(), upstream=upstream.fingerprint(gojo))
    work = Path(work or Path('D:/_quickDraw/_work/studio-refinement') /
        hashlib.sha256(str(output).encode()).hexdigest()[:16]).resolve()
    for protected in (source, output):
        if work == protected or work in protected.parents or protected in work.parents:
            raise ValueError('Keep temporary work separate from source and saved results')
    state_path = output/'study.json'
    previous = json.loads(state_path.read_text()) if state_path.exists() else None
    if previous is not None:
        if previous.get('schema') != SCHEMA or previous.get('config') != config:
            raise ValueError('This folder retains another selection or processing version; choose a new result folder')
    elif output.exists() and any(output.iterdir()):
        raise ValueError('Choose an empty connected result folder')
    output.mkdir(parents=True, exist_ok=True); work.mkdir(parents=True, exist_ok=True)
    report = dict(schema=SCHEMA, config=config, status='preparing',
        createdUtc=(previous or {}).get('createdUtc', datetime.now(timezone.utc).isoformat()),
        qualification='Read-only fitted source; saved Studio cleanup is comparison only. Frozen local models plus authored CAD/error relations. No new training or semantic accuracy claim.')
    completed, resumed, errors = 0, 0, 0
    timings = dict(preparation=0.0, build=0.0, nativeWorker=0.0, review=0.0)
    with dataset._writer_lock(output):
        for name in ('inputs', 'models', 'baseline', 'proposals', 'drawings'):
            (output/name).mkdir(exist_ok=True)
        shutil.copyfile(model, output/'models/local-providers.json')
        dataset._atomic_json(state_path, report)
        command = None
        for index, (folder, row) in enumerate(cohort):
            ident = row['sourceSha256'][:40]
            dataset.verify_row(folder, row)
            with gzip.open(dataset._safe_path(folder, row['evidencePath']), 'rt', encoding='utf-8') as stream:
                packet = json.load(stream)
            if hashlib.sha256(packet['sourceOriginalJson'].encode()).hexdigest() != row['sourceSha256']:
                raise ValueError('Saved acquisition does not match its index')
            result_folder = output/'drawings'/ident
            receipt = result_folder/'receipt.json'
            if receipt.is_file():
                saved = json.loads(receipt.read_text())
                if saved.get('version') == 2 and all(dataset.sha256(dataset._safe_path(output,name)) == sha for name, sha in saved['artifacts'].items()):
                    resumed += 1; completed += 1
                    continue
                raise ValueError('A saved connected result changed; preserve it and choose a new result folder')
            started = perf_counter()
            request, baseline, proposal = prepare(packet, ident, output/'models/local-providers.json')
            request.update(passes=passes, steps=steps, archiveMode=archive_mode, executionMode=execution_mode)
            dataset._gzip_json(output/'inputs'/(ident+'.json.gz'), request)
            dataset._atomic_json(output/'baseline'/(ident+'.json'), baseline)
            dataset._atomic_json(output/'proposals'/(ident+'.json'), dict(heuristics=request['heuristics'], reference=proposal))
            timings['preparation'] += (perf_counter()-started)*1000
            if command is None:
                print('Connected refinement: building native runtime', file=sys.stderr, flush=True)
                started = perf_counter()
                command = build(gojo, work)
                timings['build'] += (perf_counter()-started)*1000
            path = work/(ident+'.ndjson')
            path.write_text(json.dumps(request, separators=(',', ':'), allow_nan=False)+'\n', encoding='utf-8')
            started = perf_counter()
            rows = execute_batch(command, path, output, work/(ident+'.log'))
            timings['nativeWorker'] += (perf_counter()-started)*1000
            if len(rows) != 1 or rows[0]['id'] != ident:
                raise RuntimeError('Native worker returned the wrong acquisition')
            if rows[0]['ok']:
                view = json.loads((result_folder/'view.json').read_text(encoding='utf-8-sig'))
                if view['sourceSha256'] != row['sourceSha256'] or view['modelSha256'] != model_hash:
                    raise RuntimeError('Native result changed source or provider identity')
                artifacts=[result_folder/'view.json',result_folder/'bidirectional.gojo.bin.gz',
                    output/'inputs'/(ident+'.json.gz'),output/'baseline'/(ident+'.json'),
                    output/'proposals'/(ident+'.json'),output/'models/local-providers.json']
                dataset._atomic_json(receipt, dict(version=2,nativeRestored=True,archiveMode=archive_mode,executionMode=execution_mode,
                    artifacts={path.relative_to(output).as_posix():dataset.sha256(path) for path in artifacts}))
                completed += 1
            else:
                errors += 1
            dataset.verify_row(folder, row)
            report.update(status='running', completed=completed, errors=errors)
            dataset._atomic_json(state_path, report)
            # Small previews stay current; growing large cohorts refresh at
            # powers of two so repeated embedding is linear in total rows.
            if len(cohort)<=32 or completed and completed & (completed-1)==0:
                started = perf_counter()
                write(output)
                timings['review'] += (perf_counter()-started)*1000
            print(f'Connected refinement {index+1}/{len(cohort)}: {completed} complete, {errors} errors', file=sys.stderr, flush=True)
        report.update(status='complete' if not errors else 'complete-with-errors', completed=completed,
            errors=errors, resumed=resumed, finishedUtc=datetime.now(timezone.utc).isoformat())
        dataset._atomic_json(state_path, report)
        started = perf_counter()
        write(output)
        timings['review'] += (perf_counter()-started)*1000
        report['lastRunTimingMs'] = timings
        dataset._atomic_json(state_path, report)
    return dict(output=str(output), completed=completed, resumed=resumed, errors=errors, storedErrors=errors,
        review=str(output/'review.html'), passes=passes, recognition='recognized',archiveMode=archive_mode,executionMode=execution_mode,timingMs=timings)


def review(output):
    output=Path(output).resolve()
    state=json.loads((output/'study.json').read_text())
    if state.get('schema')!=SCHEMA:
        raise ValueError('Choose a connected Studio result folder')
    write(output)
    return dict(output=str(output),review=str(output/'review.html'),
        completed=sum((output/'drawings'/ident/'receipt.json').is_file() for ident in state['config']['selection']),
        errors=state.get('errors',0),storedErrors=state.get('errors',0))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dataset', type=Path); parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--review-only',action='store_true',help='Refresh completed saved results without running settlement')
    parser.add_argument('--gojo-root', type=Path); parser.add_argument('--model', type=Path)
    parser.add_argument('--limit', type=int, default=12); parser.add_argument('--passes', type=int, default=8)
    parser.add_argument('--steps', type=int, default=2); parser.add_argument('--seed', type=int, default=0)
    parser.add_argument('--order', choices=('category', 'recent', 'random'), default='category')
    parser.add_argument('--work', type=Path)
    parser.add_argument('--execution-mode', choices=('live', 'audit'), default='live',
        help='One live native world with initial/final views, or adaptive rebuilding with inspectable passes')
    parser.add_argument('--archive-mode', choices=('current', 'history'), default='current',
        help='Current executable native state plus visual passes, or the larger full proof history')
    args = parser.parse_args()
    try:
        if args.review_only:
            print(json.dumps(review(args.output)));return 0
        if args.dataset is None:
            raise ValueError('Choose a source dataset folder')
        result = run(args.dataset, args.output, args.gojo_root, limit=args.limit, passes=args.passes,
            steps=args.steps, seed=args.seed, order=args.order, model=args.model, work=args.work, archive_mode=args.archive_mode, execution_mode=args.execution_mode)
        print(json.dumps(result)); return 1 if result['errors'] else 0
    except Exception as error:
        print(str(error), file=sys.stderr); return 2


if __name__ == '__main__':
    raise SystemExit(main())
