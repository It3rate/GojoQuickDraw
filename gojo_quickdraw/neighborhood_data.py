"""Known local constructions, distorted observations, and cautious final guides."""
import math
import random


def controls(count=1800, seed=43, batch_size=40):
    batch = []
    for index in range(count):
        kind = index % 18
        family = index // 72
        base = random.Random(f'neighborhood:{seed}:{kind}:{family}')
        rng = random.Random(f'variation:{seed}:{index}')
        gap = rng.uniform(.008, .075)
        tail = rng.uniform(.008, .065)
        source, target = {'sites': [], 'edges': []}, {'sites': [], 'edges': []}

        def site(graph, p):
            graph['sites'].append(list(p)); return len(graph['sites'])-1

        def edge(graph, a, b, handles=None):
            graph['edges'].append({'a': a, 'b': b, 'handles': handles or []})

        def line(graph, a, b, noisy=False):
            handles = None
            if noisy:
                p, q = graph['sites'][a], graph['sites'][b]
                bend = rng.uniform(-.025, .025)
                dx, dy = q[0]-p[0], q[1]-p[1]
                handles = [[p[0]+dx/3-dy*bend, p[1]+dy/3+dx*bend],
                           [p[0]+2*dx/3-dy*bend, p[1]+2*dy/3+dx*bend]]
            edge(graph, a, b, handles)

        def star(graph, vectors):
            c = site(graph, (0, 0))
            for point in vectors:
                line(graph, c, site(graph, point))

        names = ('corner-gap', 'tee-gap', 'tee-overrun', 'crossing', 'continuation-gap', 'separate-parallel',
                 'smooth-arch', 'clean-corner', 'corner-double-overrun', 'fragmented-tee', 'jogged-tee', 'terminal-hook',
                 'corner-and-endpoint', 'endpoint-to-body', 'straighten-then-tee', 'trim-tee-to-corner', 'trim-cross-to-tee', 'keep-short-tee-stem')
        if kind in (0, 7):
            angle = base.uniform(.75, 2.25)
            p = (math.cos(angle), math.sin(angle))
            if kind == 0:
                line(source, site(source, (-1, 0)), site(source, (-gap/2, 0)), rng.random() < .5)
                line(source, site(source, (gap/2, 0)), site(source, p), rng.random() < .5)
            else:
                c = site(source, (0, 0)); line(source, site(source, (-1, 0)), c); line(source, c, site(source, p))
            star(target, [(-1, 0), p])
        elif kind in (1, 2, 3, 8):
            if kind == 8:
                line(source, site(source, (-1, 0)), site(source, (tail, 0)))
                line(source, site(source, (0, -tail)), site(source, (0, 1)))
                star(target, [(-1, 0), (0, 1)])
            else:
                line(source, site(source, (-1, 0)), site(source, (1, 0)), rng.random() < .3)
                start = gap if kind == 1 else -tail if kind == 2 else -1
                line(source, site(source, (0, start)), site(source, (0, 1)), rng.random() < .3)
                star(target, [(-1, 0), (1, 0), (0, 1)] + ([(0, -1)] if kind == 3 else []))
        elif kind == 4:
            line(source, site(source, (-1, 0)), site(source, (-gap/2, rng.uniform(-.035, .035))))
            line(source, site(source, (gap/2, rng.uniform(-.035, .035))), site(source, (1, 0)))
            star(target, [(-1, 0), (1, 0)])
        elif kind == 5:
            distance = base.uniform(.035, .25)
            for graph in (source, target):
                line(graph, site(graph, (-1, 0)), site(graph, (1, 0)))
                line(graph, site(graph, (-1, distance)), site(graph, (1, distance)))
        elif kind == 6:
            height = base.uniform(.25, 1.4)
            for graph in (source, target):
                edge(graph, site(graph, (-1, 0)), site(graph, (1, 0)), [[-1, height], [1, height]])
        elif kind == 9:
            for p, q in [((-1, 0), (-gap, 0)), ((gap, 0), (1, 0)), ((0, gap), (0, 1))]:
                line(source, site(source, p), site(source, q))
            star(target, [(-1, 0), (1, 0), (0, 1)])
        elif kind == 10:
            a = site(source, (-gap/3, -gap/3)); b = site(source, (gap/3, gap/3))
            line(source, site(source, (-1, 0)), a); line(source, a, b)
            line(source, b, site(source, (1, 0))); line(source, b, site(source, (0, 1)))
            star(target, [(-1, 0), (1, 0), (0, 1)])
        elif kind == 11:
            c = site(source, (1, 0)); line(source, site(source, (-1, 0)), c)
            line(source, c, site(source, (1, tail)))
            line(target, site(target, (-1, 0)), site(target, (1, 0)))
        elif kind == 12:
            c=site(source,(0,0));line(source,site(source,(-1,0)),c);line(source,c,site(source,(0,1)))
            line(source,site(source,(gap,0)),site(source,(1,0)));star(target,[(-1,0),(1,0),(0,1)])
        elif kind == 13:
            # A shared far end used to exclude this entire segment pair.
            a=site(source,(-1,0));b=site(source,(1,0));e=site(source,(0,gap))
            line(source,a,b);edge(source,b,e,[[1,1],[-1,1]])
            a=site(target,(-1,0));b=site(target,(1,0));c=site(target,(0,0))
            line(target,a,c);line(target,c,b);edge(target,b,c,[[1,1],[-1,1]])
        elif kind == 14:
            rise=base.uniform(.16,.22)
            edge(source,site(source,(-1,0)),site(source,(1,0)),[[-1/3,rise],[1/3,rise]])
            line(source,site(source,(0,-gap)),site(source,(0,-1)));star(target,[(-1,0),(1,0),(0,-1)])
        else:
            vectors=[(-1,0),(0,1)] if kind==15 else [(-1,0),(1,0),(0,1)]
            if kind==17:
                star(source,[(-1,0),(1,0),(0,tail)]);star(target,[(-1,0),(1,0),(0,tail)])
            else:
                star(source,vectors+([(tail,0)] if kind==15 else [(0,-tail)]));star(target,vectors)
        line_targets = [0 if kind == 6 else 1 for _ in source['edges']]
        if kind==13:line_targets[1]=0
        angle, scale = rng.uniform(-math.pi, math.pi), rng.uniform(25, 180)
        offset = [rng.uniform(-120, 350), rng.uniform(-120, 350)]
        def transform(p):
            c, s = math.cos(angle), math.sin(angle)
            return [offset[0]+scale*(c*p[0]-s*p[1]), offset[1]+scale*(s*p[0]+c*p[1])]
        for graph in (source, target):
            graph['sites'] = [transform(p) for p in graph['sites']]
            for e in graph['edges']:
                e['handles'] = [transform(p) for p in e['handles']]
        batch.append({'group': f'neighborhood:{seed}:{kind}:{family}', 'kind': names[kind],
                      'source': source, 'target': target, 'lineTargets': line_targets})
        if len(batch) >= batch_size:
            yield {'cases': batch}; batch = []
    if batch:
        yield {'cases': batch}


def weak_goals(packet):
    comparison = packet.get('jointComparison', {})
    source = {n['id']: n for n in comparison.get('source', {}).get('nodes', [])}
    mappings = {row['finalEdge']: row['sourceProfiles'] for row in comparison.get('lineMatches', [])}
    matches = {m['target']: m for m in comparison.get('matches', [])}
    result, skipped = [], 0
    for target in comparison.get('final', {}).get('nodes', []):
        match = matches.get(target['id'])
        if not match or target.get('added') or target['kind'] not in ('I', 'L', 'T', 'X', 'Y', 'K'):
            continue
        on, directions = target.get('on', []), target.get('directions', [])
        if len(on) != len(directions) or len(on) < 2 or any(not mappings.get(p['edge']) for p in on):
            skipped += 1; continue
        candidates = match.get('candidates', [])
        if not candidates or any(key not in source for key in candidates):
            skipped += 1; continue
        rays = []
        for p, direction in zip(on, directions):
            profiles = mappings[p['edge']]
            if any(not text.startswith('p') or not text[1:].isdigit() for text in profiles):
                break
            rays.append({'profiles': [int(text[1:]) for text in profiles], 'direction': direction})
        else:
            result.append({'sourceJoints': [int(key.removeprefix('source-j')) for key in candidates], 'rays': rays})
    return result, skipped


def closure_controls(count=1200, seed=43, batch_size=40):
    """Known missing links and open-route counterexamples; labels are authored.

    Four distortions share a template group before train/validation/test split.
    These controls do not establish that a near-loop in real ink was intended
    to be closed. Acquired timing remains absent rather than manufactured.
    """
    names=('rectangle-corner-gap','rectangle-edge-gap','triangle-gap','circular-gap',
           'open-hairpin','wide-open-box','branched-route','open-shallow-return')
    batch=[]
    for index in range(count):
        kind=index%len(names);family=index//(4*len(names))
        base=random.Random(f'closure-template:{seed}:{kind}:{family}')
        rng=random.Random(f'closure-variation:{seed}:{index}')
        width=base.uniform(.65,1.8);gap=rng.uniform(.008,.10)
        graph={'sites':[],'edges':[]}
        def path(points, handles=None):
            graph['sites']=[list(p) for p in points]
            graph['edges']=[{'a':i,'b':i+1,'handles':(handles or {}).get(i,[])} for i in range(len(points)-1)]
        if kind==0:path([(0,gap),(0,1),(width,1),(width,0),(gap,0)])
        elif kind==1:path([(width/2+gap/2,0),(width,0),(width,1),(0,1),(0,0),(width/2-gap/2,0)])
        elif kind==2:path([(-1+gap,0),(0,base.uniform(.7,1.4)),(1,0),(-1,0)])
        elif kind==3:
            missing=rng.uniform(.008,.12);start=missing/2;step=(2*math.pi-missing)/8
            angles=[start+i*step for i in range(9)];points=[(math.cos(a),math.sin(a)) for a in angles];handles={}
            k=4/3*math.tan(step/4)
            for i,(a,b) in enumerate(zip(angles,angles[1:])):
                handles[i]=[[math.cos(a)-k*math.sin(a),math.sin(a)+k*math.cos(a)],
                            [math.cos(b)+k*math.sin(b),math.sin(b)-k*math.cos(b)]]
            path(points,handles)
        elif kind==4:path([(0,0),(gap,0)],{0:[[0,1],[gap,1]]})
        elif kind==5:
            gap=rng.uniform(.16,.24)
            path([(width/2+gap/2,0),(width,0),(width,1),(0,1),(0,0),(width/2-gap/2,0)])
        elif kind==6:
            path([(0,gap),(0,1),(width,1),(width,0),(gap,0)])
            graph['sites'].append([width+1,1]);graph['edges'].append({'a':2,'b':5,'handles':[]})
        else:path([(0,0),(1,.10),(gap,0)])
        ends=[0,len(graph['sites'])-1 if kind!=6 else 4]
        angle=rng.uniform(-math.pi,math.pi);scale=rng.uniform(30,180);offset=[rng.uniform(-80,300),rng.uniform(-80,300)]
        def transform(p):
            c,s=math.cos(angle),math.sin(angle)
            return [offset[0]+scale*(c*p[0]-s*p[1]),offset[1]+scale*(s*p[0]+c*p[1])]
        graph['sites']=[transform(p) for p in graph['sites']]
        for edge in graph['edges']:edge['handles']=[transform(p) for p in edge['handles']]
        batch.append({'source':graph,'ends':ends,'label':int(kind<4),'kind':names[kind],
                      'group':f'closure:{seed}:{kind}:{family}'})
        if len(batch)>=batch_size:yield {'closureCases':batch};batch=[]
    if batch:yield {'closureCases':batch}
