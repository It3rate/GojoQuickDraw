"""A deliberately small, untrained joint-only retrieval baseline.

Reads saved native-observer caches. Identifiers connect nodes locally but never
enter descriptors; acquisition correspondence is used only for separate audit
statistics, never for ranking candidate finals. No point coordinates, category
labels, part labels, source-profile identities or pen timestamps enter scores.
"""
from collections import Counter
import argparse
import gzip
import hashlib
import json
import math
from pathlib import Path
from statistics import median

REVERSE={'right':'left','left':'right','up':'down','down':'up','up-right':'down-left',
         'down-left':'up-right','up-left':'down-right','down-right':'up-left',
         'above':'below','below':'above','aligned':'aligned','level':'level'}


def descriptor(graph):
    nodes={p['id']:p['kind'] for p in graph['nodes'] if p['kind'] not in ('I','endpoint')}
    counts=Counter(nodes.values());links=Counter();orders=Counter()
    # A local graph edge retains adjacency and coarse heading. Smooth degree-2
    # runs are compressed for this read so curve sampling density is not a motif.
    all_nodes={p['id']:p['kind'] for p in graph['nodes']};adj={p:[] for p in all_nodes}
    seen=set()
    for link in graph['links']:
        a,b,d=link['from'],link['to'],link['order']
        if a not in adj or b not in adj:continue
        key=min((a,b,d),(b,a,REVERSE.get(d,d)))
        if key in seen:continue
        seen.add(key);adj[a].append((b,d));adj[b].append((a,REVERSE.get(d,d)))
    def pair(a,b,*direction):
        return min((a,b,*direction),(b,a,*(REVERSE.get(d,d) for d in direction)))
    for start in nodes:
        for after,heading in adj[start]:
            previous=start;current=after;visited={start}
            while current not in nodes and all_nodes[current]=='I' and len(adj[current])==2 and current not in visited:
                visited.add(current);nxt=[p for p in adj[current] if p[0]!=previous]
                if len(nxt)!=1:break
                previous,current=current,nxt[0][0]
            if current in nodes and start!=current:links[pair(nodes[start],nodes[current],heading)]+=.5
    for order in graph['orders']:
        a,b=order['from'],order['to']
        if a in nodes and b in nodes:orders[pair(nodes[a],nodes[b],order['horizontal'],order['vertical'])]+=1
    return {'types':counts,'links':links,'orders':orders}


def cosine(a,b):
    length=math.sqrt(sum(v*v for v in a.values())*sum(v*v for v in b.values()))
    return sum(v*b.get(k,0) for k,v in a.items())/length if length else 0


def score(source, final, relational=True):
    if not relational:return cosine(source['types'],final['types'])
    return .25*cosine(source['types'],final['types'])+.35*cosine(source['links'],final['links'])+.40*cosine(source['orders'],final['orders'])


def ranks(scores, correct):
    own=scores[correct];higher=sum(s>own+1e-12 for s in scores);ties=sum(abs(s-own)<=1e-12 for s in scores)
    # Expected recall under random tie-breaking; never privilege the known key.
    return higher+(ties+1)/2,{str(k):min(1,max(0,(k-higher)/ties)) for k in (1,5,10)},\
        (sum(s<own-1e-12 for j,s in enumerate(scores) if j!=correct)+.5*(ties-1))/max(1,len(scores)-1)


def evaluate(dataset, limit=300):
    from .selection import selected_rows
    examples=[];missing=0;unique=set();categories=set();match_counts=Counter();same_order=total_order=0
    source_types=Counter();final_types=Counter();fingerprints=Counter();limited=0
    for folder,row in selected_rows(Path(dataset),limit=limit):
        if row['sourceSha256'] in unique:continue
        unique.add(row['sourceSha256'])
        categories.add(row.get('category','unknown'))
        with gzip.open(folder/row['evidencePath'],'rt',encoding='utf-8') as stream:packet=json.load(stream)
        read=packet.get('jointComparison')
        if not read or not read['final']['nodes']:
            missing+=1;continue
        examples.append({'key':row['keyId'],'sha':row['sourceSha256'],'source':descriptor(read['source']),'final':descriptor(read['final'])})
        source_types.update(p['kind'] for p in read['source']['nodes']);final_types.update(p['kind'] for p in read['final']['nodes'])
        match_counts.update(p['status'] for p in read['matches']);total_order+=len(read['orderChecks'])
        same_order+=sum(p['same'] for p in read['orderChecks'])
        fingerprints[row.get('analysisFingerprint','unspecified')]+=1
        limited+=any(read[k]['orderLimitReached'] for k in ('source','final'))
    if len(categories)>1:raise ValueError('Choose one generated category folder for a within-category retrieval check')
    if len(examples)<2:raise ValueError('At least two drawings with saved joint comparisons are required')
    metrics={};details=[];n=len(examples)
    for relational in (False,True):
        rank_values=[];recalls=Counter();wins=[];hard_wins=[]
        for i,example in enumerate(examples):
            scores=[score(example['source'],other['final'],relational) for other in examples]
            rank,recall,win=ranks(scores,i);rank_values.append(rank);recalls.update(recall);wins.append(win)
            # Hard negatives share the category and have the closest final
            # joint-count profile. These labels select the evaluation pool,
            # not the model's input or score.
            own_count=sum(example['final']['types'].values())
            hard=sorted((j for j in range(n) if j!=i),key=lambda j:abs(sum(examples[j]['final']['types'].values())-own_count))[:min(20,n-1)]
            hard_wins.append(sum(1 if scores[i]>scores[j]+1e-12 else .5 if abs(scores[i]-scores[j])<=1e-12 else 0 for j in hard)/len(hard))
            if relational:
                top=sorted(range(n),key=lambda j:-scores[j])[:5]
                details.append({'key':example['key'],'rankWithTies':rank,'ownScore':scores[i],
                    'topCandidates':[{'key':examples[j]['key'],'score':scores[j]} for j in top]})
        metrics['typesAndRelations' if relational else 'typesOnly']={'recall':{k:v/n for k,v in recalls.items()},
            'medianRank':median(rank_values),'meanReciprocalMidRank':sum(1/r for r in rank_values)/n,
            'randomDistractorWinRate':sum(wins)/n,'similarCountDistractorWinRate':sum(hard_wins)/n}
    clear=match_counts['same type']+match_counts['changed type']
    return {'schema':'gojo.quickdraw-joint-retrieval.v1','drawings':n,'skippedWithoutCache':missing,'trained':False,
        'cohortSha256':hashlib.sha256('\n'.join(sorted(e['sha'] for e in examples)).encode()).hexdigest(),
        'analysisFingerprints':dict(fingerprints),'relativeOrderLimitReachedDrawings':limited,
        'chanceRecall':{str(k):min(k/n,1) for k in (1,5,10)},'retrieval':metrics,
        'correspondenceAudit':{'matchStatuses':dict(match_counts),'typeAgreementAmongUnambiguous':match_counts['same type']/max(1,clear),
            'relativeOrderAgreement':same_order/max(1,total_order),'relativeOrderChecks':total_order,
            'sourceTypeCounts':dict(source_types),'finalTypeCounts':dict(final_types)},
        'features':'Joint type counts, coarse edge headings and adjacency, relative above/below/left/right/aligned/level orders; no coordinates or cross-drawing identity features.',
        'qualification':'Descriptive retrieval on dependent authored outputs, not held-out training accuracy or semantic correctness. Pair identity is an evaluation label only. Histogram descriptors discard much graph structure. Ambiguous correspondences and missing final parts remain. Single-category evaluation avoids category shortcuts.',
        'examples':details}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dataset',type=Path,required=True);parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--limit',type=int,default=300)
    args=parser.parse_args()
    if args.limit<2:parser.error('--limit must be at least two')
    result=evaluate(args.dataset,args.limit)
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='examples'},indent=2))


if __name__=='__main__':main()
