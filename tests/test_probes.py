import copy
import unittest

from gojo_quickdraw.probes import example_context, run_probes, move_contained


class ProbeTests(unittest.TestCase):
    def test_child_edit_is_independent_and_container_edit_carries_descendants_once(self):
        context = example_context(); before = copy.deepcopy(context)
        context["parts"].append({"id": "pane", "bounds": [16, 26, 24, 44]})
        context["relations"].extend([
            {"id": "pane-in-window", "kind": "containment", "participants": ["left-window", "pane"]},
            {"id": "pane-in-wall", "kind": "containment", "participants": ["wall", "pane"]}])
        original = copy.deepcopy(context)
        child = move_contained(context, "right-window", 5, carry=False)
        self.assertEqual(before["parts"][0]["bounds"], child["boundsAfter"]["wall"])
        self.assertEqual(["right-window"], child["movedParticipants"])
        parent = move_contained(context, "wall", 5)
        self.assertEqual([21, 26, 29, 44], parent["boundsAfter"]["pane"])
        self.assertEqual(5, len(parent["movedParticipants"]))
        self.assertTrue(parent["unresolved"])
        self.assertEqual(context, original)

    def test_container_movement_default_preserves_existing_containment_residue(self):
        context = example_context()
        context["parts"][2]["bounds"] = [95, 25, 105, 45]
        move = move_contained(context, "wall", 12)
        self.assertEqual([0, 5, 0], [r["residue"] for r in move["reads"]])
    def test_pair_common_intervention_and_unidentifiable_tie(self):
        result = run_probes(example_context(), delta=5)
        probe = next(row for row in result["probes"] if row["kind"] == "paired-intervention")
        variants = {row["machine"]: row for row in probe["variants"]}
        mirror = variants["mirror-around-fixed-axis"]
        centered = variants["translation-around-fixed-center"]
        anchored = variants["translation-from-fixed-first"]
        self.assertEqual(mirror["boundsAfter"], centered["boundsAfter"])
        self.assertGreater(anchored["sumDistanceResidue"], mirror["sumDistanceResidue"])
        self.assertEqual(mirror["centerDisplacements"]["left-window"], [-5., 0.])
        for variant in variants.values():
            self.assertEqual(variant["centerDisplacements"]["right-window"], [5., 0.])
        self.assertTrue(any(set(row["machines"]) == {"mirror-around-fixed-axis", "translation-around-fixed-center"} for row in probe["ties"]))

    def test_existing_asymmetry_is_not_silently_repaired(self):
        context = example_context()
        context["parts"][2]["bounds"] = [71., 28., 81., 48.]
        probe = next(row for row in run_probes(context, 3)["probes"] if row["kind"] == "paired-intervention")
        mirror = next(row for row in probe["variants"] if row["machine"] == "mirror-around-fixed-axis")
        self.assertEqual([read["residue"] for read in probe["readsBefore"][:2]], [2., 3.])
        self.assertEqual([read["residue"] for read in mirror["reads"][:2]], [2., 3.])

    def test_one_sided_motion_can_improve_an_offset_pair(self):
        context = example_context()
        context["parts"][2]["bounds"] = [71., 25., 81., 45.]
        probe = next(row for row in run_probes(context, 4)["probes"] if row["kind"] == "paired-intervention")
        variants = {row["machine"]: row for row in probe["variants"]}
        self.assertLess(variants["translation-from-fixed-first"]["sumDistanceResidue"],
                        variants["mirror-around-fixed-axis"]["sumDistanceResidue"])

    def test_bidirectional_containment_from_same_requested_state(self):
        context = example_context()
        probe = next(row for row in run_probes(context, 30)["probes"] if row["relationId"] == "inside-right-window")
        variants = {row["machine"]: row for row in probe["variants"]}
        self.assertEqual(variants["retain-conflict"]["sumDistanceResidue"], 15.)
        for name in ("move-child", "move-container", "resize-container"):
            self.assertEqual(variants[name]["sumDistanceResidue"], 0.)
        self.assertEqual(variants["move-child"]["boundsAfter"]["wall"], probe["boundsBefore"]["wall"])
        self.assertEqual(variants["move-container"]["boundsAfter"]["right-window"], probe["boundsRequested"]["right-window"])
        self.assertEqual(variants["resize-container"]["boundsAfter"]["wall"][2], 115.)

    def test_zero_probe_and_no_input_mutation(self):
        context = example_context()
        original = copy.deepcopy(context)
        result = run_probes(context, 0)
        self.assertEqual(context, original)
        pair = next(row for row in result["probes"] if row["kind"] == "paired-intervention")
        self.assertTrue(all(row["centerDisplacements"] == {} for row in pair["variants"]))

    def test_source_free_contract(self):
        # This context deliberately contains no drawing, normalized source points
        # or fit spans. Public coarse bounds suffice for every advertised probe.
        context = example_context()
        result = run_probes(context)
        self.assertEqual(len(result["probes"]), 4)
        self.assertTrue(all(not probe["sourceSpanIds"] for probe in result["probes"]))

    def test_too_large_child_exposes_unavailable_translation(self):
        context = example_context()
        context["parts"][2]["bounds"] = [-20., 20., 120., 40.]
        probe = next(row for row in run_probes(context, 10)["probes"] if row["relationId"] == "inside-right-window")
        self.assertTrue(probe["unavailableResponses"])
        self.assertNotIn("move-child", {row["machine"] for row in probe["variants"]})
        self.assertNotIn("move-container", {row["machine"] for row in probe["variants"]})

    def test_nonfinite_intervention_rejected(self):
        for value in (float("inf"), float("nan"), "5"):
            with self.assertRaises(ValueError):
                run_probes(example_context(), value)


if __name__ == "__main__":
    unittest.main()
