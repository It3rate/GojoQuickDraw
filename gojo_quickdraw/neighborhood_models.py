"""Two small candidate/line providers over finite native neighborhood reads."""
import copy
import math
import random

from .training_data import split


def tokens(bands):
    if not bands or any(type(v) is not int or not 0 <= v <= 5 for v in bands):
        raise ValueError('Expected finite native neighborhood cells')
    return ['bias', *[f'{i}:{b}' for i, b in enumerate(bands)],
            f'pair:0:{bands[0]}:3:{bands[min(3,len(bands)-1)]}',
            f'pair:1:{bands[1]}:5:{bands[min(5,len(bands)-1)]}']


def predict(model, bands):
    index = {token: i for i, token in enumerate(model['columns'])}
    active = tokens(bands)
    score = sum(model['weights'][index[t]] for t in active if t in index)
    return 1/(1+math.exp(-max(-40,min(40,score)))), sum(t not in index for t in active)


def metrics(model, rows, threshold=.68):
    correct = accepted = useful = 0
    loss = 0
    for row in rows:
        p, unknown = predict(model, row['bands']); target = row['label']
        correct += (p >= .5) == target
        loss -= math.log(max(1e-10,p if target else 1-p))
        if p >= threshold and unknown <= 3:
            accepted += 1; useful += target
    n = len(rows)
    return {'examples': n, 'agreement': correct/n if n else None, 'crossEntropy': loss/n if n else None,
            'proposalCoverage': accepted/n if n else None, 'precisionWhenProposed': useful/accepted if accepted else None}


def fit(rows, feature_names, *, epochs=35, seed=43):
    train = [r for r in rows if split(r['group'],seed) == 'train']
    valid = [r for r in rows if split(r['group'],seed) == 'validation']
    if not train or not valid or len({r['label'] for r in train}) != 2:
        raise ValueError('Both classes and independent training/validation groups are needed')
    columns = sorted({t for row in train for t in tokens(row['bands'])})
    index = {t:i for i,t in enumerate(columns)}
    model = {'schema':'gojo.neighborhood-provider.v1','columns':columns,'weights':[0.0]*len(columns),
             'featureNames':feature_names,'featureCount':len(feature_names),'seed':seed}
    positives = sum(r['label'] for r in train)
    balance = min(3,max(.5,(len(train)-positives)/max(1,positives)))
    encoded = [([index[t] for t in tokens(r['bands'])],r['label'],(.15 if r['basis'].startswith('weak') else 1)*(balance if r['label'] else 1)) for r in train]
    rng = random.Random(seed); best = float('inf'); selected = None; curve = []
    for epoch in range(epochs):
        rng.shuffle(encoded); rate = .08/math.sqrt(1+epoch*.2)
        model['weights'] = [v*.998 for v in model['weights']]
        for active,target,strength in encoded:
            value = sum(model['weights'][i] for i in active)
            p = 1/(1+math.exp(-max(-40,min(40,value))))
            delta = rate*strength*(target-p)
            for i in active:
                model['weights'][i] += delta
        loss = metrics(model,valid)['crossEntropy']; curve.append({'epoch':epoch+1,'validationLoss':loss})
        if loss < best:
            best=loss;selected=copy.deepcopy(model['weights']);model['selectedEpoch']=epoch+1
    model['weights']=selected;model['learningCurve']=curve
    return model
