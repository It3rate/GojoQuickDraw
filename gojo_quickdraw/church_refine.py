"""Bounded church run with the older experts compiled into native port errors."""
import argparse
from datetime import datetime, timezone
import gzip
import hashlib
import json
from pathlib import Path
import shutil

from . import dataset, upstream
from .church_study import build, execute_batch, fingerprint
from .church_replay import selection_rows
from .church_heuristics import compile_plan
from .church_refine_review import write


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def run(args):
    if getattr(args,'cad',False) and not getattr(args,'connections',False):
        raise ValueError('--cad requires --connections')
    source=args.source_study.resolve(); output=args.output.resolve()
    if output==source or source in output.parents or output in source.parents:
        raise ValueError('Keep the comparison source separate from the new experiment')
    if not 1<=args.passes<=16 or not 1<=args.steps<=8 or not 1<=args.limit<=32:
        raise ValueError('Use 1..32 drawings, 1..16 passes and 1..8 responses per pass')
    state=json.loads((source/'study.json').read_text(encoding='utf-8-sig'))
    if state['status']!='complete':
        raise ValueError('A completed, retained comparison study is required')
    selection=selection_rows(args.selection or source/'selection.json')[:args.limit]
    origins={row['id']:Path(row.get('study',source)).resolve() for row in selection}
    if len(origins)!=len(selection):
        raise ValueError('Do not count the same acquisition twice')
    source_states={str(root):digest(root/'study.json') for root in set(origins.values())}
    baseline_root=args.baseline_study.resolve() if getattr(args,'baseline_study',None) else None
    if baseline_root:
        if baseline_root==output or baseline_root in output.parents or output in baseline_root.parents:
            raise ValueError('Keep the read-only baseline separate from the output')
        baseline_state=json.loads((baseline_root/'study.json').read_text(encoding='utf-8-sig'))
        if baseline_state['status']!='complete' or baseline_state['config']['modelSha256']!=state['config']['modelSha256']:
            raise ValueError('The baseline must be complete and use the same provider bytes')
        source_states[str(baseline_root)]=digest(baseline_root/'study.json')
    for root in set(origins.values()):
        if root==output or root in output.parents or output in root.parents:
            raise ValueError('Keep every source study separate from the output')
        other=json.loads((root/'study.json').read_text(encoding='utf-8-sig'))
        if other['status']!='complete' or other['config']['modelSha256']!=state['config']['modelSha256']:
            raise ValueError('Additional cases require completed studies using the same provider bytes')
    model=source/'models/local-providers.json'
    if digest(model)!=state['config']['modelSha256']:
        raise ValueError('Retained provider bytes changed')
    work=Path(args.work or Path('D:/_quickDraw/_work')/output.name).resolve()
    if output==work or output in work.parents or work in output.parents or any(
            root==work or root in work.parents or work in root.parents for root in set(origins.values())|({baseline_root} if baseline_root else set())):
        raise ValueError('Keep temporary work separate from retained studies')
    work.mkdir(parents=True,exist_ok=True); output.mkdir(parents=True,exist_ok=True)
    config=dict(sourceStudy=str(source),sourceStudySha256=digest(source/'study.json'),selection=[p['id'] for p in selection],
        connections=getattr(args,'connections',False),cad=getattr(args,'cad',False),baselineStudy=str(args.baseline_study.resolve()) if getattr(args,'baseline_study',None) else None,
        inputStudies={ident:str(root) for ident,root in origins.items()},sourceStudyHashes=source_states,
        passes=args.passes,steps=args.steps,candidates=state['config']['candidates'],modelSha256=digest(model),
        adapterSha256=fingerprint(),upstream=upstream.fingerprint(upstream.resolve(args.gojo_root)))
    if any(output.iterdir()):
        if not (output/'study.json').is_file() or json.loads((output/'study.json').read_text())['config']!=config:
            raise ValueError('This folder retains a different experiment')
    report=dict(schema='gojo.church-heuristic-settlement.v1',config=config,status='preparing',
        createdUtc=datetime.now(timezone.utc).isoformat(),qualification='Same recognized acquisitions and learned local providers; added authored church experts. Native source-seeded parameter settlement, not copying final cleanup positions.')
    with dataset._writer_lock(output):
        for name in ('inputs','models','baseline','proposals','drawings'):
            (output/name).mkdir(exist_ok=True)
        shutil.copyfile(model,output/'models/local-providers.json')
        dataset._atomic_json(output/'selection.json',selection)
        dataset._atomic_json(output/'study.json',report)
        command=build(upstream.resolve(args.gojo_root),work)
        receipts=[]
        for index,row in enumerate(selection):
            ident=row['id']; origin=origins[ident]; input_path=origin/'inputs'/(ident+'.json.gz')
            input_hash=digest(input_path)
            with gzip.open(input_path,'rt',encoding='utf-8') as stream:
                retained=json.load(stream)
            baseline=(baseline_root or origin)/'drawings'/ident/'view.json'
            baseline_hash=digest(baseline)
            shutil.copyfile(baseline,output/'baseline'/(ident+'.json'))
            packet=retained['packet']
            source_hash=hashlib.sha256(packet['sourceOriginalJson'].encode()).hexdigest()
            earlier=json.loads(baseline.read_text(encoding='utf-8-sig'))
            if (retained['id']!=ident or earlier['id']!=ident or packet['source']['recognized'] is not True or
                    source_hash!=earlier['sourceSha256']):
                raise ValueError('Acquisition identity or recognition status changed')
            scaffold,mapping,heuristics,proposal=compile_plan(packet['observation'],retained['scaffold'],getattr(args,'connections',False),getattr(args,'cad',False))
            request={**retained,'scaffold':scaffold,'mapping':mapping,'heuristics':heuristics,
                     'arms':['bidirectional'],'passes':args.passes,'steps':args.steps,
                     'candidates':config['candidates'],
                     'modelPath':str(output/'models/local-providers.json')}
            with gzip.open(output/'inputs'/(ident+'.json.gz'),'wt',encoding='utf-8') as stream:
                json.dump(request,stream,separators=(',',':'),allow_nan=False)
            dataset._atomic_json(output/'proposals'/(ident+'.json'),dict(heuristics=heuristics,reference=proposal))
            path=work/(ident+'.ndjson');path.write_text(json.dumps(request,separators=(',',':'),allow_nan=False)+'\n',encoding='utf-8')
            if not (output/'drawings'/ident/'view.json').is_file() or not (output/'drawings'/ident/'bidirectional.gojo.bin.gz').is_file():
                batch=execute_batch(command,path,output,work/(ident+'.log'))
                if len(batch)!=1 or batch[0]['id']!=ident:
                    raise RuntimeError('Native worker did not return the requested acquisition identity')
                receipts.extend(batch)
            if digest(input_path)!=input_hash or digest(baseline)!=baseline_hash:
                raise ValueError('Source changed')
            report.update(status='running',completed=index+1,errors=sum(not r['ok'] for r in receipts))
            dataset._atomic_json(output/'study.json',report)
            write(output)
            print(f'Church heuristic settlement {index+1}/{len(selection)}; errors {report["errors"]}',flush=True)
        report.update(status='complete' if not report['errors'] else 'complete-with-errors',finishedUtc=datetime.now(timezone.utc).isoformat())
        if digest(model)!=config['modelSha256'] or any(digest(Path(root)/'study.json')!=sha for root,sha in source_states.items()):
            raise ValueError('Source study or providers changed during this experiment')
        dataset._atomic_json(output/'native-results.json',receipts)
        dataset._atomic_json(output/'study.json',report);write(output)
    return report


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source-study',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    p.add_argument('--selection',type=Path,help='Optional explicit IDs; each row may name a completed source study')
    p.add_argument('--baseline-study',type=Path,help='Optional read-only comparison views from the same acquisitions')
    p.add_argument('--connections',action='store_true',help='Reconsider opening rows and test native capture/hold/release constructions')
    p.add_argument('--cad',action='store_true',help='Propose nearby collinearity, outward symmetry and repeated-row CAD relations')
    p.add_argument('--limit',type=int,default=10);p.add_argument('--passes',type=int,default=12)
    p.add_argument('--steps',type=int,default=4);p.add_argument('--work',type=Path);p.add_argument('--gojo-root',type=Path)
    state=run(p.parse_args())
    if state['status']!='complete':
        raise SystemExit('Some native runs failed; retained outcomes identify them')


if __name__=='__main__':
    main()
