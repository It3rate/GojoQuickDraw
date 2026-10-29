import copy
import unittest

from gojo_quickdraw.layout import estimate, _rectangle
from gojo_quickdraw.layout_mapping import point
from gojo_quickdraw.layout_recovery import recover, _crossings, _pointed, _residue
from gojo_quickdraw.church_heuristics import compile_plan
from test_layout_roofs import observation


def drawing(tower=True):
    paths=[]
    if tower:
        paths += [[[265,120],[225,40],[180,145],[155,270],[110,430]],
                  [[262,130],[260,275],[230,430],[111,430]]]
    paths += [[[0,230],[0,430],[430,430],[430,230]],[[0,230],[430,230]],
              [[220,285],[220,390]]]
    observed=observation(paths)
    if tower:
        # The problematic read is one curved cap-and-wall interval, without
        # a fitted corner at the intended eave. A pre-segmented roof is easier.
        observed['spans']=[s for s in observed['spans'] if s['id'] not in ('0:1','0:2')]
        observed['spans'].insert(1,dict(id='left-curve',strokeIndex=0,startIndex=1,endIndex=3,
            controls=[[225,40],[168,132],[155,270]],kind='quadratic'))
        observed['strokes'][0]['spans']=['0:0','left-curve','0:3']
    controls=[[250,335],[215,319],[175,355]]
    points=[point(controls,i/16) for i in range(17)];index=len(paths)
    observed['strokes'].append(dict(index=index,points=points,spans=['bar']))
    observed['spans'].append(dict(id='bar',strokeIndex=index,controls=controls,startIndex=0,endIndex=16,kind='quadratic'))
    return observed


class RecoveryTests(unittest.TestCase):
    def test_continuous_pointed_outline_recovers_front_tower_and_preserves_first_read(self):
        observed=drawing();original=copy.deepcopy(observed);first=estimate(observed,recover_missing=False)
        result=recover(observed,first)
        tower=next(p for p in result['parts'] if p['id'].startswith('layout-recovered-tower'))
        roof=next(p for p in result['parts'] if p['id'].startswith('layout-recovered-roof'))
        self.assertEqual([tower['id']],roof['participants'])
        self.assertEqual(tower['bounds'][1],roof['bounds'][3]);self.assertEqual(original,observed)
        self.assertEqual(first['parts'],result['recovery']['initialParts'])
        self.assertTrue(result['recovery']['decisions'][0]['sourceIntervals'])
        self.assertTrue(result['recovery']['decisions'][0]['penUpGaps'])
        self.assertLess(result['recovery']['finalResidualLength'],result['recovery']['initialResidualLength'])

    def test_lower_curved_cross_replaces_a_prior_and_uses_the_recovered_container(self):
        observed=drawing();first=estimate(observed,recover_missing=False);result=recover(observed,first)
        cross=next(p for p in result['parts'] if p['id'].startswith('layout-recovered-cross'))
        self.assertEqual(2,len(cross['spans']));self.assertNotEqual('prior-only',cross['status'])
        self.assertTrue(cross['participants'][0].startswith('layout-recovered-tower'))
        self.assertGreater(cross['geometry']['points'][12][1],230)
        self.assertFalse(any(p['role']=='cross' and p['status']=='prior-only' for p in result['parts']))
        self.assertTrue(any(p['role']=='cross' and p['status']=='prior-only' for p in result['recovery']['initialParts']))

    def test_existing_source_cross_prevents_another_residual_cross_search(self):
        observed=drawing(False);first=estimate(observed,recover_missing=False)
        cross=next(p for p in first['parts'] if p['role']=='cross')
        cross.update(status='regional-estimate',spans=['bar'])
        result=recover(observed,first)
        self.assertEqual('already-source-supported',result['recovery']['crossSearch'])
        self.assertFalse(any(d['kind']=='crossing' for d in result['recovery']['decisions']))

    def test_window_dividers_do_not_become_a_church_cross(self):
        observed=drawing(False);first=estimate(observed,recover_missing=False)
        ids=[s['id'] for s in observed['spans'] if s['strokeIndex']>=2]
        geometry,_=_rectangle([160,275,280,400])
        first['parts'].append(dict(id='window',role='window',bounds=[160,275,280,400],geometry=geometry,
            participants=['layout-building'],spans=ids,status='regional-estimate'))
        result=recover(observed,first)
        self.assertFalse(any(d['kind']=='crossing' for d in result['recovery']['decisions']))

    def test_tiny_overrun_is_not_a_four_armed_symbol(self):
        observed=observation([[[0,200],[0,430],[430,430],[430,200]],[[0,200],[430,200]],
                              [[220,280],[220,390]],[[170,387],[270,387]]])
        first=estimate(observed,recover_missing=False);lengths,intervals=_residue(observed,first)
        candidates,_=_crossings(observed,first,lengths,intervals,[0,200,430,430])
        self.assertFalse(candidates)

    def test_explained_ink_does_not_supply_new_candidates(self):
        observed=drawing();first=estimate(observed,recover_missing=False)
        zeros={s['id']:0 for s in observed['spans']};ranges={s['id']:[] for s in observed['spans']}
        self.assertFalse(_pointed(observed,first,zeros,ranges,[0,40,430,430]))
        self.assertFalse(_crossings(observed,first,zeros,ranges,[0,40,430,430])[0])

    def test_recovery_is_idempotent_and_shared_source_ids_remain_unique_per_part(self):
        observed=drawing();first=estimate(observed,recover_missing=False);result=recover(observed,first)
        self.assertEqual(result,recover(observed,result))
        valid={s['id'] for s in observed['spans']}
        for p in result['parts']:
            self.assertEqual(len(p['spans']),len(set(p['spans'])));self.assertTrue(set(p['spans'])<=valid)

    def test_default_layout_and_opt_in_cached_study_both_use_the_same_recovery(self):
        observed=drawing();first=estimate(observed,recover_missing=False);result=estimate(observed)
        self.assertTrue(result['recovery']['decisions'])
        scaffold,_,plan,_=compile_plan(observed,first,True,True,True)
        self.assertIn('recovery',plan)
        self.assertTrue(any(p['id'].startswith('layout-recovered-tower') for p in scaffold['parts']))
        self.assertNotIn('recovery',compile_plan(observed,first,True,True,False)[2])


if __name__=='__main__':unittest.main()
