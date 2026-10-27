import copy
import unittest

from gojo_quickdraw.cleanup import build as cleanup
from gojo_quickdraw.layout import estimate
from gojo_quickdraw.layout_mapping import build as mapping
from gojo_quickdraw.layout_panes import enrich
from test_layout_roofs import observation
from test_opening_evidence import facade, circle


def panes(x, y, size=48):
    return [[[x,y],[x+size,y],[x+size,y+size],[x,y+size],[x,y]],
            [[x+size/2,y],[x+size/2,y+size]],
            [[x,y+size/2],[x+size,y+size/2]]]


class WindowPaneTests(unittest.TestCase):
    def test_multiple_frames_keep_their_dividers_and_separate_identities(self):
        for count in (1,2,3,4):
            obs=facade([p for x in (70,160,250,340)[:count] for p in panes(x,220)])
            before=copy.deepcopy(obs);layout=estimate(obs);final=cleanup(obs,layout)
            windows=[p for p in layout['parts'] if p['role']=='window']
            refined=[p for p in final['parts'] if p['role']=='window']
            self.assertEqual(count,len(windows));self.assertEqual(count,len(refined))
            self.assertEqual(count,len({p['id'] for p in windows}))
            for p in windows+refined:
                self.assertEqual('rectangle',p['shape']);self.assertIn('panePattern',p)
                geo=p['geometry'];degrees=[sum(i in s for s in geo['segments']) for i in range(len(geo['points']))]
                self.assertEqual([2,2,2,2,3,3,3,3,4],degrees)
            self.assertTrue(all(p['sourcePart'] for p in refined))
            self.assertEqual(before,obs)

    def test_interior_plus_supports_a_gapped_uneven_frame(self):
        # Authored analogue of the reported left window. The top stroke falls
        # short of the left side, and both divider strokes overrun the frame.
        obs=facade([[[105,216],[102,267],[172,278],[178,239],[127,229]],
                    [[139,229],[127,279]],[[103,245],[183,256]]])
        p=next(p for p in estimate(obs)['parts'] if p['role']=='window')
        self.assertIn('panePattern',p)
        self.assertEqual({'4:0','4:1','4:2','4:3'},set(p['panePattern']['frameSpans']))
        self.assertEqual(['5:0'],p['panePattern']['verticalSpans'])
        self.assertEqual(['6:0'],p['panePattern']['horizontalSpans'])

    def test_upper_panes_do_not_become_church_crosses(self):
        obs=facade(panes(90,190)+[[[240,0],[240,40]],[[220,13],[260,13]]])
        obs['crosses']=[{'point':[114,214],'spans':['5:0','6:0']},
                        {'point':[240,13],'spans':['7:0','8:0']}]
        before=copy.deepcopy(obs);result=estimate(obs)
        actual=[p for p in result['parts'] if p['role']=='cross' and p['status']!='prior-only']
        self.assertEqual(1,len(actual));self.assertEqual({'7:0','8:0'},set(actual[0]['spans']))
        self.assertEqual(1,len([p for p in result['parts'] if p['role']=='window']))
        self.assertEqual(before,obs,'Raw crossing evidence is retained as measured, not deleted')

    def test_bare_plus_round_frame_and_external_cross_do_not_invent_panes(self):
        examples=[([[[130,200],[130,260]],[[100,230],[160,230]]],[100,200,160,260]),
                  ([circle(130,230,30),[[130,200],[130,260]],[[100,230],[160,230]]],[100,200,160,260]),
                  (panes(100,200)[:1]+[[[190,200],[190,260]],[[160,230],[220,230]]],[100,200,148,248])]
        for paths,bounds in examples:
            obs=observation(paths);region=dict(bounds=bounds,spans=[s['id'] for s in obs['spans']])
            self.assertNotIn('panePattern',enrich(obs,region))

    def test_unplaced_upper_frame_keeps_competing_drawn_cross(self):
        obs=observation([[[192,98],[325,98],[325,234],[192,234],[192,98]],
            [[252,120],[252,220]],[[220,180],[300,180]],
            [[50,480],[50,260],[192,250],[208,40],[335,32],[335,250],[458,270],[458,480],[50,480]]])
        obs['crosses']=[{'point':[252,180],'spans':['1:0','2:0']}]
        result=estimate(obs)
        self.assertFalse(any(p['role']=='window' for p in result['parts']))
        self.assertTrue(result['markerReads']['enclosedDividerCandidates'])
        self.assertFalse(result['markerReads']['enclosedDividerCandidates'][0]['acceptedAsWindow'])
        cross=next(p for p in result['parts'] if p['role']=='cross')
        self.assertEqual({'1:0','2:0'},set(cross['spans']))
        self.assertEqual('regional-estimate',cross['status'])

    def test_mapping_separates_frame_and_divider_source_intervals(self):
        obs=facade(panes(90,220));layout=estimate(obs)
        window=next(p for p in layout['parts'] if p['role']=='window')
        result=mapping(obs,layout)
        targets=[t for t in result['targets'] if t['part']==window['id']]
        self.assertEqual(6,len(targets))
        for t,group in zip(targets,window['geometry']['boundaryGroups']):
            self.assertTrue(t['ink'])
            self.assertTrue({i['span'] for i in t['ink']}<=set(group['sourceSpans']))
        for span in obs['spans']:
            ranges=[r for r in result['sourceCoverage'] if r['span']==span['id']]
            self.assertAlmostEqual(1,sum(r['to']-r['from'] for r in ranges))


if __name__=='__main__':unittest.main()
