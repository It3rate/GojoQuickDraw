"""Bounded read-only snapshots and controlled local construction examples."""
from __future__ import annotations

from collections import defaultdict
import gzip
import hashlib
import itertools
import json
import math
from pathlib import Path
import random

from . import dataset, recognition as recognition_read
from .selection import datasets

NAMES = ('separate', 'continuation', 'corner', 'tee', 'crossing')


def split(group, seed=31):
    value = int.from_bytes(hashlib.sha256(f'{seed}:{group}'.encode()).digest()[:4], 'big') % 100
    return 'train' if value < 70 else 'validation' if value < 85 else 'test'


def snapshot(path, limit=300, seed=31, recognition='all'):
    """Read fixed committed journal prefixes; never repair a writer's index/cache."""
    recognition_read.validate(recognition)
    if not 1 <= limit <= 5000:
        raise ValueError('Choose 1..5000 source drawings for this bounded pilot')
    latest, warnings = {}, []
    for folder, _ in datasets(path):
        journal = folder / 'index.ndjson'
        if not journal.is_file():
            continue
        with journal.open('rb') as stream:
            size = journal.stat().st_size
            content = stream.read(size)
        if content and not content.endswith(b'\n'):
            content = content[:content.rfind(b'\n') + 1]
            warnings.append(f'{folder.name}: ignored an incomplete final index entry in the live snapshot')
        per_id = {}
        for line in content.splitlines():
            if line.strip():
                row = json.loads(line)
                per_id[row['itemId']] = row
        for row in per_id.values():
            if row.get('status') != 'ok':
                continue
            if recognition != 'all' and not recognition_read.accepts(recognition_read.from_row(folder, row), recognition):
                continue
            key = row['sourceSha256']
            prior = latest.get(key)
            if prior is None or row.get('convertedUtc', '') > prior[1].get('convertedUtc', ''):
                latest[key] = folder, row
    ordered = sorted(latest, key=lambda key: hashlib.sha256(f'{seed}:{key}'.encode()).digest())[:limit]
    return [latest[key] for key in ordered], warnings


def load_packet(folder, row):
    path = dataset._safe_path(folder, row['evidencePath'])
    if dataset.sha256(path) != row['evidenceSha256']:
        raise ValueError('Training source changed after snapshot; retry against a fresh snapshot')
    with gzip.open(path, 'rt', encoding='utf-8') as stream:
        packet = json.load(stream)
    if packet['itemKey'] != row['itemId'] or packet['source']['key_id'] != row['keyId'] or hashlib.sha256(packet['sourceOriginalJson'].encode()).hexdigest() != row['sourceSha256']:
        raise ValueError('Training acquisition differs from its committed journal')
    return packet


def real_request(packet, group, max_patches=24, *, inference=False):
    spans = packet['observation']['spans']
    if len(spans) < 2:
        return None
    degenerate = {i for i, span in enumerate(spans) if len({tuple(round(value*1000) for value in point) for point in span['controls']}) < 2}
    labels = defaultdict(set)
    # Inference proposals must be independent of authored finals and labels.
    comparison = {} if inference else packet.get('jointComparison', {})
    nodes = {node['id']: node for node in comparison.get('source', {}).get('nodes', [])}
    final = {node['id']: node for node in comparison.get('final', {}).get('nodes', [])}
    names = {'I': 1, 'L': 2, 'T': 3, 'X': 4}
    for match in comparison.get('matches', []):
        candidates = match.get('candidates', [])
        target = final.get(match['target'])
        if len(candidates) != 1 or not target or target.get('added') or target['kind'] not in names:
            continue
        node = nodes[candidates[0]]
        on = {int(p['edge'][1:]) for p in node.get('on', []) if p['edge'].startswith('p')}
        # A two-run patch must cover the entire local proposal's source support.
        # Three-run T neighborhoods need a larger model; never label a fragment T.
        if len(on) == 2 and all(0 <= i < len(spans) and i not in degenerate for i in on):
            labels[tuple(sorted(on))].add(names[target['kind']])
    bounds = []
    for span in spans:
        points = span['controls']
        bounds.append((min(p[0] for p in points), min(p[1] for p in points), max(p[0] for p in points), max(p[1] for p in points)))
    possible = []
    for a, b in itertools.combinations(range(len(spans)), 2):
        if a in degenerate or b in degenerate:
            continue
        x, y = bounds[a], bounds[b]
        gap = math.hypot(max(0, x[0]-y[2], y[0]-x[2]), max(0, x[1]-y[3], y[1]-x[3]))
        scale = max(spans[a].get('pathLength', 1), spans[b].get('pathLength', 1), 1)
        if (a, b) in labels or gap <= .16 * scale:
            possible.append((0 if len(labels.get((a, b), ())) == 1 else 1, gap/scale,
                             hashlib.sha256(f'{group}:{a}:{b}'.encode()).digest(), a, b))
    patches = []
    for _, _, _, a, b in sorted(possible)[:max_patches]:
        choices = labels.get((a, b), set())
        label = next(iter(choices)) if len(choices) == 1 else None
        patches.append({'id': f'{group[:16]}-{a}-{b}', 'profiles': [a, b], 'group': group,
                        'label': label, 'labelBasis': 'weak-authored-final-correspondence' if label is not None else 'unlabelled-source-patch'})
    if not patches:
        return None
    # The native source annotation importer does not need any church synthesis.
    minimal = {key: packet[key] for key in ('sourceOriginalJson', 'source', 'observation', 'program', 'annotations')}
    return {'sourceKey': packet['source']['key_id'], 'packet': minimal, 'patches': patches, 'unresolvedProfiles': sorted(degenerate)}


def _curve(a, b, bend, hook, rng):
    points = []
    dx, dy = b[0]-a[0], b[1]-a[1]
    for i in range(21):
        t = i/20
        wave = bend*math.sin(math.pi*t)
        points.append([a[0]+dx*t-dy*wave, a[1]+dy*t+dx*wave])
    if hook:
        # A short acquired tail; retained rather than destructively trimmed.
        end = points[-1]
        points.append([end[0]-.025*dy, end[1]+.025*dx])
    return points


def synthetic_requests(count=2500, seed=31, batch_size=100):
    if not 100 <= count <= 20000:
        raise ValueError('Choose 100..20000 controlled patches')
    batch = []
    for index in range(count):
        label = index % len(NAMES)
        family = index // (len(NAMES)*4)
        # Four distortions share one template and always stay in one split.
        group = f'synthetic:{seed}:{label}:{family}'
        base = random.Random(hashlib.sha256(group.encode()).digest())
        rng = random.Random(f'{seed}:{index}')
        reach = base.uniform(.65, 1.35)
        gap = rng.uniform(0, .055)
        angle = base.uniform(55, 125)*math.pi/180
        vector = (math.cos(angle), math.sin(angle))
        if label == 0:
            if family % 2:
                separation = base.uniform(.22, .8)
                pairs = [((-reach, 0), (reach, 0)), ((0, separation), (vector[0], separation+vector[1]))]
            else:
                separation = base.uniform(.10, .55)
                pairs = [((-reach, 0), (reach, 0)), ((-reach, separation), (reach, separation))]
        elif label == 1:
            pairs = [((-reach, 0), (0, 0)), ((gap, 0), (reach, base.uniform(-.05, .05)))]
        elif label == 2:
            pairs = [((-reach, 0), (0, 0)), ((gap*vector[0], gap*vector[1]), (reach*vector[0], reach*vector[1]))]
        elif label == 3:
            overrun = rng.uniform(-.055, .055)
            pairs = [((-reach, 0), (base.uniform(.6, 1.4), 0)), ((-reach*vector[0], -reach*vector[1]), (overrun*vector[0], overrun*vector[1]))]
        else:
            lower = base.uniform(.25, 1.4)
            pairs = [((-reach, 0), (base.uniform(.4, 1.3), 0)), ((-reach*vector[0], -reach*vector[1]), (lower*vector[0], lower*vector[1]))]
        runs = [_curve(a, b, rng.uniform(-.025, .025), rng.random() < .2, rng) for a, b in pairs]
        if label == 1 and family % 3 == 0:
            # Smooth circular continuation despite large total turning.
            extent = base.uniform(.4, 1.15)
            runs = [[[math.sin(t), 1-math.cos(t)] for t in [extent*(i/20-1) for i in range(21)]],
                    [[math.sin(t), 1-math.cos(t)] for t in [extent*i/20 for i in range(21)]]]
        rotation = rng.uniform(-math.pi, math.pi)
        scale = rng.uniform(30, 180)
        offset = (rng.uniform(-200, 500), rng.uniform(-200, 500))
        c, s = math.cos(rotation), math.sin(rotation)
        runs = [[[offset[0]+scale*(c*x-s*y), offset[1]+scale*(s*x+c*y)] for x, y in run] for run in runs]
        if rng.random() < .5:
            runs.reverse()
        for run in runs:
            if rng.random() < .5:
                run.reverse()
        batch.append({'id': f'synthetic-{index}', 'runs': runs, 'label': label, 'labelBasis': 'controlled-construction', 'group': group})
        if len(batch) == batch_size:
            yield {'sourceKey': 'controlled', 'patches': batch}
            batch = []
    if batch:
        yield {'sourceKey': 'controlled', 'patches': batch}
