import copy
import json
import unittest

from gojo_quickdraw.dataset import build_packet
from gojo_quickdraw.opinions import build, Response
from gojo_quickdraw.analysis import enrich
from test_dataset import GOJO


class OpinionTests(unittest.TestCase):
    def packet(self):
        # A gapped rectangle, plus an upper peak. Actual time order is retained,
        # even when it is not monotonic. The hypotheses cannot repair the source.
        source = {"key_id": "opinions", "word": "church", "drawing": [
            [[0, 80, 80, 0, 0], [40, 40, 120, 120, 43], [9, 7, 8, 6, 5]],
            [[0, 40, 80], [40, 0, 40], [10, 11, 12]]]}
        return build_packet(json.dumps(source), GOJO, "test")

    def test_shape_exposes_dependent_corner_expectations_without_joining_ink(self):
        packet = self.packet(); before = copy.deepcopy(packet)
        result = build(packet)
        self.assertEqual(packet, before)
        rows = result["claims"]; seen = set()
        for row in rows:
            self.assertTrue(set(row["dependencies"]) <= seen)
            seen.add(row["id"])
        rectangle = next(row for row in rows if row["kind"] == "rectangle-candidate")
        corners = [r for r in rows if r["kind"] == "expected-L-corner" and rectangle["id"] in r["dependencies"]]
        self.assertEqual(4, len(corners))
        self.assertTrue(all(c["context"] == "hypotheses" for c in corners))
        self.assertTrue(any(len(c["dependencies"]) > 1 for c in corners))
        self.assertEqual(3, packet["source"]["drawing"][0][1][-1] - packet["source"]["drawing"][0][1][0])

    def test_provider_can_supply_a_contrary_opinion_through_the_same_contract(self):
        requests = []
        def small_model(request):
            requests.append(request)
            return Response("weak", "strong", "test-model.v1", "Contrary test opinion")
        result = build(self.packet(), providers={"join-candidate": small_model})
        joins = [row for row in result["claims"] if row["kind"] == "join-candidate"]
        self.assertTrue(requests)
        self.assertTrue(all(row["provider"] == "test-model.v1" and row["status"] == "conflicted" for row in joins))
        self.assertTrue(all(request.spans and request.comparisons for request in requests))
        self.assertTrue(all(request.profiles and request.profiles[0]["controls"] for request in requests))

    def test_invalid_grades_and_capacity_do_not_silently_invent_evidence(self):
        with self.assertRaisesRegex(ValueError, "ordinal"):
            build(self.packet(), providers={"join-candidate": lambda _: Response("certain", "none", "bad", "bad")})
        result = build(self.packet(), maximum=2)
        self.assertEqual(2, len(result["claims"]))
        self.assertTrue(result["capacityReached"])
        self.assertTrue(result["withheldClaimIds"])

    def test_full_analysis_keeps_timing_source_and_seven_proposals_separate(self):
        packet = self.packet(); before = copy.deepcopy(packet)
        result = enrich(packet, "church", GOJO)
        self.assertEqual(packet, before)
        self.assertEqual(packet["sourceOriginalJson"], result["sourceOriginalJson"])
        self.assertEqual([9, 7, 8, 6, 5], result["observation"]["strokes"][0]["times"])
        self.assertEqual(3, len(result["analysis"]["annotationModes"]))
        self.assertEqual(7, len(result["analysis"]["proposal"]["steps"]))
        self.assertFalse(result["analysis"]["proposalNativeExecuted"])
        self.assertFalse(result["opinions"]["learned"])

    def test_other_categories_and_empty_acquisitions_get_no_church_template(self):
        result = enrich(self.packet(), "frog", GOJO)
        self.assertIsNone(result["analysis"]["proposal"])
        self.assertEqual([], result["context"]["parts"])
        empty = build_packet('{"key_id":"empty","drawing":[[[],[]]]}', GOJO, "empty")
        result = enrich(empty, "church", GOJO)
        self.assertIsNone(result["analysis"]["proposal"])
        self.assertEqual([], result["opinions"]["claims"])


if __name__ == "__main__":
    unittest.main()
