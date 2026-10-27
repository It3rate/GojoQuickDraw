import copy
import math
import unittest

from gojo_quickdraw.layout import estimate, _opening_regions
from gojo_quickdraw.layout_openings import classify, geometry, outline
from gojo_quickdraw.cleanup import build
from gojo_quickdraw.cleanup_display import svg
from test_layout import church
from test_layout_roofs import observation


def facade(openings=()):
    return observation([[[40,150],[40,480]], [[440,150],[440,480]],
                        [[40,150],[240,40],[440,150],[40,150]], [[40,480],[440,480]], *openings])


def circle(x,y,r):
    return [[x+r*math.cos(i*math.tau/40),y+r*math.sin(i*math.tau/40)] for i in range(41)]


class OpeningEvidenceTests(unittest.TestCase):
    def test_large_drawn_door_survives_split_wall_and_does_not_seed_extra_windows(self):
        obs=observation([[[45,195],[55,475]], [[40,185],[395,150],[470,165],[452,335],[453,438]],
            [[40,182],[243,32]], [[466,163],[330,69]],
            [[197,452],[184,317],[390,305],[368,458]], [[275,323],[282,431]],
            [[298,388],[306,382],[285,373],[293,383]], [[59,480],[462,439]]])
        before=copy.deepcopy(obs);layout=estimate(obs);result=build(obs,layout)
        body=next(p for p in layout['parts'] if p['role']=='building')
        self.assertLess(body['bounds'][1],210, 'Door lintel is not the wall top')
        door=next(p for p in result['parts'] if p['role']=='door')
        self.assertEqual('refined-estimate',door['status']);self.assertEqual('rectangle',door['shape'])
        self.assertGreater(door['bounds'][2]-door['bounds'][0],190)
        self.assertAlmostEqual(287,(door['bounds'][0]+door['bounds'][2])/2,delta=4)
        self.assertFalse(any(p['role']=='window' for p in result['parts']))
        roof=next(p for p in layout['parts'] if p['role']=='roof')
        self.assertEqual('inferred-slope-intersection',roof['tipCompletion'])
        self.assertEqual(before,obs)

    def test_large_round_window_is_not_shrunk_to_door_size(self):
        obs=facade([circle(240,270,76),[[220,480],[220,392],[260,392],[260,480]]])
        before=copy.deepcopy(obs);layout=estimate(obs);result=build(obs,layout)
        window=next(p for p in result['parts'] if p['role']=='window')
        door=next(p for p in result['parts'] if p['role']=='door')
        self.assertEqual('circle',window['shape']);self.assertEqual('refined-estimate',window['status'])
        self.assertAlmostEqual(152,window['bounds'][2]-window['bounds'][0],delta=1)
        self.assertGreater(window['bounds'][2]-window['bounds'][0],door['bounds'][2]-door['bounds'][0])
        self.assertEqual(32,len(window['geometry']['segments']))
        self.assertTrue(window['sourcePart']);self.assertTrue(window['sourceStrokeOrder'])
        self.assertEqual(before,obs)

    def test_window_count_follows_evidence_one_three_or_four(self):
        for count in (1,3,4):
            paths=[[[x,220],[x+38,220],[x+38,265],[x,265],[x,220]] for x in (70,160,250,340)[:count]]
            obs=facade(paths);layout=estimate(obs);result=build(obs,layout)
            windows=[p for p in result['parts'] if p['role']=='window']
            self.assertEqual(count,len(windows));self.assertTrue(all(p['status']=='refined-estimate' for p in windows))

    def test_multistroke_window_and_arch_use_perimeter_support(self):
        paths=[[[80,210],[130,210]],[[130,212],[130,260]],[[129,260],[80,260]],[[80,259],[80,212]],
               outline([210,350,280,480],45)]
        obs=facade(paths);layout=estimate(obs);result=build(obs,layout)
        window=next(p for p in result['parts'] if p['role']=='window')
        door=next(p for p in result['parts'] if p['role']=='door')
        self.assertEqual('rectangle',window['shape']);self.assertEqual('arch',door['shape'])
        self.assertGreater(len(window['sourceStrokeOrder']),1)
        source=next(p for p in layout['parts'] if p['id']==door['sourcePart'])
        self.assertEqual(source['archFraction'],door['archFraction'])

    def test_partial_stroke_keeps_only_selected_opening_intervals(self):
        obs=facade([[[40,480],[160,480],[160,360],[235,360],[235,480]]])
        layout=estimate(obs);door=next(p for p in layout['parts'] if p['role']=='door')
        self.assertNotIn('4:0',door['spans']);self.assertIn('4:2',door['spans'])

    def test_cross_handle_and_writing_are_not_supported_windows(self):
        for path in ([[[80,200],[120,230],[80,260],[120,260]]],
                     [[[100,210],[100,260]],[[80,235],[125,235]]],
                     [[[290,380],[305,375],[315,385],[300,390]]]):
            obs=facade(path)
            self.assertFalse(any(p['role']=='window' for p in estimate(obs)['parts']))

    def test_no_automatic_pair_and_no_entrance_without_strong_context(self):
        for obs in (church(openings=False,cross=False), facade()):
            result=build(obs,estimate(obs))
            self.assertFalse(any(p['role'] in ('door','window') for p in result['parts']))
        obs=church(openings=False);result=build(obs,estimate(obs))
        added=[p for p in result['parts'] if p['role']=='door']
        self.assertEqual(1,len(added));self.assertEqual('rectangle',added[0]['shape'])
        self.assertEqual('prior-only',added[0]['status']);self.assertIsNone(added[0]['sourcePart'])
        self.assertTrue(any(r['kind']=='shape-refinement-entrance-expectation' for r in result['relations']))
        # A failed detector must not place an imaginary door over unresolved ink.
        obs['strokes'].append({'index':99,'points':[[215,350],[240,390],[210,430]],'spans':['partial']})
        obs['spans'].append({'id':'partial','strokeIndex':99,'controls':[[215,350],[240,390],[210,430]]})
        self.assertFalse(any(p['role']=='door' for p in build(obs,estimate(obs))['parts']))

    def test_prior_cross_is_gray_dotted_but_supported_cross_stays_colored(self):
        for present in (False,True):
            obs=church(cross=present);result=build(obs,estimate(obs));p=next(p for p in result['parts'] if p['role']=='cross')
            image=svg({'observation':obs,'cleanup':{'parts':[p]}})
            self.assertIn('stroke="'+(p['color'] if present else '#9aa2a6')+'"',image)
            self.assertEqual(not present,'stroke-dasharray="1 4"' in image)


if __name__=='__main__':unittest.main()
