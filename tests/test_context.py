import copy
import unittest

from gojo_quickdraw.context import build_context


def fixture():
    segments = [
        ("left-wall", [[10, 40], [10, 100]]),
        ("right-wall", [[90, 40], [90, 100]]),
        ("wall-top", [[10, 40], [90, 40]]),
        ("wall-bottom", [[10, 100], [90, 100]]),
        ("roof-left", [[10, 40], [50, 5]]),
        ("roof-right", [[50, 5], [90, 40]]),
        ("window-a1", [[20, 55], [30, 55]]),
        ("window-a2", [[30, 55], [30, 70]]),
        ("window-a3", [[30, 70], [20, 70]]),
        ("window-a4", [[20, 70], [20, 55]]),
        ("window-b1", [[70, 55], [80, 55]]),
        ("window-b2", [[80, 55], [80, 70]]),
        ("window-b3", [[80, 70], [70, 70]]),
        ("window-b4", [[70, 70], [70, 55]]),
    ]
    spans = [{"id": sid, "controls": points} for sid, points in segments]
    observation = {"sourceId": "fixture", "bounds": [10, 5, 90, 100], "spans": spans,
                   "strokes": [{"id": "stroke-left", "spans": ["left-wall"]},
                               {"id": "stroke-right", "spans": ["right-wall"]},
                               {"id": "stroke-top", "spans": ["wall-top"]},
                               {"id": "stroke-bottom", "spans": ["wall-bottom"]},
                               {"id": "stroke-roof", "spans": ["roof-left", "roof-right"]},
                               {"id": "stroke-window-a", "spans": ["window-a1", "window-a2", "window-a3", "window-a4"]},
                               {"id": "stroke-window-b", "spans": ["window-b1", "window-b2", "window-b3", "window-b4"]}],
                   "crosses": []}
    annotations = {"sourceId": "fixture", "symmetries": [], "joints": [],
                   "relations": [{"id": "roof-junction", "kind": "roof-slopes", "spans": ["roof-left", "roof-right"],
                                  "point": [50, 5], "measured": {}}]}
    return observation, annotations


class ContextTests(unittest.TestCase):
    def test_source_linked_parts_and_optional_parts(self):
        observation, annotations = fixture()
        result = build_context(observation, annotations)
        kinds = [part["kind"] for part in result["parts"]]
        self.assertEqual(kinds.count("window"), 2)
        self.assertIn("body", kinds)
        self.assertIn("roof", kinds)
        self.assertNotIn("cross", kinds)
        self.assertNotIn("door", kinds)
        source = {span["id"] for span in observation["spans"]}
        for part in result["parts"]:
            self.assertTrue(part["spans"])
            self.assertTrue(set(part["spans"]) <= source)
            self.assertEqual(part["status"], "seeded-candidate")

    def test_ground_is_hidden_seed_not_observed_line(self):
        observation, annotations = fixture()
        result = build_context(observation, annotations)
        ground = next(row for row in result["assumptions"] if row["kind"] == "support-ground")
        self.assertEqual(ground["status"], "seeded-hidden")
        self.assertFalse(ground["expected"]["drawn"])
        self.assertEqual(ground["geometry"]["segments"], [[0, 1]])
        self.assertNotIn(ground["id"], {s["id"] for s in observation["spans"]})

    def test_closure_and_containment_are_distinct(self):
        result = build_context(*fixture())
        kinds = [row["kind"] for row in result["relations"]]
        self.assertEqual(kinds.count("containment"), 2)
        self.assertEqual(kinds.count("closure"), 2)
        self.assertEqual(kinds.count("paired-placement"), 1)
        closure = next(row for row in result["relations"] if row["kind"] == "closure")
        containment = next(row for row in result["relations"] if row["kind"] == "containment")
        self.assertEqual(len(closure["participants"]), 1)
        self.assertEqual(len(containment["participants"]), 2)

    def test_view_and_physical_scale_remain_open(self):
        result = build_context(*fixture())
        top = next(row for row in result["views"] if row["kind"] == "top")
        self.assertEqual(top["status"], "open")
        self.assertEqual(top["support"], [])
        for kind in ("physical-scale", "gravity-direction", "support-material", "camera-elevation"):
            self.assertEqual(next(row for row in result["assumptions"] if row["kind"] == kind)["status"], "open")

    def test_inputs_remain_exactly_unchanged(self):
        observation, annotations = fixture()
        expected = copy.deepcopy((observation, annotations))
        build_context(observation, annotations)
        self.assertEqual((observation, annotations), expected)

    def test_candidate_geometry_is_an_independent_snapshot(self):
        observation, annotations = fixture()
        expected = copy.deepcopy((observation, annotations))
        context = build_context(observation, annotations)
        roof = next(part for part in context["parts"] if part["kind"] == "roof")
        roof["geometry"]["points"][0][0] = -999
        self.assertEqual((observation, annotations), expected)

    def test_unsupported_nonchurch_parts_not_inserted(self):
        result = build_context(*fixture(), category="frog")
        self.assertEqual(result["parts"], [])
        self.assertEqual(result["relations"], [])
        self.assertEqual(len(result["unassignedSpanIds"]), len(fixture()[0]["spans"]))

    def test_empty_source_preserves_open_assumptions(self):
        result = build_context({"sourceId": "empty", "bounds": None, "spans": []}, None)
        self.assertEqual(result["parts"], [])
        self.assertEqual(len(result["views"]), 5)
        self.assertFalse(any(row["kind"] == "support-ground" for row in result["assumptions"]))

    def test_asymmetric_windows_keep_measured_residue(self):
        observation, annotations = fixture()
        for span in observation["spans"]:
            if span["id"].startswith("window-b"):
                span["controls"] = [[x - 7, y + 3] for x, y in span["controls"]]
        result = build_context(observation, annotations)
        pair = next(row for row in result["relations"] if row["kind"] == "paired-placement")
        self.assertEqual(pair["measured"]["midpointOffset"], -3.5)
        self.assertEqual(pair["measured"]["heightDifference"], 3.)


if __name__ == "__main__":
    unittest.main()
