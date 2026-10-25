import copy
import math
import unittest

from gojo_quickdraw.layout import _rectangle, _opening_regions
from gojo_quickdraw.layout_mapping import build, groups
from gojo_quickdraw.layout_openings import classify, outline
from test_layout_roofs import observation


def part(ident, role, bounds, spans=(), **extra):
    return dict(id=ident, role=role, bounds=bounds, geometry=_rectangle(bounds)[0],
                spans=list(spans), participants=[], status='regional-estimate', **extra)


class BoundaryMappingTests(unittest.TestCase):
    def test_shared_partial_ink_keeps_a_complete_nonduplicating_source_partition(self):
        o=observation([[[0,100],[400,100]],[[0,100],[0,400]],[[200,250],[220,265],[200,280]]])
        parts=[part('wall','building',[0,100,400,400]),part('tower','tower',[150,20,250,100])]
        before=copy.deepcopy(o); m=build(o,{'parts':parts})
        shared=[r for r in m['sourceCoverage'] if len(r['parts'])>1]
        self.assertTrue(shared); self.assertTrue(all(r['parts']==['tower','wall'] for r in shared))
        self.assertTrue(all(0<r['from']<r['to']<1 for r in shared))
        self.assertTrue(all(r['status']=='unresolved' for r in m['sourceCoverage'] if r['span'].startswith('2:')))
        for span in o['spans']:
            ranges=[r for r in m['sourceCoverage'] if r['span']==span['id']]
            self.assertEqual(0,ranges[0]['from']);self.assertEqual(1,ranges[-1]['to'])
            for a,b in zip(ranges,ranges[1:]):self.assertEqual(a['to'],b['from'])
            self.assertAlmostEqual(1,sum(r['to']-r['from'] for r in ranges))
        self.assertEqual(before,o)

    def test_ground_endpoints_and_prior_cross_never_claim_nearby_ink(self):
        o=observation([[[0,100],[0,400]],[[200,100],[200,400]],[[95,10],[95,80]]])
        ground=part('ground','ground',[0,400,200,400]);ground['geometry']={'points':[[0,400],[200,400]],'segments':[[0,1]]}
        cross=part('cross','cross',[90,10,100,80]);cross['status']='prior-only';cross['spans']=['2:0']
        m=build(o,{'parts':[ground,cross]})
        self.assertFalse(any(t['ink'] for t in m['targets']))
        self.assertEqual(2,len(m['targets'][0]['endpointSupport']))
        self.assertTrue(all(t['status']=='expectation-prior' for t in m['targets'][1:]))
        self.assertTrue(all(r['status']=='unresolved' for r in m['sourceCoverage']))

    def test_reverse_traversal_is_preserved_as_opposite_target_order(self):
        o=observation([[[400,100],[0,100]]])
        m=build(o,{'parts':[part('wall','building',[0,100,400,400])]})
        match=next(v for t in m['targets'] for v in t['ink'])
        self.assertGreater(match['targetFrom'],match['targetTo'])
        self.assertLess(match['from'],match['to'])

    def test_unprofiled_dot_stays_available_as_unresolved_acquisition(self):
        o=observation([[[10,10]]]);m=build(o,{'parts':[]})
        self.assertEqual([{'stroke':0,'sample':0}],m['unprofiledSamples'])

    def test_split_scaffold_edges_are_one_ordered_boundary_group(self):
        p=part('wall','building',[0,100,400,400]);p['geometry']=_rectangle(p['bounds'],[150,200,250])[0]
        actual=groups(p)
        self.assertEqual(4,len(actual));self.assertEqual(4,len(actual[0]['segments']))
        self.assertEqual(list(range(len(p['geometry']['segments']))),sorted(i for g in actual for i in g['segments']))

    def test_curved_head_and_nearly_flat_head_are_distinguished(self):
        bounds=[100,100,200,300]
        for arch,expected in ((0,'rectangle'),(60,'arch')):
            o=observation([outline(bounds,arch)])
            result=classify(o,dict(bounds=bounds,spans=[s['id'] for s in o['spans']]))
            self.assertEqual(expected,result['shape'])

    def test_opening_side_can_join_interior_of_overhanging_beam(self):
        o=observation([[[100,230],[100,300]],[[85,230],[150,230],[150,290]]])
        candidates=_opening_regions(o,[0,100,300,300],set())
        self.assertTrue(any(set(c['spans'])=={'0:0','1:0','1:1'} for c in candidates))

    def test_small_retraced_mark_does_not_close_a_large_writing_region(self):
        o=observation([[[100,100],[100,150],[102,100]],[[105,140],[150,100],[200,150]]])
        r=classify(o,{'bounds':[100,100,200,150],'spans':[s['id'] for s in o['spans']]})
        self.assertFalse(r['closedTrace'])


if __name__=='__main__':unittest.main()
