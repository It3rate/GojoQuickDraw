"""Bounded, resumable actual-church experiment; source datasets stay read-only.

This deliberately does not change Studio's default conversion or claim a learned
church recognizer. The linked native study executes all motion and topology.
"""
from __future__ import annotations

import argparse
from concurrent.futures import ProcessPoolExecutor, ThreadPoolExecutor, as_completed
from datetime import datetime, timezone
import gzip
import hashlib
import heapq
import json
from pathlib import Path
import subprocess
import time

from . import dataset, upstream

ARMS = ('local-models', 'shapes-only', 'fixed-scaffold', 'bidirectional')
SCHEMA = 'gojo.church-coupled-study.v2'
MAX_PASSES = 64


def select(path, limit, seed, excluded=()):
    """Uniform deterministic priority sample over explicitly recognized records."""
    if not 1 <= limit <= 10000:
        raise ValueError('Choose an explicit bounded sample of 1..10000 drawings')
    excluded=set(excluded)
    selected, seen, counts = [], set(), dict(records=0, recognized=0, unrecognized=0, unknown=0, malformed=0, duplicates=0, priorModelSources=0)
    digest = hashlib.sha256()
    opener = gzip.open if path.suffix == '.gz' else open
    with opener(path, 'rb') as stream:
        for raw in stream:
            digest.update(raw); counts['records'] += 1
            try:
                source = json.loads(raw)
            except ValueError:
                counts['malformed'] += 1
                continue
            value = source.get('recognized')
            counts['recognized' if value is True else 'unrecognized' if value is False else 'unknown'] += 1
            if value is not True:
                continue
            identity = source.get('key_id')
            if identity in seen:
                counts['duplicates'] += 1
                continue
            seen.add(identity)
            if str(identity) in excluded:
                counts['priorModelSources'] += 1
                continue
            text = raw.decode('utf-8-sig').rstrip('\r\n')
            sha = hashlib.sha256(text.encode()).hexdigest()
            rank = int.from_bytes(hashlib.sha256(f'{seed}:{identity}'.encode()).digest(), 'big')
            row = (-rank, sha, text)
            if len(selected) < limit:
                heapq.heappush(selected, row)
            elif row > selected[0]:
                heapq.heapreplace(selected, row)
    return [dict(id=sha[:40], keyId=str(json.loads(text)['key_id']), sourceSha256=sha, original=text) for _, sha, text in sorted(selected, reverse=True)], counts, digest.hexdigest()


def prepare_one(args):
    row, gojo_root, output = args
    path = Path(output) / 'inputs' / (row['id'] + '.json.gz')
    started = time.perf_counter()
    if path.is_file():
        with gzip.open(path, 'rt', encoding='utf-8') as stream:
            retained = json.load(stream)
        if hashlib.sha256(retained['packet']['sourceOriginalJson'].encode()).hexdigest() != row['sourceSha256']:
            raise ValueError('A retained source snapshot has changed')
        return dict(id=row['id'], ok=True, retained=True)
    try:
        packet = dataset.build_packet(row['original'], Path(gojo_root), row['id'], category='church')
        from .layout import estimate
        from .layout_mapping import build
        from .neighborhood_training import source_packet
        scaffold = estimate(packet['observation'])
        mapping = build(packet['observation'], scaffold)
        request = dict(id=row['id'], packet=source_packet(packet), scaffold=scaffold, mapping=mapping)
        path.parent.mkdir(parents=True, exist_ok=True)
        dataset._gzip_json(path, request)
        return dict(id=row['id'], ok=True, spans=len(packet['observation']['spans']), parts=len(scaffold['parts']), seconds=time.perf_counter()-started)
    except Exception as error:
        return dict(id=row['id'], ok=False, error=f'{type(error).__name__}: {error}')


def fingerprint():
    paths = [path for folder in ('church','learning','native','shared','gojo_quickdraw')
             for path in upstream.ROOT.joinpath(folder).glob('*') if path.suffix in ('.cs','.py','.csproj')]
    sha = hashlib.sha256()
    for path in sorted(paths):
        if path.is_file():
            sha.update(path.relative_to(upstream.ROOT).as_posix().encode()); sha.update(b'\0'); sha.update(path.read_bytes())
    return sha.hexdigest()


def build(gojo_root, work):
    target = work / 'bin'
    result = subprocess.run(['dotnet', 'build', str(upstream.ROOT/'church/GojoQuickDraw.ChurchStudy.csproj'), '-c', 'Release',
                             '--verbosity', 'quiet', f'-p:GojoRoot={gojo_root}', '-o', str(target)], capture_output=True, text=True)
    (work/'build.log').write_text(result.stdout + result.stderr, encoding='utf-8')
    if result.returncode:
        raise RuntimeError('Church study build failed: ' + result.stdout[-5000:] + result.stderr[-2000:])
    return ['dotnet', str(target/'GojoQuickDraw.ChurchStudy.dll')]


def execute_batch(command, path, output, log):
    with log.open('w', encoding='utf-8') as err:
        result = subprocess.run([*command, 'run', str(path), str(output/'drawings')], stdout=subprocess.PIPE, text=True, encoding='utf-8', stderr=err)
    rows = [json.loads(line) for line in result.stdout.splitlines() if line.strip()]
    if result.returncode:
        raise RuntimeError(f'Native church worker failed; see {log}')
    return rows


def run(args):
    source = args.input.resolve(); output = args.output.resolve(); gojo = upstream.resolve(args.gojo_root)
    if output == source or output in source.parents or source in output.parents:
        raise ValueError('Choose a new study folder separate from the source file')
    if not 1 <= args.workers <= 8 or not 1 <= args.passes <= MAX_PASSES or not 1 <= args.steps <= 8 or not 1 <= args.candidates <= 256:
        raise ValueError('Choose bounded worker, iteration and candidate budgets')
    output.mkdir(parents=True, exist_ok=True)
    work = Path(args.work or Path('D:/_quickDraw/_work')/output.name).resolve(); work.mkdir(parents=True, exist_ok=True)
    model = args.model.resolve(); model_bytes = model.read_bytes()
    config = dict(input=str(source), limit=args.limit, seed=args.seed, passes=args.passes, steps=args.steps,
                  candidates=args.candidates, arms=list(ARMS), modelSha256=hashlib.sha256(model_bytes).hexdigest(),
                  adapterSha256=fingerprint(), upstream=upstream.fingerprint(gojo))
    state_path = output/'study.json'
    if state_path.is_file():
        prior = json.loads(state_path.read_text(encoding='utf-8'))
        if prior['config'] != config:
            raise ValueError('This study retains different source, model, implementation or budgets; choose a new output folder')
    elif any(output.iterdir()):
        raise ValueError('A new study requires an empty folder')
    state = dict(schema=SCHEMA, config=config, createdUtc=datetime.now(timezone.utc).isoformat(), status='selecting',
                 adapterRevision=subprocess.run(['git','-C',str(upstream.ROOT),'rev-parse','HEAD'],check=True,capture_output=True,text=True).stdout.strip(),
                 qualification='Game recognition, not human recognition. No new model fitting in this run. Existing local models plus authored native constructions and explicit error responses.')
    dataset._atomic_json(state_path, state)
    with dataset._writer_lock(output):
        model_metadata=json.loads(model_bytes)
        excluded={str(row['key']) for row in model_metadata.get('sourceSnapshot',[])}
        rows, counts, source_hash = select(source, args.limit, args.seed,excluded)
        state.update(sourceCounts=counts, sourceSha256=source_hash, selected=len(rows), status='preparing')
        dataset._atomic_json(state_path, state)
        (output/'models').mkdir(exist_ok=True); (output/'models/local-providers.json').write_bytes(model_bytes)
        cohort = [dict(id=r['id'],keyId=r['keyId'],sourceSha256=r['sourceSha256'],split=('train','validation','test')[
            0 if int.from_bytes(hashlib.sha256(f'{args.seed}:{r["keyId"]}'.encode()).digest()[:4],'big')%100<70 else
            1 if int.from_bytes(hashlib.sha256(f'{args.seed}:{r["keyId"]}'.encode()).digest()[:4],'big')%100<85 else 2]) for r in rows]
        dataset._atomic_json(output/'cohort.json',cohort)
        prepared=[]
        with ProcessPoolExecutor(max_workers=args.workers) as pool:
            futures=[pool.submit(prepare_one,(row,str(gojo),str(output))) for row in rows]
            for future in as_completed(futures):
                prepared.append(future.result())
                if len(prepared)%10==0 or len(prepared)==len(rows):
                    state.update(prepared=len(prepared),preparationErrors=sum(not r['ok'] for r in prepared));dataset._atomic_json(state_path,state)
                    print(f'Prepared {len(prepared)}/{len(rows)}; errors {state["preparationErrors"]}',flush=True)
        dataset._atomic_json(output/'preparation.json',prepared)
        successes={r['id'] for r in prepared if r['ok']}
        requests=[]
        for row in rows:
            if row['id'] not in successes or (output/'drawings'/row['id']/'view.json').is_file():
                continue
            with gzip.open(output/'inputs'/(row['id']+'.json.gz'),'rt',encoding='utf-8') as stream:
                request=json.load(stream)
            request.update(modelPath=str(output/'models/local-providers.json'),arms=list(ARMS),passes=args.passes,steps=args.steps,candidates=args.candidates)
            requests.append(request)
        command=build(gojo,work);state['status']='running';dataset._atomic_json(state_path,state)
        from .church_study_review import write
        write(output)
        batches=[]
        for start in range(0,len(requests),4):
            path=work/f'batch-{start//4:05d}.ndjson'
            path.write_text(''.join(json.dumps(r,separators=(',',':'),allow_nan=False)+'\n' for r in requests[start:start+4]),encoding='utf-8')
            batches.append((path,work/f'batch-{start//4:05d}.log'))
        completed=[]
        with ThreadPoolExecutor(max_workers=args.workers) as pool:
            futures=[pool.submit(execute_batch,command,path,output,log) for path,log in batches]
            for future in as_completed(futures):
                completed.extend(future.result())
                state.update(completed=sum(1 for _ in (output/'drawings').glob('*/view.json')),nativeErrors=sum(not r['ok'] for r in completed))
                dataset._atomic_json(state_path,state)
                if state['completed'] % 32 == 0 or state['completed'] == len(successes):
                    from .church_study_review import write
                    write(output)
                print(f'Completed {state["completed"]}/{len(successes)}; native errors {state["nativeErrors"]}',flush=True)
        dataset._atomic_json(output/'native-results.json',completed)
        state.update(status='complete' if state.get('nativeErrors',0)==0 and not state.get('preparationErrors',0) else 'complete-with-errors',
                     finishedUtc=datetime.now(timezone.utc).isoformat())
        dataset._atomic_json(state_path,state)
        from .church_study_review import write
        write(output)
    return state


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command',choices=('run','review'))
    parser.add_argument('--input',type=Path,default=Path('D:/_quickDraw/quickdraw/church.ndjson'))
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--gojo-root',type=Path)
    parser.add_argument('--model',type=Path,default=upstream.ROOT/'models/neighborhood-closure-2026-10-28.json')
    parser.add_argument('--limit',type=int,default=1024);parser.add_argument('--seed',type=int,default=291043)
    parser.add_argument('--workers',type=int,default=4);parser.add_argument('--passes',type=int,default=6)
    parser.add_argument('--steps',type=int,default=1);parser.add_argument('--candidates',type=int,default=48)
    parser.add_argument('--work',type=Path)
    args=parser.parse_args()
    if args.command=='review':
        from .church_study_review import write
        write(args.output.resolve())
    else:run(args)


if __name__=='__main__':main()
