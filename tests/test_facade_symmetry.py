import copy
import unittest

from gojo_quickdraw.cleanup import build
from gojo_quickdraw.cleanup_checks import mirrors
from gojo_quickdraw.cleanup_openings import separate
from gojo_quickdraw.cleanup_symmetry import pair_windows
from gojo_quickdraw.layout import estimate, _ring
from gojo_quickdraw.layout_markers import crossing_candidates, markers
from gojo_quickdraw.layout_openings import classify
from test_layout import church
from test_layout_roofs import observation


class FacadeSymmetryTests(unittest.TestCase):
    def test_tilted_enclosure_reads_a_local_frame_without_mutating_ink(self):
        observed=observation([[[80,215],[145,195],[165,255],[100,275],[80,215]]])
        before=copy.deepcopy(observed)
        result=classify(observed,{'bounds':[80,195,165,275],'spans':[s['id'] for s in observed['spans']]})
        self.assertTrue(result['openingSupport']);self.assertTrue(result['closedTrace'])
        self.assertEqual('rectangle',result['shape']);self.assertIn('localRotationDegrees',result['shapeReads'])
        self.assertEqual(before,observed)

    def test_upper_tower_is_fully_supported_and_cap_follows_actual_shared_sites(self):
        obs=church(openings=False);layout=estimate(obs);body=next(p for p in layout['parts'] if p['role']=='building')
        l,t,r,g=body['bounds'];bounds=[l-60,t-90,l+80,t]
        tower=dict(id='layout-tower-1',role='tower',bounds=bounds,participants=[body['id']],spans=[],status='regional-estimate',
            geometry=_ring([[bounds[0],bounds[1]],[bounds[2],bounds[1]],[bounds[2],t],[bounds[0],t]]))
        layout['parts'].insert(2,tower)
        roof=next(p for p in layout['parts'] if p['role']=='roof');roof['participants']=[tower['id']]
        roof['bounds']=[bounds[0],t-140,bounds[2],t-90]
        before=copy.deepcopy(layout);final=build(obs,layout);parts={p['id']:p for p in final['parts']}
        result=parts['cleanup-tower-1'];cap=parts['cleanup-roof']
        self.assertEqual(140,result['bounds'][2]-result['bounds'][0])
        self.assertGreaterEqual(result['bounds'][0],l);self.assertLessEqual(result['bounds'][2],r)
        self.assertEqual(result['bounds'][::2],cap['bounds'][::2]);self.assertEqual(before,layout)
        for p in (result,cap):
            references=[ref for ref in p['geometry']['siteSources'] if ref]
            self.assertGreaterEqual(len(references),2)
        self.assertTrue(any(r['kind']=='shape-refinement-supported-footprint' for r in final['relations']))

    def test_four_supported_windows_keep_count_and_form_two_local_pairs(self):
        owner=dict(id='wall',role='building',bounds=[0,0,400,300])
        specs=[owner,dict(id='door',role='door',parent='wall',bounds=[180,220,220,300])]
        for i,(x,y,w,h) in enumerate([(50,50,45,50),(300,70,65,60),(70,140,50,45),(295,150,55,45)]):
            specs.append(dict(id=str(i),role='window',parent='wall',bounds=[x,y,x+w,y+h],shape='rectangle',changes=[],status='refined-estimate'))
        result={'symmetries':[],'relations':[]};pair_windows(specs,{},result)
        self.assertEqual(2,len(result['symmetries']));self.assertEqual(4,sum(p['role']=='window' for p in specs))
        self.assertFalse(any(c['status']=='needs-review' for c in mirrors(specs,result['symmetries'])))
        self.assertEqual(4,len({p['id'] for p in specs if p['role']=='window'}))

    def test_round_and_rectangular_openings_do_not_become_one_family(self):
        specs=[dict(id='wall',role='building',bounds=[0,0,400,300])]
        for i,shape in enumerate(['circle','rectangle']):
            specs.append(dict(id=str(i),role='window',parent='wall',bounds=[40+240*i,60,110+240*i,130],shape=shape,changes=[]))
        result={'symmetries':[]};before=copy.deepcopy(specs);pair_windows(specs,{},result)
        self.assertFalse(result['symmetries']);self.assertEqual(before,specs)

    def test_circle_stays_round_when_wall_margin_requires_a_smaller_window(self):
        def part(ident,role,bounds,shape='rectangle'):
            a,t,b,g=bounds
            return dict(id=ident,role=role,bounds=bounds,shape=shape,spans=[],participants=[],status='regional-estimate',
                geometry=_ring([[a,t],[b,t],[b,g],[a,g]]))
        layout={'parts':[part('layout-ground','ground',[0,400,120,400],'level'),
            part('layout-building','building',[0,0,120,400]),
            part('layout-window-1','window',[5,100,115,210],'circle')]}
        final=build(observation([]),layout);window=next(p for p in final['parts'] if p['role']=='window')
        a,t,b,g=window['bounds']
        self.assertAlmostEqual(b-a,g-t,places=3);self.assertAlmostEqual(100.8,b-a,places=3)

    def test_pair_can_shrink_together_before_clearance_breaks_its_mirror(self):
        specs=[dict(id='wall',role='building',bounds=[65,230,431,473]),
               dict(id='door',role='door',parent='wall',bounds=[209,356,297,473],status='refined-estimate',changes=[])]
        for ident,x in [('a',135),('b',371)]:
            specs.append(dict(id=ident,role='window',parent='wall',bounds=[x-48,268,x+48,367],status='refined-estimate',changes=[]))
        result={'relations':[],'symmetries':[dict(left='a',right='b',axisPart='door')]}
        checks=separate(specs,result)
        self.assertFalse(any(c['status']=='needs-review' for c in checks))
        self.assertFalse(any(c['status']=='needs-review' for c in mirrors(specs,result['symmetries'])))
        self.assertLess(specs[2]['bounds'][2]-specs[2]['bounds'][0],96)

    def test_short_overrun_does_not_duplicate_a_cross_but_raw_joints_survive(self):
        obs=observation([[[200,20],[200,120]],[[170,55],[230,55]],[[195,110],[205,110]]])
        obs['crosses']=[dict(point=[200,55],spans=['0:0','1:0'],segmentFractions=[.35,.5]),
                        dict(point=[200,110],spans=['0:0','2:0'],segmentFractions=[.9,.5])]
        before=copy.deepcopy(obs);accepted,rejected=crossing_candidates(obs,[0,0,400,400])
        self.assertEqual(1,len(accepted));self.assertEqual(1,len(rejected));self.assertEqual(before,obs)
        # Two complete symbols sharing a stem remain two candidates.
        obs['crosses'][1]['segmentFractions']=[.6,.5]
        accepted,rejected=crossing_candidates(obs,[0,0,400,400])
        self.assertEqual(2,len(accepted));self.assertFalse(rejected)

    def test_face_cross_chooses_its_local_tower_not_the_building_roof(self):
        obs=observation([[[115,170],[115,240]],[[90,192],[140,192]]])
        obs['crosses']=[dict(point=[115,192],spans=['0:0','1:0'])]
        parents=[dict(id='cap',role='roof',bounds=[50,70,180,140],apex=[115,70]),
                 dict(id='tower',role='tower',bounds=[50,140,180,250],apex=[115,140])]
        selected,_=markers(obs,parents,[50,250,450,450],[0,0,500,500],outlines=[])
        self.assertEqual(1,selected[0]['roofIndex'])


if __name__=='__main__':unittest.main()
