import copy
import unittest
from gojo_quickdraw.layout import estimate
from gojo_quickdraw.cleanup import build, enrich_context
from gojo_quickdraw.layout_outlines import crosses
from gojo_quickdraw.layout_side_roofs import regions
from test_layout import church
from test_layout_roofs import observation


class CleanupTests(unittest.TestCase):
    def test_peak_relocation_moves_attached_cross_and_preserves_input(self):
        observed=church(openings=False);layout=estimate(observed)
        roof=next(p for p in layout['parts'] if p['role']=='roof')
        roof['apex'][0]+=45;roof['geometry']['points'][1][0]+=45
        cross=next(p for p in layout['parts'] if p['role']=='cross')
        before=copy.deepcopy((observed,layout));result=build(observed,layout)
        parts={p['id']:p for p in result['parts']};r=parts['cleanup-roof'];c=parts['cleanup-cross']
        self.assertEqual((r['bounds'][0]+r['bounds'][2])/2,r['geometry']['points'][1][0])
        self.assertEqual(r['geometry']['points'][1],c['geometry']['points'][14])
        self.assertEqual({'part':r['id'],'index':1},c['geometry']['siteSources'][14])
        self.assertNotEqual(cross['bounds'],c['bounds'])
        self.assertEqual(before,(observed,layout))
        for p in result['parts']:
            self.assertFalse(p['spans'])
            if p['role'] in ('door','window'):
                self.assertEqual('prior-only',p['status']);self.assertIsNone(p['sourcePart'])
                l,t,rr,b=parts[p['parent']]['bounds'];a,y,c,d=p['bounds']
                self.assertTrue(l<=a<c<=rr and t<=y<d<=b)

    def test_shared_contacts_only_reference_earlier_proposed_sites(self):
        observed=church();initial={'parts':[],'relations':[]};context,layout=__import__('gojo_quickdraw.layout',fromlist=['enrich_context']).enrich_context(observed,initial)
        context,clean=enrich_context(observed,context,layout);known={}
        for p in context['parts']:
            self.assertTrue(set(p.get('participants',[]))<=set(known))
            for point,source in zip(p.get('geometry',{}).get('points',[]),p.get('geometry',{}).get('siteSources',[])):
                if source:self.assertEqual(point,known[source['part']]['geometry']['points'][source['index']])
            known[p['id']]=p
        again,_=enrich_context(observed,context,layout);self.assertEqual(context,again)

    def test_outline_can_be_open_multistroke_and_is_not_a_rectangle_or_grid(self):
        paths=[[[190,310],[190,170]],[[240,280],[240,170]],
               [[190,170],[90,170],[90,110],[160,110]],
               [[240,170],[300,170],[300,110],[230,110],[230,40],[160,40],[160,110]]]
        hints=crosses(observation(paths),[0,0,450,500]);self.assertEqual(1,len(hints))
        self.assertEqual('outline-contour-read',hints[0]['markerBasis'])
        self.assertTrue(hints[0]['openContour'])
        for paths in ([[[100,50],[250,50],[250,250],[100,250],[100,50]]],
                      [[[100,50],[250,50],[250,250],[100,250],[100,50]],[[175,50],[175,250]],[[100,150],[250,150]]]):
            self.assertFalse(crosses(observation(paths),[0,0,450,500]))

    def test_side_roof_requires_eave_or_sloping_end_and_preserves_side_view(self):
        obs=observation([[[20,280],[50,180],[300,180]],[[20,280],[300,280]]])
        roofs=regions(obs,[20,280,400,470],[0,0,450,500],[],[{'bounds':[300,50,400,280]}],set())
        self.assertEqual(1,len(roofs));self.assertEqual('parallelogram',roofs[0]['shape'])
        self.assertEqual(300,roofs[0]['quad'][-1][0])
        self.assertFalse(regions(observation([[[50,180],[300,180]]]),[20,280,400,470],[0,0,450,500],[],[],set()))

    def test_no_building_stays_open(self):
        result=build({'sourceId':'empty'},{'parts':[]})
        self.assertEqual('open-no-building-area',result['status']);self.assertFalse(result['parts'])

    def test_half_tick_centers_still_bind_the_actual_parent_position(self):
        observed=church(openings=False);layout=estimate(observed)
        body=next(p for p in layout['parts'] if p['role']=='building');body['bounds'][0]+=.001
        result=build(observed,layout);known={p['id']:p for p in result['parts']}
        for contact in result['attachments']:
            parent=known[contact['parent']]['geometry']['points'][contact['parentPoint']]
            child=known[contact['child']]['geometry']['points'][contact['childPoint']]
            self.assertEqual(parent,child)


if __name__=='__main__':unittest.main()
