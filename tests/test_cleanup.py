import copy
import unittest
from gojo_quickdraw.layout import estimate
from gojo_quickdraw.cleanup import build, enrich_context
from gojo_quickdraw.layout_outlines import crosses
from gojo_quickdraw.layout_side_roofs import regions
from gojo_quickdraw.layout import _ring
from gojo_quickdraw.layout_markers import cross_geometry
from gojo_quickdraw.cleanup_crosses import dimensions
from gojo_quickdraw.cleanup_partitions import intersection, subtract
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

    def test_large_cross_size_is_monotone_and_not_limited_by_a_narrow_tower(self):
        heights=[]
        for height in (30,60,120,240,480):
            widths,h=dimensions({'bounds':[0,0,.6*height,height]},300)
            heights.append(h)
            self.assertGreaterEqual(h,.55*height)
        self.assertEqual(heights,sorted(set(heights)))
        observed=church();layout=estimate(observed)
        roof=next(p for p in layout['parts'] if p['role']=='roof')
        roof.update(role='tower',shape='rectangle',bounds=[235,110,255,230])
        roof['geometry']=_ring([[235,110],[255,110],[255,230],[235,230]])
        cross=next(p for p in layout['parts'] if p['role']=='cross')
        cross.update(bounds=[205,-10,277,110],geometry=cross_geometry(241,-10,120),placement='above-tower')
        final=next(p for p in build(observed,layout)['parts'] if p['role']=='cross')
        self.assertAlmostEqual(120,final['bounds'][3]-final['bounds'][1],places=2)

    def test_face_cross_spanning_a_shallow_roof_is_not_squeezed_or_raised(self):
        observed=church();layout=estimate(observed)
        roof=next(p for p in layout['parts'] if p['role']=='roof')
        roof['bounds'][1]=roof['bounds'][3]-55
        cross=next(p for p in layout['parts'] if p['role']=='cross');top=roof['bounds'][1]+20
        cross.update(bounds=[205,top,277,top+120],geometry=cross_geometry(241,top,120),placement='within-roof')
        final=next(p for p in build(observed,layout)['parts'] if p['role']=='cross')
        self.assertEqual('on-face',final['placement'])
        self.assertGreater(final['bounds'][3]-final['bounds'][1],110)
        self.assertAlmostEqual(top,final['bounds'][1],delta=.01)
        self.assertTrue(all(s is None for s in final['geometry']['siteSources']))

    def test_partition_keeps_area_and_does_not_treat_contact_as_overlap(self):
        box=[0,0,100,100];masks=[[40,-20,60,100],[55,50,80,100]]
        pieces=subtract(box,masks)
        self.assertEqual(7000,sum((p[2]-p[0])*(p[3]-p[1]) for p in pieces))
        for i,a in enumerate(pieces):
            self.assertTrue(all(intersection(a,b) is None for b in masks+pieces[i+1:]))
        self.assertEqual([box],subtract(box,[[100,0,150,100]]))

    def test_central_tower_splits_wall_and_retains_shared_sites_and_source(self):
        observed=church(openings=False);layout=estimate(observed)
        body=next(p for p in layout['parts'] if p['role']=='building');l,t,r,g=body['bounds'];cx=(l+r)/2
        tower={'id':'layout-tower-1','role':'tower','bounds':[cx-40,t-80,cx+40,g],
               'geometry':_ring([[cx-40,t-80],[cx+40,t-80],[cx+40,g],[cx-40,g]]),
               'status':'regional-estimate','spans':[],'participants':[body['id']]}
        layout['parts'].append(tower);before=copy.deepcopy(layout)
        result=build(observed,layout);parts={p['id']:p for p in result['parts']}
        frame=parts['cleanup-building-frame'];walls=[p for p in parts.values() if p['role']=='building' and p['shape']=='rectangle']
        self.assertEqual(2,len(walls));self.assertEqual([],frame['geometry']['segments'])
        self.assertEqual(before,layout)
        for wall in walls:
            self.assertEqual(body['id'],wall['sourcePart']);self.assertIn(frame['id'],wall['participants'])
            self.assertIsNone(intersection(wall['bounds'],parts['cleanup-tower-1']['bounds']))
            self.assertTrue(any(c['parent']==wall['id'] and c['child']=='cleanup-tower-1' for c in result['attachments']))
        for p in result['parts']:
            for point,ref in zip(p['geometry']['points'],p['geometry']['siteSources']):
                if ref:self.assertEqual(point,parts[ref['part']]['geometry']['points'][ref['index']])
        for p in parts.values():
            if p['role'] in ('window','door'):
                a,b=parts[p['parent']]['bounds'],p['bounds']
                self.assertTrue(a[0]<=b[0]<b[2]<=a[2] and a[1]<=b[1]<b[3]<=a[3])

    def test_tower_on_wall_does_not_duplicate_existing_base_contacts(self):
        observed=church(openings=False);layout=estimate(observed)
        body=next(p for p in layout['parts'] if p['role']=='building');l,t,r,g=body['bounds'];cx=(l+r)/2
        layout['parts'].append({'id':'layout-tower-1','role':'tower','bounds':[cx-40,t-80,cx+40,t],
            'geometry':_ring([[cx-40,t-80],[cx+40,t-80],[cx+40,t],[cx-40,t]]),
            'status':'regional-estimate','spans':[],'participants':[body['id']]})
        result=build(observed,layout)
        identities=[(c['parent'],c['child'],c['parentPoint']) for c in result['attachments']]
        self.assertEqual(len(identities),len(set(identities)))

    def test_sloping_cross_arms_keep_inset_stems_without_requiring_closure(self):
        path=[[230,280],[230,170],[120,180],[130,115],[165,130],[235,135],
              [232,32],[295,47],[292,119],[351,143],[254,176],[270,229]]
        result=crosses(observation([path]),[0,0,450,500])
        self.assertEqual(1,len(result));self.assertTrue(result[0]['openContour'])


if __name__=='__main__':unittest.main()
