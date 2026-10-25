import copy
import unittest
from unittest.mock import patch

from gojo_quickdraw.objects import POLICY, _features, enrich_context
from gojo_quickdraw.opinions import Comparison, Request, heuristic


def fixture():
    segments = [
        ("wall-l", [[0, 40], [0, 160]]), ("wall-t", [[0, 40], [200, 40]]),
        ("wall-r", [[200, 40], [200, 160]]), ("wall-b", [[200, 160], [0, 160]]),
        ("a1", [[30, 70], [55, 70]]), ("a2", [[55, 70], [55, 105]]),
        ("a3", [[55, 105], [30, 105]]), ("a4", [[30, 105], [30, 70]]),
        ("b1", [[145, 70], [170, 70]]), ("b2", [[170, 70], [170, 105]]),
        ("b3", [[170, 105], [145, 105]]), ("b4", [[145, 105], [145, 70]]),
        ("roof-l", [[0, 40], [100, 0]]), ("roof-r", [[100, 0], [200, 40]]),
        ("unknown", [[230, 95], [240, 110]])]
    spans = [{"id": sid, "controls": points, "lineError": {"max": 0}} for sid, points in segments]
    observation = {"sourceId": "objects-fixture", "bounds": [0, 0, 240, 160], "spans": spans,
                   "strokes": [{"id": "stroke-"+str(i), "index": i, "spans": [s["id"]]} for i, s in enumerate(spans)]}
    context = {"sourceId": observation["sourceId"], "category": "church", "relations": [], "parts": [
        {"id": "legacy-body", "kind": "body", "spans": ["wall-l", "wall-t", "wall-r", "wall-b"], "bounds": [0, 40, 200, 160]},
        {"id": "legacy-roof", "kind": "roof", "spans": ["roof-l", "roof-r"], "bounds": [0, 0, 200, 40]}]}
    return observation, context, {"sourceId": observation["sourceId"]}


class ObjectTests(unittest.TestCase):
    def test_closed_triangle_and_orthogonal_cross_are_not_rectangles_but_open_u_is(self):
        def features(points):
            spans = {str(i): {"id": str(i), "controls": [a, b], "lineError": {"max": 0}}
                     for i, (a, b) in enumerate(zip(points, points[1:]))}
            return _features(list(spans), spans, 2)
        triangle = features([[0, 0], [80, 0], [80, 80], [0, 0]])
        self.assertTrue(triangle["closed"])
        self.assertFalse(triangle["rectangular"])
        self.assertEqual("closed-contour", triangle["shape"])
        cross = features([[30, 0], [50, 0], [50, 30], [80, 30], [80, 50], [50, 50],
                          [50, 80], [30, 80], [30, 50], [0, 50], [0, 30], [30, 30], [30, 0]])
        self.assertTrue(cross["closed"])
        self.assertFalse(cross["rectangular"])
        self.assertEqual("closed-contour", cross["shape"])
        open_u = features([[0, 80], [0, 0], [80, 0], [80, 80]])
        self.assertTrue(open_u["rectangular"])
        self.assertTrue(open_u["bottomOpen"])
        self.assertFalse(open_u["closed"])

    def test_each_roof_and_steeple_attachment_reads_only_its_own_parent(self):
        observed, context, annotations = fixture()
        context["parts"].append({"id": "alternative-body", "kind": "body",
                                 "spans": ["wall-l", "wall-r", "wall-b"], "bounds": [0, 80, 200, 160]})
        context["parts"].append({"id": "legacy-steeple", "kind": "steeple",
                                 "spans": ["roof-l", "roof-r"], "bounds": [0, 0, 200, 40]})
        enriched, summary = enrich_context(observed, context, annotations)
        parts = {p["id"]: p for p in enriched["parts"]}
        attachments = [r for r in enriched["relations"] if r["kind"] in ("roof-wall-attachment", "upper-part-attachment")]
        self.assertEqual(4, len(attachments))
        for relation in attachments:
            parent, child = [parts[key] for key in relation["participants"]]
            self.assertEqual(1, len(relation["comparisons"]))
            check = relation["comparisons"][0]
            self.assertEqual(child["bounds"][3]-parent["bounds"][1], check["actual"])
            request = Request("local", "attachment-candidate", (), (), (Comparison(**check),), "hypotheses", (), {}, (), {})
            self.assertEqual("none" if parent["bounds"][1] == 40 else "strong", heuristic(request).opposition)
        for candidate in summary["candidates"]:
            if candidate["role"] in ("roof", "steeple"):
                self.assertFalse(candidate["comparisons"])

    def test_wall_capacity_reports_an_omitted_qualifying_candidate(self):
        observed, context, annotations = fixture()
        _, complete = enrich_context(observed, context, annotations)
        wall_count = sum(c["role"] == "wall" for c in complete["candidates"])
        self.assertGreater(wall_count, 1)
        with patch.dict(POLICY, maximumWalls=1):
            _, bounded = enrich_context(observed, context, annotations)
        self.assertEqual(1, sum(c["role"] == "wall" for c in bounded["candidates"]))
        self.assertTrue(bounded["capacity"]["wallBudget"])
        self.assertTrue(bounded["capacityReached"])
        self.assertTrue(any(key.startswith("object-wall-") for key in bounded["withheldCandidateIds"]))
        with patch.dict(POLICY, maximumWalls=wall_count):
            _, exact = enrich_context(observed, context, annotations)
        self.assertFalse(exact["capacity"]["wallBudget"])

    def test_two_multistroke_windows_have_separate_ids_and_shared_parent(self):
        observed, context, annotations = fixture()
        enriched, result = enrich_context(observed, context, annotations)
        windows = [c for c in result["candidates"] if c["role"] == "window"]
        self.assertEqual(2, len(windows))
        self.assertNotEqual(windows[0]["instanceId"], windows[1]["instanceId"])
        self.assertEqual(set(windows[0]["parentIds"]), set(windows[1]["parentIds"]))
        self.assertEqual({"a1", "a2", "a3", "a4"}, set(next(c for c in windows if "a1" in c["spans"])["spans"]))
        self.assertTrue(any(r["kind"] == "repeated-part-candidates" for r in enriched["relations"]))

    def test_nesting_is_connected_and_building_wall_overlap_is_not_ambiguity(self):
        observed, context, annotations = fixture()
        enriched, result = enrich_context(observed, context, annotations)
        building = next(c for c in result["candidates"] if c["role"] == "building")
        wall = next(c for c in result["candidates"] if c["role"] == "wall")
        self.assertIn(building["id"], wall["parentIds"])
        self.assertIn(wall["id"], building["participants"])
        self.assertEqual("assembly", building["focusKind"])
        self.assertFalse(set(wall["spans"]) & set(result["ambiguousSpanIds"]))
        known = set()
        for part in enriched["parts"]:
            self.assertTrue(set(part.get("participants", [])) <= known)
            known.add(part["id"])
        for relation in enriched["relations"]:
            self.assertTrue(set(relation["participants"]) <= known)

    def test_competing_wall_support_stays_ambiguous_even_when_covered(self):
        observed, context, annotations = fixture()
        context["parts"].append({"id": "alternative-body", "kind": "body",
                                 "spans": ["wall-l", "wall-t", "a2"], "bounds": [0, 40, 55, 160]})
        _, result = enrich_context(observed, context, annotations)
        self.assertIn("wall-l", result["ambiguousSpanIds"])
        self.assertNotIn("wall-l", result["unknownSpanIds"])
        self.assertTrue(any(c["conflicts"] for c in result["candidates"] if c["role"] == "wall"))

    def test_uncovered_line_is_unknown_and_source_is_unchanged(self):
        observed, context, annotations = fixture()
        original = copy.deepcopy((observed, context, annotations))
        enriched, result = enrich_context(observed, context, annotations)
        self.assertEqual(original, (observed, context, annotations))
        self.assertIn("unknown", result["unknownSpanIds"])
        self.assertTrue(any(c["role"] == "unknown" and "unknown" in c["spans"] for c in result["candidates"]))
        result["candidates"][0]["bounds"][0] = -999
        self.assertNotEqual(-999, next(p for p in enriched["parts"] if p["id"].startswith("object-"))["bounds"][0])

    def test_ids_do_not_depend_on_stroke_or_candidate_enumeration(self):
        observed, context, annotations = fixture()
        _, first = enrich_context(observed, context, annotations)
        observed["strokes"].reverse(); observed["spans"].reverse(); context["parts"].reverse()
        _, second = enrich_context(observed, context, annotations)
        self.assertEqual({c["id"] for c in first["candidates"]}, {c["id"] for c in second["candidates"]})

    def test_upper_rectangle_retains_its_own_scope(self):
        observed, context, annotations = fixture()
        # An upper tower, drawn in separate strokes, must not be pooled into
        # the body frame or erase either set of source profile identities.
        for i, points in enumerate([[[80, 38], [80, 5]], [[80, 5], [120, 5]], [[120, 5], [120, 38]]]):
            sid = "tower-"+str(i)
            observed["spans"].append({"id": sid, "controls": points, "lineError": {"max": 0}})
            observed["strokes"].append({"id": sid, "spans": [sid]})
        _, result = enrich_context(observed, context, annotations)
        tower = next(c for c in result["candidates"] if c["role"] == "wall" and set(c["spans"]) == {"tower-0", "tower-1", "tower-2"})
        body = next(c for c in result["candidates"] if c["role"] == "wall" and "wall-l" in c["spans"])
        self.assertNotEqual(tower["instanceId"], body["instanceId"])
        self.assertEqual([80, 5, 120, 38], tower["bounds"])
        self.assertEqual([0, 40, 200, 160], body["bounds"])

    def test_wrong_source_and_duplicate_spans_are_rejected(self):
        observed, context, annotations = fixture()
        annotations["sourceId"] = "other"
        with self.assertRaisesRegex(ValueError, "identities"):
            enrich_context(observed, context, annotations)
        annotations["sourceId"] = observed["sourceId"]
        observed["spans"].append(copy.deepcopy(observed["spans"][0]))
        with self.assertRaisesRegex(ValueError, "unique"):
            enrich_context(observed, context, annotations)

    def test_upper_contour_can_propose_roof_without_legacy_roof_label(self):
        observed, context, annotations = fixture()
        context["parts"] = [p for p in context["parts"] if p["kind"] != "roof"]
        # Combine the upper slopes as one source stroke; the separate wall
        # strokes retain their own identities and the roof need not be closed.
        observed["strokes"].append({"id": "roof-acquisition", "spans": ["roof-l", "roof-r"]})
        enriched, result = enrich_context(observed, context, annotations)
        roof = next(c for c in result["candidates"] if c["role"] == "roof")
        self.assertEqual({"roof-l", "roof-r"}, set(roof["spans"]))
        self.assertTrue(roof["parentIds"])
        self.assertEqual(1, len(roof["comparisons"]))
        parts = {p["id"]: p for p in enriched["parts"]}
        for relation in enriched["relations"]:
            if relation["kind"] == "roof-wall-attachment":
                self.assertEqual(2, len(relation["comparisons"]))
                parent = parts[relation["participants"][0]]
                self.assertEqual(roof["bounds"][3]-parent["bounds"][1], relation["comparisons"][1]["actual"])

    def test_refresh_replaces_derived_candidates_without_duplicating_witnesses(self):
        observed, context, annotations = fixture()
        first, summary = enrich_context(observed, context, annotations)
        second, repeated = enrich_context(observed, first, annotations)
        self.assertEqual(summary, repeated)
        self.assertEqual(first, second)


if __name__ == "__main__":
    unittest.main()
