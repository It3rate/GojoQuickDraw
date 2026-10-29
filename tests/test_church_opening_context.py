import copy
import unittest
from unittest.mock import patch

from gojo_quickdraw.church_opening_context import revise
from gojo_quickdraw.church_heuristics import compile_plan
from gojo_quickdraw.church_connection_plan import extend
from gojo_quickdraw.layout import estimate
from test_layout import church


def fixture(mixed=False):
    bounds=[[210,330,290,450],[90,260,150,310],[350,260,410,310],
            [90,350,150,420],[350,350,410,420]]
    observed=dict(strokes=[],spans=[])
    regions=[];reads={};parts=[dict(id='body',role='building',shape='rectangle',bounds=[50,230,450,450])]
    for i,b in enumerate(bounds):
        sid=str(i);pts=[[b[0],b[1]],[b[2],b[3]]]
        observed['spans'].append(dict(id=sid,strokeIndex=i,controls=pts))
        observed['strokes'].append(dict(index=i,points=pts,spans=[sid]))
        if i:regions.append(dict(bounds=b,spans=[sid]))
        shape='rectangle' if i<3 else 'arch'
        costs=dict(rectangle=.01,arch=.1,ellipse=.2) if i<3 else dict(rectangle=.1,arch=.01,ellipse=.2)
        if mixed and i==2:shape='ellipse';costs=dict(rectangle=.2,arch=.2,ellipse=.01)
        reads[sid]=dict(shape=shape,archFraction=.4,shapeCosts=costs,openingSupport=i!=3,
            shapeReads=dict(boundaryCost=.01,boundaryCoverage=.6))
        if i==3:continue
        # The earlier door envelope has reached the floor; its acquired ink has not.
        pb=b if i!=4 else [350,350,410,450]
        parts.append(dict(id='p'+sid,role='door' if i in (0,4) else 'window',bounds=pb,
                          spans=[sid],**reads[sid],geometry=dict(points=pts,segments=[[0,1]])))
    return observed,dict(parts=parts),regions,reads


class ChurchOpeningContextTests(unittest.TestCase):
    def revised(self,mixed=False):
        observed,layout,regions,reads=fixture(mixed);before=copy.deepcopy((observed,layout))
        with patch('gojo_quickdraw.church_opening_context._opening_regions',return_value=regions), \
             patch('gojo_quickdraw.church_opening_context.classify',side_effect=lambda o,r:reads[r['spans'][0]]):
            result,context=revise(observed,layout)
        self.assertEqual(before,(observed,layout))
        return result,context

    def test_multiple_rows_include_partial_mate_and_reconsider_floating_door(self):
        result,context=self.revised()
        self.assertEqual(4,sum(p['role']=='window' for p in result['parts']))
        self.assertEqual(1,sum(p['role']=='door' for p in result['parts']))
        pairs=context['pairs'];self.assertEqual(2,len(pairs))
        lower=pairs[1];self.assertEqual('p4',lower['leader'])
        by_id={p['id']:p for p in result['parts']}
        self.assertEqual(['arch','arch'],[by_id[lower[k]]['shape'] for k in ('left','right')])
        self.assertEqual('door',by_id['p4']['earlierHypothesis']['role'])
        self.assertEqual(450,by_id['p4']['earlierHypothesis']['bounds'][3])
        self.assertEqual(420,by_id['p4']['bounds'][3])

    def test_strong_evidence_for_distinct_families_can_decline_common_family(self):
        result,context=self.revised(mixed=True)
        top=context['pairs'][0];self.assertFalse(top['commonFamily'])
        by_id={p['id']:p for p in result['parts']}
        self.assertNotEqual(by_id[top['left']]['shape'],by_id[top['right']]['shape'])

    def test_straight_corner_can_outweigh_a_small_ellipse_fit_advantage_for_a_pair(self):
        observed,layout,regions,reads=fixture()
        reads['1']['shapeCosts']=dict(rectangle=.04,arch=.15,ellipse=.07)
        reads['2']['shapeCosts']=dict(rectangle=.10,arch=.15,ellipse=.04)
        with patch('gojo_quickdraw.church_opening_context._opening_regions',return_value=regions[:2]), \
             patch('gojo_quickdraw.church_opening_context.classify',side_effect=lambda o,r:reads[r['spans'][0]]), \
             patch('gojo_quickdraw.church_opening_context._corners',side_effect=lambda o,s:1 if s==['1'] else 0):
            result,context=revise(observed,layout)
        pair=context['pairs'][0];by_id={p['id']:p for p in result['parts']}
        self.assertEqual(['rectangle','rectangle'],[by_id[pair[k]]['shape'] for k in ('left','right')])
        self.assertIn('ellipse',{c['shape'] for c in pair['alternatives']})

    def test_partial_outline_without_a_supported_mate_is_not_promoted(self):
        observed,layout,regions,reads=fixture()
        with patch('gojo_quickdraw.church_opening_context._opening_regions',return_value=regions[2:3]), \
             patch('gojo_quickdraw.church_opening_context.classify',side_effect=lambda o,r:reads[r['spans'][0]]):
            result,context=revise(observed,layout)
        self.assertEqual(layout,result);self.assertFalse(context['pairs'])

    def test_connections_are_opt_in_and_reference_real_construction_roles(self):
        observed=church();initial=estimate(observed)
        ordinary=compile_plan(observed,initial)
        enhanced=compile_plan(observed,initial,connections=True)
        self.assertNotIn('connections',ordinary[2]);self.assertIn('connections',enhanced[2])
        self.assertEqual(ordinary,compile_plan(observed,initial))
        ids={p['id'] for p in enhanced[0]['parts']}
        for recipe in enhanced[2]['connections']['recipes']:
            for binding in recipe['bindings']:
                self.assertIn(binding['target']['part'],ids)
                self.assertTrue(all(t['part'] in ids for t in binding['terms']))

    def test_upper_tower_can_attach_to_wall_without_requiring_grounded_tower_to_do_so(self):
        parts=[dict(id='body',role='building',shape='rectangle',parent=None),
               dict(id='upper',role='tower',shape='rectangle',parent='body'),
               dict(id='grounded',role='tower',shape='rectangle',parent='body')]
        proposed={'body':dict(bounds=[10,200,400,450]),'upper':dict(bounds=[120,80,200,200]),
                  'grounded':dict(bounds=[300,100,380,450])}
        plan=extend(parts,proposed,{},dict(view=dict(selected='front-like'),symmetries=[]),[],{})
        recipes={r['id']:r for r in plan['recipes']}
        self.assertIn('upper-base-contact',recipes)
        self.assertNotIn('grounded-base-contact',recipes)
        self.assertEqual(dict(part='body',coordinate=1,coefficient=1),recipes['upper-base-contact']['bindings'][0]['terms'][0])


if __name__=='__main__':unittest.main()
