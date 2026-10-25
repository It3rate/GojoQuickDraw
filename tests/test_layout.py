import copy
import unittest

from gojo_quickdraw.layout import estimate, enrich_context


def church(openings=True, cross=True):
    lines = [([[50, 230], [55, 470]], 'wall'), ([[450, 225], [446, 474]], 'wall'),
             ([[52, 234], [245, 110], [452, 228], [52, 234]], 'roof'),
             ([[52, 473], [449, 471]], 'ground')]
    if openings:
        lines += [([[215, 471], [212, 355], [287, 358], [290, 473]], 'door'),
                  ([[100, 280], [140, 283], [141, 325], [98, 323], [100, 280]], 'window'),
                  ([[360, 280], [400, 281], [399, 322], [360, 324], [360, 280]], 'window')]
    if cross:
        lines += [([[245, 45], [247, 108]], 'cross'), ([[225, 65], [267, 65]], 'cross')]
    observed = {'sourceId': 'layout-fixture', 'strokes': [], 'spans': [], 'crosses': []}
    cross_ids = []
    for i, (points, role) in enumerate(lines):
        ids = []
        for j, (a, b) in enumerate(zip(points, points[1:])):
            sid = f'stroke-{i}-span-{j}'; ids.append(sid)
            observed['spans'].append({'id': sid, 'strokeIndex': i, 'controls': [a, b]})
        observed['strokes'].append({'id': f'stroke-{i}', 'index': i, 'points': points, 'spans': ids})
        if role == 'cross': cross_ids.extend(ids)
    if cross: observed['crosses'] = [{'point': [246, 65], 'spans': cross_ids}]
    return observed


class LayoutTests(unittest.TestCase):
    def test_regions_retain_source_and_share_only_proposed_contact_sites(self):
        observed = church(); before = copy.deepcopy(observed)
        context, result = enrich_context(observed, {'parts': [], 'relations': []})
        self.assertEqual(before, observed)
        parts = {p['id']: p for p in result['parts']}
        self.assertEqual([1, 1, 1, 1, 2], [sum(p['role'] == role for p in parts.values())
                         for role in ('building', 'roof', 'cross', 'door', 'window')])
        building = parts['layout-building']; roof = parts['layout-roof']
        self.assertLess(building['bounds'][0], 90); self.assertGreater(building['bounds'][2], 420)
        self.assertGreater(roof['geometry']['points'][1][0], 200, 'Closed roof start is not its apex or axis')
        self.assertEqual(building['geometry']['points'][0], roof['geometry']['points'][0])
        known = {}
        for part in context['parts']:
            self.assertTrue(set(part.get('participants', [])) <= set(known))
            for point, source in zip(part['geometry']['points'], part['geometry'].get('siteSources', [])):
                if source:
                    self.assertIn(source['part'], part['participants'])
                    self.assertEqual(known[source['part']]['geometry']['points'][source['index']], point)
            known[part['id']] = part
        l, t, r, g = building['bounds']
        door = parts['layout-door-1']['bounds']
        self.assertEqual(g, door[3]); self.assertEqual(g, parts['layout-ground']['bounds'][1])
        for p in parts.values():
            if p['role'] not in ('door', 'window'): continue
            a, b, c, d = p['bounds']
            self.assertTrue(l <= a < c <= r and t < b < d <= g)
            if p['role'] == 'window':
                self.assertLess(d, g)
                self.assertLess(c-a, door[2]-door[0]); self.assertLess(d-b, door[3]-door[1])
        self.assertGreater(len(result['alternatives']), 1)
        self.assertFalse(result['policy']['learned'])

    def test_missing_openings_are_not_invented_and_missing_cross_is_explicit(self):
        result = estimate(church(openings=False, cross=False))
        self.assertFalse(any(p['role'] in ('door', 'window') for p in result['parts']))
        self.assertEqual('prior-only', next(p for p in result['parts'] if p['role'] == 'cross')['status'])
        self.assertFalse(next(p for p in result['parts'] if p['role'] == 'cross')['spans'])

    def test_cross_within_roof_has_ideal_outline_inside_triangle(self):
        observed = church(cross=False)
        observed['strokes'] += [{'id': 'inner-v', 'index': 10, 'points': [[245, 155], [245, 205]], 'spans': ['cv']},
                                {'id': 'inner-h', 'index': 11, 'points': [[226, 170], [264, 170]], 'spans': ['ch']}]
        observed['spans'] += [{'id': s['spans'][0], 'strokeIndex': s['index'], 'controls': s['points']} for s in observed['strokes'][-2:]]
        observed['crosses'] = [{'point': [245, 170], 'spans': ['cv', 'ch']}]
        parts = {p['role']: p for p in estimate(observed)['parts']}
        self.assertEqual('within-roof', parts['cross']['placement'])
        left, apex, right = parts['roof']['geometry']['points'][:3]
        for x, y in parts['cross']['geometry']['points']:
            fraction = (y-apex[1])/(left[1]-apex[1])
            self.assertTrue(0 <= fraction <= 1)
            self.assertGreaterEqual(x+.002, apex[0]+(left[0]-apex[0])*fraction)
            self.assertLessEqual(x-.002, apex[0]+(right[0]-apex[0])*fraction)

    def test_empty_or_one_dimensional_ink_leaves_layout_open(self):
        for observation in ({'strokes': [], 'spans': []}, {'strokes': [{'index': 0, 'points': [[5, 0], [5, 400]]}],
                                                        'spans': [{'id': 'one', 'controls': [[5, 0], [5, 400]]}]}):
            result = estimate(observation)
            self.assertFalse(result['parts']); self.assertTrue(result['status'].startswith('open-'))

    def test_enrichment_is_repeatable_and_does_not_mutate_earlier_context(self):
        observed = church(); initial = {'parts': [{'id': 'earlier', 'kind': 'unknown'}], 'relations': []}
        first, _ = enrich_context(observed, initial)
        second, _ = enrich_context(observed, first)
        self.assertEqual(first, second)
        self.assertEqual(1, len(initial['parts']))


if __name__ == '__main__': unittest.main()
