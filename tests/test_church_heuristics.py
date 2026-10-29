import copy
import unittest
from gojo_quickdraw.church_heuristics import compile_plan, supplement
from gojo_quickdraw.layout import estimate
from test_layout import church
from test_layout_roofs import observation


class ChurchHeuristicTests(unittest.TestCase):
    def test_proposals_preserve_source_and_seed_observed_bounds_not_final_targets(self):
        observed=church(); initial=estimate(observed)
        roof=next(p for p in initial['parts'] if p['role']=='roof')
        roof['apex'][0]+=35;roof['geometry']['points'][1][0]+=35
        before=copy.deepcopy((observed,initial))
        scaffold,mapping,plan,reference=compile_plan(observed,initial)
        self.assertEqual(before,(observed,initial))
        originals={p['id']:p for p in initial['parts']}
        for p in scaffold['parts']:
            if p['id'] in originals:
                self.assertEqual(originals[p['id']]['bounds'],p['bounds'])
                self.assertEqual(originals[p['id']]['geometry'],p['sourceGeometry'])
        self.assertTrue(plan['rules']);self.assertTrue(mapping['sourceCoverage'])
        self.assertIn('attachment',{r['layer'] for r in plan['rules']})
        self.assertFalse(any('target' in r for r in plan['rules']))

    def test_uncertain_scribbles_do_not_automatically_become_windows(self):
        observed=church(openings=False);initial=estimate(observed)
        after,decisions=supplement(observed,initial)
        self.assertFalse([p for p in after['parts'] if p['id'].startswith('layout-residual-window')])

    def test_unused_closed_window_is_reconsidered_even_when_another_window_exists(self):
        observed=church();initial=estimate(observed)
        missing=next(p for p in initial['parts'] if p['role']=='window')
        initial['parts'].remove(missing)
        after,decisions=supplement(observed,initial)
        recovered=[p for p in after['parts'] if p['id'].startswith('layout-residual-window')]
        self.assertEqual(1,len(recovered))
        self.assertEqual(set(missing['spans']),set(recovered[0]['spans']))
        self.assertEqual('source-supported',recovered[0]['status'])
        self.assertEqual(2,sum(p['role']=='window' for p in after['parts']))

    def test_affine_coefficients_are_translation_invariant_and_reference_real_ports(self):
        observed=church();scaffold,_,plan,_=compile_plan(observed,estimate(observed))
        ids={p['id'] for p in scaffold['parts']}
        for rule in plan['rules']:
            self.assertAlmostEqual(0,sum(t['coefficient'] for t in rule['terms']))
            self.assertTrue(all(t['part'] in ids and 0<=t['coordinate']<=3 for t in rule['terms']))


if __name__=='__main__':unittest.main()
