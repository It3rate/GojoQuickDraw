"""Small trained softmax ranker over published native band cells only.

Pure Python keeps the first CPU experiment independent of a GPU or ML runtime.
Names identify exported columns/candidates; the native constructions own topology.
"""
from collections import Counter
import copy
import math
import random

from .training_data import NAMES, split

MODES = ('topology', 'partitions', 'curves')


def tokens(bands, mode='curves'):
    if mode not in MODES or len(bands) != 16 or any(type(b) is not int or not 0 <= b <= 5 for b in bands):
        raise ValueError('Expected sixteen native band cells and a known feature mode')
    tails = sorted((min(bands[0:2]), min(bands[2:4])))
    reach = tuple(1 if value == 0 else 2 for value in tails)
    values = ['bias', f'arms:{reach}', f'angle:{bands[6]}']
    if mode != 'topology':
        # Ordered cuts remain in the archive. This type-ranking read deliberately
        # factors out reversal/order; source orientation hints remain separate.
        values += [f'tails:{tuple(tails)}', f'gap:{tuple(sorted(bands[4:6]))}', f'length:{bands[15]}']
        values += [f'angle-gap:{bands[6]}:{max(bands[4:6])}']
    if mode == 'curves':
        for name, pair in (('bend', (7, 8)), ('turn', (9, 10)), ('turn-locality', (11, 12)), ('hook', (13, 14))):
            values.append(f'{name}:{tuple(sorted(bands[i] for i in pair))}')
    return values


def probabilities(weights, indices, temperature=1):
    scores = [sum(row[i] for i in indices)/temperature for row in weights]
    highest = max(scores)
    values = [math.exp(score-highest) for score in scores]
    total = sum(values)
    return [value/total for value in values]


def encode(model, bands):
    columns = {name: i for i, name in enumerate(model['columns'])}
    values = tokens(bands, model['mode'])
    return [columns[value] for value in values if value in columns], sum(value not in columns for value in values)


def predict(model, bands):
    indices, unseen = encode(model, bands)
    scores = probabilities(model['weights'], indices, model.get('temperature', 1))
    order = sorted(range(len(scores)), key=lambda i: -scores[i])
    confident = scores[order[0]] >= .60 and scores[order[0]]-scores[order[1]] >= .15 and unseen <= 1
    return {'scores': scores, 'ranking': order, 'proposed': order[0] if confident else None,
            'unseenFeatures': unseen, 'status': 'proposed' if confident else 'unresolved'}


def metrics(model, examples):
    labelled = [row for row in examples if row['label'] is not None]
    confusion = [[0]*len(NAMES) for _ in NAMES]
    loss = correct = accepted = accepted_correct = 0
    for row in labelled:
        read = predict(model, row['bands'])
        target = row['label']; guess = read['ranking'][0]
        confusion[target][guess] += 1
        correct += target == guess
        loss -= math.log(max(1e-12, read['scores'][target]))
        if read['proposed'] is not None:
            accepted += 1; accepted_correct += target == guess
    n = len(labelled)
    return {'examples': n, 'top1Agreement': correct/n if n else None, 'crossEntropy': loss/n if n else None,
            'proposalCoverage': accepted/n if n else None, 'agreementWhenProposed': accepted_correct/accepted if accepted else None,
            'confusion': confusion, 'candidateOrder': list(NAMES),
            'targets': dict(Counter(NAMES[row['label']] for row in labelled))}


def fit(examples, mode='curves', epochs=35, seed=31, weak_weight=.25, log=lambda value: None):
    if not 1 <= epochs <= 200:
        raise ValueError('Choose 1..200 training epochs')
    train = [row for row in examples if row['label'] is not None and split(row['group'], seed) == 'train']
    valid = [row for row in examples if row['label'] is not None and split(row['group'], seed) == 'validation']
    if not train or not valid:
        raise ValueError('Training and validation groups are both required')
    columns = sorted({token for row in train for token in tokens(row['bands'], mode)})
    index = {token: i for i, token in enumerate(columns)}
    encoded = [([index[token] for token in tokens(row['bands'], mode)], row['label'],
                weak_weight if row['labelBasis'].startswith('weak-') else 1) for row in train]
    model = {'schema': 'gojo.local-joint-model.v1', 'mode': mode, 'columns': columns,
             'weights': [[0.0]*len(columns) for _ in NAMES], 'temperature': 1,
             'candidateOrder': list(NAMES), 'seed': seed, 'selectionBasis': 'minimum validation cross entropy',
             'qualification': 'External learned ranker over native finite reads; scores are not certified probabilities or source contacts.'}
    weights = model['weights']; rng = random.Random(seed); best = math.inf; best_weights = None; curve = []
    for epoch in range(epochs):
        rng.shuffle(encoded); rate = .12/math.sqrt(1+epoch*.15); running = 0
        for row in weights:
            for i in range(len(row)):
                row[i] *= .999
        for active, target, strength in encoded:
            scores = probabilities(weights, active)
            running -= math.log(max(1e-12, scores[target]))
            for candidate in range(len(NAMES)):
                delta = rate*strength*((1 if candidate == target else 0)-scores[candidate])
                row = weights[candidate]
                for i in active:
                    row[i] += delta
        current = metrics(model, valid)['crossEntropy']
        curve.append({'epoch': epoch+1, 'trainingCrossEntropy': running/len(encoded), 'validationCrossEntropy': current})
        if current < best:
            best = current; best_weights = copy.deepcopy(weights); model['selectedEpoch'] = epoch+1
        if epoch == 0 or (epoch+1) % 10 == 0 or epoch+1 == epochs:
            log(f'{mode}: epoch {epoch+1}/{epochs}, validation loss {current:.3f}')
    model['weights'] = best_weights
    model['learningCurve'] = curve
    # Temperature is selected on validation only; real-data calibration remains
    # conditional on weak authored targets, not human interpretations.
    best_temperature = min((.5, .75, 1, 1.5, 2, 3), key=lambda t: _temperature_loss(model, valid, t))
    model['temperature'] = best_temperature
    return model


def _temperature_loss(model, rows, temperature):
    loss = 0
    for row in rows:
        active, _ = encode(model, row['bands'])
        loss -= math.log(max(1e-12, probabilities(model['weights'], active, temperature)[row['label']]))
    return loss/max(1, len(rows))
