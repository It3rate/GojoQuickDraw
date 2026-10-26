import copy
import unittest

from gojo_quickdraw.cleanup import build
from gojo_quickdraw.cleanup_checks import mirrors
from gojo_quickdraw.cleanup_crosses import placement
from gojo_quickdraw.cleanup_foreground import wall_in_front, ground_support
from gojo_quickdraw.cleanup_openings import gap, separate
from gojo_quickdraw.cleanup_display import svg
from gojo_quickdraw.layout import estimate, _ring, COLORS
from gojo_quickdraw.layout_markers import cross_geometry
from test_layout import church
from test_layout_roofs import observation


def marker(x,top,height,bar=None):
    return dict(id='cross',bounds=[x-.3*height,top,x+.3*height,top+height],status='regional-estimate',
                placement='above-roof',spans=['stem'],geometry=cross_geometry(x,top,height,bar=bar))


def support(shape='triangle'):
    return dict(id='roof',shape=shape,bounds=[50,120,350,240],apex=[310,120])


def openings(prior=True):
    specs=[dict(id='wall',role='building',bounds=[0,0,400,120]),
           dict(id='door',role='door',bounds=[124,45,276,120],parent='wall',status='refined-estimate',changes=[])]
    for ident,bounds in [('left',[76,30,124,54]),('right',[276,30,324,54])]:
        specs.append(dict(id=ident,role='window',bounds=bounds,parent='wall',
                          status='prior-only' if prior else 'refined-estimate',changes=[]))
    result={'relations':[],'symmetries':[dict(left='left',right='right',axisPart='wall')]}
    return specs,result


class CleanupDetailTests(unittest.TestCase):
    def test_off_center_peak_and_cross_move_together_without_resizing(self):
        old=support();new=dict(old,apex=[200,120]);cross=marker(310,53,60)
        before=copy.deepcopy((cross,old,new))
        bounds,options=placement(cross,new,old,300)
        self.assertEqual(200,(bounds[0]+bounds[2])/2)
        self.assertEqual(120,bounds[3]);self.assertEqual(60,bounds[3]-bounds[1])
        self.assertEqual('peak-attachment',options['supportRead']['basis'])
        self.assertEqual(before,(cross,old,new))

    def test_shortened_cross_envelope_uses_selected_upright_endpoint(self):
        old=dict(support('dome'),apex=[200,120]);cross=marker(200,20,60)
        obs=observation([[[200,120],[200,10]]]);cross['spans']=['0:0']
        bounds,options=placement(cross,old,old,300,obs)
        self.assertEqual(120,bounds[3]);self.assertEqual('attached-top',options['placement'])
        self.assertTrue(options['supportRead']['endpointReads'])
        # Unrelated nearby strokes cannot become attachment support.
        cross['spans']=[];bounds,options=placement(cross,old,old,300,obs)
        self.assertEqual('above-detached',options['placement'])
        self.assertLess(bounds[3],120)

    def test_genuinely_offset_large_cross_keeps_its_offset(self):
        old=support();cross=marker(85,0,200)
        bounds,options=placement(cross,old,old,300)
        self.assertLess((bounds[0]+bounds[2])/2,170)
        self.assertEqual('offset-support',options['supportRead']['basis'])

    def test_upside_down_bar_corrects_without_moving_face_cross_bounds(self):
        old=dict(id='wall',shape='rectangle',bounds=[0,0,400,400]);cross=marker(200,70,100,135)
        cross['placement']='within-building';before=copy.deepcopy(cross)
        bounds,options=placement(cross,old,old,400)
        self.assertLess(options['bar'],(bounds[1]+bounds[3])/2)
        self.assertEqual(100,bounds[3]-bounds[1]);self.assertEqual(before,cross)
        # The old face-placement anchor is kept; only the bar moves upward.
        self.assertEqual(80,bounds[1]);self.assertAlmostEqual(115,options['bar'])

    def test_wall_continuity_selects_foreground_but_not_a_grounded_tower(self):
        body=dict(bounds=[0,200,400,450]);tower=dict(bounds=[280,80,360,230],spans=['1:0','2:0'])
        observed=observation([[[0,200],[400,208]],[[280,80],[280,230]],[[360,80],[360,218]]])
        read=wall_in_front(observed,body,tower)
        self.assertEqual({'0:0','1:0','2:0'},set(read['spans']))
        self.assertIsNone(wall_in_front(observed,body,dict(tower,bounds=[280,80,360,450])))
        observed['spans']=observed['spans'][1:]
        self.assertIsNone(wall_in_front(observed,body,tower))

    def test_offset_upper_tower_keeps_lower_wall_whole_and_shares_boundary(self):
        observed=church(openings=False);layout=estimate(observed);body=next(p for p in layout['parts'] if p['role']=='building')
        l,t,r,g=body['bounds'];a,b=r-80,r-20
        layout['parts'].append(dict(id='layout-tower-1',role='tower',bounds=[a,t-90,b,t],
            geometry=_ring([[a,t-90],[b,t-90],[b,t],[a,t]]),status='regional-estimate',spans=[],participants=[body['id']]))
        result=build(observed,layout);parts={p['id']:p for p in result['parts']}
        self.assertEqual(body['bounds'],parts['cleanup-building']['bounds'])
        self.assertEqual(t,parts['cleanup-tower-1']['bounds'][3])
        self.assertNotIn('cleanup-building-frame',parts)
        contacts=[c for c in result['attachments'] if c['child']=='cleanup-tower-1']
        self.assertEqual(2,len(contacts));self.assertTrue(all(c['parent']=='cleanup-building' for c in contacts))

    def test_selected_long_upright_keeps_side_tower_grounded(self):
        observed=church(openings=False);layout=estimate(observed);body=next(p for p in layout['parts'] if p['role']=='building')
        l,t,r,g=body['bounds'];a,b=r-80,r+20
        observed['spans'].append(dict(id='tower-upright',strokeIndex=90,controls=[[b,t-90],[b,g]]))
        tower=dict(id='layout-tower-1',role='tower',bounds=[a,t-90,b,t],
            geometry=_ring([[a,t-90],[b,t-90],[b,t],[a,t]]),status='regional-estimate',spans=['tower-upright'],participants=[body['id']])
        layout['parts'].append(tower)
        result=build(observed,layout);final=next(p for p in result['parts'] if p['id']=='cleanup-tower-1')
        self.assertEqual(g,final['bounds'][3]);self.assertEqual('cleanup-ground',final['parent'])
        self.assertEqual(['tower-upright'],final['groundSupportSpans'])
        self.assertFalse(ground_support(observed,body,dict(tower,spans=[])))

    def test_windows_clear_door_while_preserving_size_containment_and_mirror(self):
        specs,result=openings();before=copy.deepcopy(specs);checks=separate(specs,result)
        self.assertEqual(4,len(specs));self.assertEqual(before[1],specs[1])
        for p in specs[2:]:
            self.assertGreaterEqual(gap(p['bounds'],specs[1]['bounds']),24-.002)
            self.assertEqual(48,p['bounds'][2]-p['bounds'][0])
            self.assertEqual(24,p['bounds'][3]-p['bounds'][1])
            self.assertTrue(0<p['bounds'][0]<p['bounds'][2]<400)
        self.assertEqual(400,specs[2]['bounds'][0]+specs[3]['bounds'][2])
        self.assertFalse(any(c['status']=='needs-review' for c in checks))
        self.assertTrue(all(len(r['participants'])==3 for r in result['relations']))

    def test_impossible_added_pair_is_open_but_observed_pair_is_retained_for_review(self):
        for prior in (True,False):
            specs,result=openings(prior);specs[1]['bounds']=[8,0,392,120]
            checks=separate(specs,result)
            self.assertEqual(0 if prior else 2,sum(p['role']=='window' for p in specs))
            if prior:
                self.assertFalse(result['symmetries'])
                self.assertEqual(2,sum(r['status']=='unplaced-optional' for r in result['relations']))
            else:self.assertTrue(any(c['status']=='needs-review' for c in checks))

    def test_detached_one_of_two_crosses_exposes_mirror_residue(self):
        parts=[dict(id='a',role='cross',bounds=[60,20,100,100]),
               dict(id='b',role='cross',bounds=[300,40,340,120]),dict(id='axis',bounds=[0,120,400,450])]
        pairs=[dict(left='a',right='b',axisPart='axis')]
        check=mirrors(parts,pairs)[0]
        self.assertEqual('needs-review',check['status']);self.assertEqual([0,0,-20,-20],check['signedResiduals'])
        self.assertEqual({'surface':'cleanup.symmetries','index':0},check['nativeRead'])
        parts[0]['bounds']=[60,40,100,120]
        self.assertEqual('within-allowance',mirrors(parts,pairs)[0]['status'])

    def test_added_openings_are_gray_and_dotted_observed_openings_keep_color(self):
        for present in (False,True):
            observed=church(openings=present);result=build(observed,estimate(observed))
            for p in result['parts']:
                if p['role'] not in ('door','window'):continue
                drawing=svg({'observation':observed,'cleanup':dict(parts=[p])})
                self.assertIn('stroke="'+(COLORS[p['role']] if present else '#9aa2a6')+'"',drawing)
                self.assertEqual(not present,'stroke-dasharray="1 4"' in drawing)


if __name__=='__main__':unittest.main()
