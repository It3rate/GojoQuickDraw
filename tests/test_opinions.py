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

    def object_packet(self):
        packet = self.packet()
        spans = [span["id"] for span in packet["observation"]["spans"]]
        geometry = {"points": [[0, 0], [80, 0], [80, 120], [0, 120]],
                    "segments": [[0, 1], [1, 2], [2, 3], [3, 0]]}
        check = {"actual": 2, "expected": 0, "allowance": 4, "basis": "normalized page distance"}
        packet["context"] = {"sourceId": packet["observation"]["sourceId"], "parts": [
            {"id": "object-wall", "kind": "wall", "role": "wall", "spans": spans,
             "bounds": [0, 0, 80, 120], "geometry": geometry, "comparisons": [check]},
            {"id": "object-building", "kind": "building", "role": "building", "spans": spans,
             "participants": ["object-wall"], "bounds": [0, 0, 80, 120], "comparisons": [check]},
            {"id": "object-window", "kind": "window", "role": "window", "spans": [spans[0]],
             "bounds": [20, 30, 40, 70], "status": "ambiguous-candidate", "conflicts": ["object-door"], "comparisons": [check]},
            {"id": "object-door", "kind": "door", "role": "door", "spans": [spans[0]],
             "bounds": [20, 30, 40, 70], "status": "ambiguous-candidate", "conflicts": ["object-window"], "comparisons": [check]},
            {"id": "object-unknown", "kind": "unknown", "role": "unknown", "spans": [spans[-1]],
             "bounds": [0, 0, 80, 40], "status": "unresolved"}],
            "relations": [{"id": "contains", "kind": "containment", "participants": ["object-wall", "object-window"],
                           "comparisons": [{**check, "actual": 12}]}]}
        return packet

    def test_object_opinions_connect_exact_parts_nested_memberships_and_relations(self):
        packet = self.object_packet(); before = copy.deepcopy(packet)
        result = build(packet); rows = {row["id"]: row for row in result["claims"]}
        building = rows["part-scope-object-building"]
        self.assertEqual(["object-building"], building["contextParts"])
        self.assertEqual(["part-scope-object-wall"], building["dependencies"])
        self.assertEqual(rows["part-scope-object-wall"]["spans"], building["spans"])
        relation = rows["context-contains"]
        self.assertEqual(["object-wall", "object-window"], relation["contextParts"])
        self.assertEqual("strong", relation["opposition"])
        self.assertEqual(12, relation["comparisons"][0]["actual"])
        self.assertEqual(relation["contextParts"], rows["carry-contains"]["contextParts"])
        self.assertEqual(packet, before)

    def test_object_role_ambiguity_and_unknowns_stay_opposed_without_feedback(self):
        result = build(self.object_packet()); seen = set()
        for row in result["claims"]:
            self.assertTrue(set(row["dependencies"]) <= seen)
            seen.add(row["id"])
        rows = {row["id"]: row for row in result["claims"]}
        window = rows["part-role-object-window"]
        self.assertEqual(["object-window", "object-door"], window["contextParts"])
        self.assertEqual("medium", window["support"])
        self.assertEqual("weak", window["opposition"])
        self.assertEqual("conflicted", window["status"])
        self.assertTrue(all(key.startswith("part-scope-") for key in window["dependencies"]))
        unknown = rows["part-role-object-unknown"]
        self.assertEqual("none", unknown["support"])
        self.assertEqual("weak", unknown["opposition"])
        self.assertFalse(result["learned"])

    def test_provider_gets_readonly_part_details_without_mutating_source(self):
        packet = self.object_packet(); before = copy.deepcopy(packet); requests = []
        def provider(request):
            requests.append(request)
            self.assertEqual(request.context_parts[0], request.parts[0]["id"])
            with self.assertRaises(TypeError):
                request.parts[0]["role"] = "certain"
            with self.assertRaises(TypeError):
                request.parts[0]["bounds"][0] = 999
            return Response("weak", "none", "read-only-provider", "No source edits")
        build(packet, providers={"part-membership-candidate": provider})
        self.assertEqual(len(packet["context"]["parts"]), len(requests))
        self.assertEqual(packet, before)
        packet["context"]["parts"][0]["bounds"][0] = -100
        self.assertEqual(0, requests[0].parts[0]["bounds"][0])
        building = next(request for request in requests if request.context_parts == ("object-building",))
        self.assertEqual(["object-wall"], building.earlier[0]["contextParts"])

    def test_missing_or_forward_objects_are_not_turned_into_unconnected_labels(self):
        packet = self.object_packet()
        packet["context"]["parts"][0]["participants"] = ["object-building"]
        with self.assertRaisesRegex(ValueError, "forward"):
            build(packet)
        packet = self.object_packet()
        packet["context"]["parts"][2]["conflicts"] = ["absent-alternative"]
        with self.assertRaisesRegex(ValueError, "missing competing"):
            build(packet)

    def test_roof_attachments_use_explicit_local_participants_and_separation_deduplicates_ink(self):
        packet = self.object_packet(); context = packet["context"]
        source_ids = [span["id"] for span in packet["observation"]["spans"]]
        context["parts"].insert(0, {"id": "legacy-body", "kind": "body", "spans": source_ids,
                                     "bounds": [-200, -200, 200, 200]})
        context["parts"].append({"id": "object-roof", "kind": "roof", "role": "roof", "spans": [source_ids[-1]],
                                 "bounds": [0, -40, 80, 0], "participants": ["object-wall"]})
        context["relations"].append({"id": "local-roof-wall", "kind": "roof-wall-attachment",
                                     "participants": ["object-wall", "object-roof"], "spans": [source_ids[-1]]})
        new_window = {"id": "object-window-b", "kind": "window", "spans": [source_ids[-1]], "bounds": [50, 30, 70, 70]}
        context["parts"].append(new_window)
        context["parts"].append({**new_window, "id": "legacy-window-b"})
        result = build(packet)
        attachments = [row for row in result["claims"] if row["kind"].startswith("roof-wall-attachment")]
        self.assertEqual(1, len(attachments))
        self.assertEqual(["object-wall", "object-roof"], attachments[0]["contextParts"])
        separations = [row for row in result["claims"] if row["kind"] == "separate-part-instances"]
        self.assertEqual(1, len(separations))
        self.assertTrue(all(part.startswith("object-") for part in separations[0]["contextParts"]))

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
