"""Read-only, whole-drawing comparisons for the connected church experiment.

Writes only requested derived reports, never inputs, models, or native graphs.
"""
from __future__ import annotations
from collections import Counter, defaultdict
import argparse
import json
import math
from pathlib import Path
import random
from statistics import mean, median


def bezier(points,t):
    while len(points)>1:
        points=[[(1-t)*a[k]+t*b[k] for k in (0,1)] for a,b in zip(points,points[1:])]
    return points[0]


def fixed_samples(initial,final):
    """Same 17 sample parameters per acquired active profile in every version.
    Missing intervals are counted separately; they never receive zero error.
    """
    by_source=defaultdict(list)
    for e in final['edges']:
        if not e['inferred']:by_source[e['source']].append(e)
    distances=[];missing=0;total=0
    for edge in initial['edges']:
        if edge['inferred']:continue
        for i in range(17):
            total+=1;t=i/16;source=bezier(edge['controls'],t)
            options=[]
            for candidate in by_source[edge['source']]:
                a,b=candidate['interval']
                if a-1e-7<=t<=b+1e-7 and b>a:
                    options.append(math.dist(source,bezier(candidate['controls'],max(0,min(1,(t-a)/(b-a))))))
            if options:distances.append(min(options))
            else:missing+=1
    return dict(mean=mean(distances) if distances else None,max=max(distances,default=None),
                missing=missing,total=total,covered=total-missing)


def incidence(graph):
    by_site=defaultdict(list)
    for e in graph['edges']:
        by_site[e['start']].append((e['source'] if not e['inferred'] else 'inferred','out'))
        by_site[e['end']].append((e['source'] if not e['inferred'] else 'inferred','in'))
    return sorted((s['kind'],tuple(sorted(by_site[s['id']]))) for s in graph['sites'])


def layer(stage,name):
    values=[e['demand'] for e in stage['errors'] if e['layer']==name and e['resolved']]
    return mean(values) if values else None


def bootstrap(values,seed=291043):
    if not values:return None
    rng=random.Random(seed);n=len(values)
    estimates=sorted(mean(values[rng.randrange(n)] for _ in range(n)) for _ in range(1000))
    return dict(drawings=n,mean=mean(values),median=median(values),percentile95=[estimates[25],estimates[974]])


def analyze(root,output):
    root=Path(root);views=[json.loads(p.read_text(encoding='utf-8-sig')) for p in sorted((root/'drawings').glob('*/view.json'))]
    rows=[];types=defaultdict(Counter);metrics=defaultdict(list);by_complexity=defaultdict(list)
    for page_index,view in enumerate(views):
        per={}
        for run in view['results']:
            first,last=run['stages'][0],run['stages'][-1];fixed=fixed_samples(first['graph'],last['graph'])
            data=dict(arm=run['arm'],fixedSamples=fixed,mappingStrain=layer(last,'mapping'),sourceStrain=layer(last,'source'),
                      attachmentStrain=layer(last,'attachment'),containmentStrain=layer(last,'containment'),
                      sourceCoverage=last.get('sourceCoverage'),components=last['components'],cycles=last['cycles'],
                      sites=len(last['graph']['sites']),rewrites=run['accepted'],laterRewrites=sum(s['applied'] for s in run['stages'][2:]),
                      passes=run['passes'],quiet=run['stopReason'].startswith('quiet'),
                      initialMappingStrain=layer(first,'mapping'),
                      numericRounds=sum(len(s['transfers']) for s in run['stages']),
                      roundsWithQuantumRemainder=sum(r['quantum']>0 for s in run['stages'] for r in s['transfers']))
            per[run['arm']]=data;metrics[run['arm']].append(data)
            types[run['arm']].update(s['kind'] for s in last['graph']['sites'])
        local=next(r for r in view['results'] if r['arm']=='local-models')['stages'][-1]['graph']
        coupled=next(r for r in view['results'] if r['arm']=='bidirectional')['stages'][-1]['graph']
        fixed=next(r for r in view['results'] if r['arm']=='fixed-scaffold')['stages'][-1]['graph']
        row=dict(id=view['id'],keyId=view['keyId'],page=page_index//12+1,card=page_index%12+1,
                 profiles=view.get('sourceProfiles'),unrepresentedProfiles=view.get('unrepresentedProfiles',0),
                 incidenceDiffersFromLocal=incidence(local)!=incidence(coupled),incidenceDiffersFromFixed=incidence(fixed)!=incidence(coupled),versions=per)
        rows.append(row);count=len(view['results'][0]['stages'][0]['graph']['edges'])
        by_complexity['1–8' if count<=8 else '9–16' if count<=16 else '17–32' if count<=32 else '33+'].append(row)
    summary={}
    for arm,data in metrics.items():
        aggregate={key:mean(r[key] for r in data if r[key] is not None) if any(r[key] is not None for r in data) else None
                   for key in ('mappingStrain','sourceStrain','attachmentStrain','containmentStrain','sourceCoverage','rewrites','laterRewrites','passes','quiet','components','cycles','sites')}
        aggregate.update(drawings=len(data),fixedSampleMean=mean(r['fixedSamples']['mean'] for r in data if r['fixedSamples']['mean'] is not None),
                         fixedSamples=sum(r['fixedSamples']['total'] for r in data),missingSamples=sum(r['fixedSamples']['missing'] for r in data),
                         jointTypes=dict(types[arm]),numericRounds=sum(r['numericRounds'] for r in data),
                         roundsWithQuantumRemainder=sum(r['roundsWithQuantumRemainder'] for r in data))
        summary[arm]=aggregate
    differences=[];wins=Counter()
    for row in rows:
        a=row['versions']['bidirectional']['mappingStrain'];b=row['versions']['fixed-scaffold']['mappingStrain']
        if a is not None and b is not None:
            differences.append(a-b);wins['lower' if a<b-1e-8 else 'higher' if a>b+1e-8 else 'equal']+=1
    worst=sorted(rows,key=lambda r:r['versions']['bidirectional']['fixedSamples']['mean'] or 0,reverse=True)[:20]
    report=dict(drawings=len(rows),versions=summary,pairedMappingDifference=bootstrap(differences),pairedMappingOutcomes=dict(wins),
                jointIncidenceChangedFromLocal=sum(r['incidenceDiffersFromLocal'] for r in rows),
                jointIncidenceChangedFromFixed=sum(r['incidenceDiffersFromFixed'] for r in rows),
                drawingsWithUnrepresentedProfiles=sum(r['unrepresentedProfiles']>0 for r in rows),unrepresentedProfiles=sum(r['unrepresentedProfiles'] for r in rows),
                complexity={name:dict(drawings=len(data),meanBidirectionalDrift=mean(r['versions']['bidirectional']['fixedSamples']['mean'] or 0 for r in data),
                                     incidenceChanged=sum(r['incidenceDiffersFromLocal'] for r in data)) for name,data in by_complexity.items()},
                inspectHighSourceDisplacement=[{k:r[k] for k in ('id','keyId','page','card')} for r in worst],
                qualification='Fresh game-recognized drawings; descriptive authored strain and source displacement, not semantic accuracy. No final rendering is a target. A lower compatibility score can accompany a worse interpretation. Fixed-parameter source samples have a separate missing count. Joint-incidence signatures describe source participation and kinds; equality does not prove geometric equality. Bootstrap resamples complete drawings.')
    output=Path(output);output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
    output.with_name(output.stem+'-drawings.json').write_text(json.dumps(rows,separators=(',',':'),ensure_ascii=False),encoding='utf-8')
    return report


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('study',type=Path);p.add_argument('output',type=Path);args=p.parse_args()
    r=analyze(args.study,args.output);print(json.dumps({k:v for k,v in r.items() if k not in ('versions','inspectHighSourceDisplacement')},ensure_ascii=False))
