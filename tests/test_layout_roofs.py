import copy
import math
import unittest

from gojo_quickdraw.layout import _cap_geometry, _rectangle, estimate
from gojo_quickdraw.layout_markers import markers
from gojo_quickdraw.layout_roofs import regions


def observation(paths):
    result = {'sourceId': 'authored-cap-controls', 'strokes': [], 'spans': [], 'crosses': []}
    for i, points in enumerate(paths):
        ids = []
        for j, (a, b) in enumerate(zip(points, points[1:])):
            sid = f'{i}:{j}'; ids.append(sid)
            result['spans'].append({'id': sid, 'strokeIndex': i, 'controls': [a, b],
                                    'startIndex': j, 'endIndex': j+1})
        result['strokes'].append({'index': i, 'points': points, 'spans': ids})
    return result


def dome(cx, half_width, top, base):
    return [[cx+half_width*math.cos(math.pi-i*math.pi/16),
             base-(base-top)*math.sin(math.pi-i*math.pi/16)] for i in range(17)]


class LocalCapTests(unittest.TestCase):
    building = [0, 200, 400, 400]
    scene = [0, 0, 400, 400]

    def test_two_distinct_caps_include_dome_and_keep_tower_bases_implied(self):
        paths = [[[40, 120], [80, 40], [120, 120]], dome(300, 50, 60, 120)]
        paths += [[[x, 120], [x, 400]] for x in (40, 120, 250, 350)]
        observed = observation(paths); before = copy.deepcopy(observed)
        caps, reads = regions(observed, self.building, self.scene, set())
        self.assertEqual(['triangle', 'dome'], [c['shape'] for c in caps])
        self.assertEqual([400, 400], [c['towerBottom'] for c in caps])
        self.assertTrue(all(c['baseBoundary'] == 'inferred-level' for c in caps))
        self.assertFalse(set(caps[0]['spans']) & set(caps[1]['spans']))
        self.assertEqual(before, observed)
        reverse, _ = regions(observation([list(reversed(p)) for p in paths]), self.building, self.scene, set())
        self.assertEqual([c['shape'] for c in caps], [c['shape'] for c in reverse])
        self.assertFalse(reads['capacityReached'])

    def test_narrow_offset_roof_does_not_expand_to_main_wall_corners(self):
        caps, _ = regions(observation([[[240, 200], [280, 100], [340, 200]]]),
                          self.building, self.scene, set())
        self.assertEqual(1, len(caps))
        self.assertEqual([240, 100, 340, 200], caps[0]['bounds'])
        self.assertIsNone(caps[0]['towerBottom'])

    def test_upper_contour_wins_over_straighter_interior_eave(self):
        paths = [[[40, 170], [200, 20], [360, 210]],
                 [[40, 210], [100, 170], [360, 210]]]
        caps, _ = regions(observation(paths), self.building, self.scene, set())
        self.assertEqual(1, len(caps))
        self.assertEqual(20, caps[0]['bounds'][1])
        self.assertIn('unexplainedUpperContour', caps[0]['costTerms'])

    def test_disconnected_cap_is_not_attached_by_coordinate_height(self):
        paths = [[[-150, 160], [-100, 50], [-50, 160]], [[40, 200], [200, 40], [360, 200]]]
        caps, reads = regions(observation(paths), self.building, [-150, 0, 400, 400], set())
        self.assertEqual(1, len(caps))
        self.assertGreaterEqual(caps[0]['bounds'][0], 0)
        self.assertTrue(reads['unattachedRegions'])

    def test_capacity_leaves_unselected_regions_visible(self):
        observed = observation([[[40, 120], [80, 40], [120, 120]],
                                [[250, 120], [300, 40], [350, 120]]])
        caps, reads = regions(observed, self.building, self.scene, set(), maximum=1)
        self.assertEqual(1, len(caps))
        self.assertTrue(reads['capacityReached'])
        self.assertTrue(reads['withheldRegions'])

    def test_dome_has_closed_ordered_boundary_and_explicit_parent_sites(self):
        cap = {'shape': 'dome', 'bounds': [100, 60, 200, 140], 'apex': [150, 60]}
        rectangle, maps = _rectangle([100, 140, 200, 400], top_anchors=[150])
        geo = _cap_geometry(cap, 'tower', maps[0])
        self.assertEqual(len(geo['points']), len(geo['segments']))
        self.assertEqual([150, 60], geo['points'][16])
        self.assertEqual(3, sum(ref is not None for ref in geo['siteSources']))
        for i, (a, b) in enumerate(geo['segments']):
            self.assertEqual(b, geo['segments'][(i+1) % len(geo['segments'])][0])
        for p, ref in zip(geo['points'], geo['siteSources']):
            if ref: self.assertEqual(p, rectangle['points'][ref['index']])

    def test_crosses_are_independent_and_partial_marker_is_not_a_full_detection(self):
        roofs = [{'bounds': [40, 120, 120, 200], 'apex': [80, 120]},
                 {'bounds': [250, 120, 350, 200], 'apex': [300, 120]}]
        observed = observation([[[80, 30], [80, 120]], [[60, 60], [100, 60]],
                                [[300, 30], [300, 120]]])
        observed['crosses'] = [{'point': [80, 60], 'spans': ['0:0', '1:0']}]
        hints, _ = markers(observed, roofs, self.building, self.scene)
        self.assertEqual(2, len(hints))
        self.assertEqual([0, 1], [h['roofIndex'] for h in hints])
        self.assertEqual(['crossing-read', 'upright-marker-prior'], [h['markerBasis'] for h in hints])
        self.assertTrue(hints[1]['missingHorizontalArm'])
        observed['crosses'].append({'point': [200, 80], 'spans': ['central-upright', 'central-arm']})
        hints, _ = markers(observed, roofs, self.building, self.scene)
        self.assertIsNone(next(h for h in hints if h['point'] == [200, 80])['roofIndex'])

    def test_whole_layout_orders_multiple_tower_dependencies_without_source_mutation(self):
        paths = [[[0, 240], [0, 400]], [[400, 240], [400, 400]], [[0, 240], [400, 240]],
                 [[0, 400], [400, 400]], [[40, 120], [80, 40], [120, 120]],
                 [[250, 120], [300, 40], [350, 120]]]
        paths += [[[x, 120], [x, 240]] for x in (40, 120, 250, 350)]
        observed = observation(paths); before = copy.deepcopy(observed)
        result = estimate(observed)
        self.assertEqual(2, sum(p['role'] == 'tower' for p in result['parts']))
        self.assertEqual(2, sum(p['role'] == 'roof' for p in result['parts']))
        known = {}
        for part in result['parts']:
            self.assertTrue(set(part['participants']) <= set(known))
            for point, ref in zip(part['geometry']['points'], part['geometry'].get('siteSources', [])):
                if ref:
                    self.assertIn(ref['part'], part['participants'])
                    self.assertEqual(point, known[ref['part']]['geometry']['points'][ref['index']])
            known[part['id']] = part
        self.assertEqual(before, observed)


if __name__ == '__main__': unittest.main()
