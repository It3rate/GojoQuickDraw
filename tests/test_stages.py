import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from gojo_quickdraw.stages import generate, _curve_extent, _diagnose_review


def review_data(controls=None):
    return {"introduction": "Seven stages.", "sources": [{"id": "fresh-key",
            "construction": {"id": "program", "rungs": [{"id": "r6", "visibleEdgeIds": ["e"]}],
                             "analysis": {"edges": [{"id": "e", "controls": controls or [[10, 20], [300, 400]]}]}},
            "passes": [{"metrics": {"sourceFitRms": 80, "previousPassRms": 120}}]}]}


def review_html(data):
    return '<html><div>Five source drawings</div><script id="review-data" type="application/json">' + json.dumps(data) + '</script></html>'


class StageContractTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.dataset = self.root / "baseline"
        self.dataset.mkdir()
        (self.dataset / "manifest.json").write_text('{"baseline":true}', encoding="utf-8")
        self.output = self.root / "later"
        self.gojo = self.root / "Gojo"
        self.tools = self.gojo / "experiments/quickDraw/tools"
        self.tools.mkdir(parents=True)
        for name in ("quickdraw_gestalt.py", "inspect_quickdraw_gestalt.py"):
            (self.tools / name).write_text("# Test adapter", encoding="utf-8")
        self.row = {"itemId": "abc", "keyId": "fresh-key", "category": "church", "evidenceSha256": "123"}
        self.original = '{ "key_id":"fresh-key", "word":"church", "drawing":[] }'
        self.packet = {"sourceOriginalJson": self.original, "source": json.loads(self.original),
                       "observation": {"sourceId": "fresh-key", "spans": [{"id": "a"}]}}
        self.load = patch("gojo_quickdraw.dataset.load_records", return_value=iter([(self.row, self.packet)]))
        self.loader = self.load.start()
        self.addCleanup(self.load.stop)
        self.fingerprint = patch("gojo_quickdraw.upstream.fingerprint", return_value={"revision": "test"})
        self.fingerprint.start()
        self.addCleanup(self.fingerprint.stop)

    def script(self, script, *args):
        args = dict(zip(args[::2], args[1::2])) if args[0] != "--synthesize" else dict(zip(args[1::2], args[2::2]))
        output = Path(args["--output"])
        if script.name == "quickdraw_gestalt.py":
            output.mkdir()
            self.assertEqual(Path(args["--source"]).read_text(encoding="utf-8"), self.original + "\n")
            (output / "manifest.json").write_text(json.dumps({"sources": [{"passes": [{}] * 7}]}), encoding="utf-8")
            for name in ("programs.ndjson", "packets.ndjson", "observations.ndjson"):
                (output / name).write_text("{}\n", encoding="utf-8")
        else:
            output.parent.mkdir()
            output.write_text(review_html(review_data()), encoding="utf-8")

    def test_unbounded_synthesis_is_never_implicit(self):
        with self.assertRaisesRegex(ValueError, "explicit limit"):
            generate(self.dataset, self.output, self.gojo)
        self.loader.assert_not_called()

    def test_selection_and_success_preserve_baseline_and_source(self):
        before = (self.dataset / "manifest.json").read_bytes()
        with patch("gojo_quickdraw.stages._script", side_effect=self.script), patch(
                "gojo_quickdraw.bridge.run", return_value={"message": "verified"}) as native:
            result = generate(self.dataset, self.output, self.gojo, key_id="fresh-key")
        self.loader.assert_called_once_with(self.dataset.resolve(), key_id="fresh-key", limit=None)
        self.assertEqual(result["passes"], 7)
        self.assertEqual([call.args[1] for call in native.call_args_list], ["construct", "gestalt-evidence"])
        self.assertTrue((self.output / "review/index.html").is_file())
        self.assertEqual((self.dataset / "manifest.json").read_bytes(), before)
        provenance = json.loads((self.output / "dataset-provenance.json").read_text(encoding="utf-8"))
        self.assertEqual(provenance["selected"][0]["keyId"], "fresh-key")
        self.assertEqual(provenance["nativeConstruction"]["output"], "native")
        self.assertEqual(result["warnings"], [])
        self.assertTrue((self.output / "diagnostics.json").is_file())
        self.assertNotIn("Five source drawings", (self.output / "review/index.html").read_text())
        self.assertEqual(list(self.root.glob(".quickdraw-stages-*")), [])

    def test_native_rejection_does_not_publish_a_partial_run(self):
        with patch("gojo_quickdraw.stages._script", side_effect=self.script), patch(
                "gojo_quickdraw.bridge.run", side_effect=RuntimeError("invalid native references")):
            with self.assertRaisesRegex(RuntimeError, "invalid native"):
                generate(self.dataset, self.output, self.gojo, limit=1)
        self.assertFalse(self.output.exists())
        self.assertEqual(list(self.root.glob(".quickdraw-stages-*")), [])

    def test_different_category_is_not_given_church_priors(self):
        self.row["category"] = "frog"
        with self.assertRaisesRegex(ValueError, "church only"):
            generate(self.dataset, self.output, self.gojo, limit=1)
        self.assertFalse(self.output.exists())

    def test_no_overwrite_of_dataset_or_existing_run(self):
        for destination in (self.dataset, self.dataset / "stages", self.root):
            with self.subTest(destination=destination), self.assertRaises(ValueError):
                generate(self.dataset, destination, self.gojo, limit=1)
        self.output.mkdir()
        (self.output / "keep.txt").write_text("Keep existing run")
        with self.assertRaisesRegex(ValueError, "empty directory"):
            generate(self.dataset, self.output, self.gojo, limit=1)
        self.assertEqual((self.output / "keep.txt").read_text(), "Keep existing run")

    def test_invalid_limit_rejected(self):
        for value in (0, -1, True, 1.5):
            with self.subTest(value=value), self.assertRaises(ValueError):
                generate(self.dataset, self.output, self.gojo, limit=value)

    def test_empty_observation_does_not_invent_a_church(self):
        self.packet["observation"].update(emptyRead=True, spans=[])
        with self.assertRaisesRegex(ValueError, "no fitted intervals"):
            generate(self.dataset, self.output, self.gojo, limit=1)

    def test_curve_extent_uses_curve_not_control_polygon(self):
        extent = _curve_extent([[256, 200], [856, 200], [-344, 200], [256, 200]])
        self.assertGreater(extent[0], 0)
        self.assertLess(extent[2], 512)
        self.assertAlmostEqual(extent[0], 256 - 100 * (3 ** .5))
        self.assertAlmostEqual(extent[2], 256 + 100 * (3 ** .5))

    def test_outside_geometry_flagged_without_rejecting_deliberate_drift(self):
        html = self.root / "review.html"
        data = review_data([[10, 20], [600, 400]])
        html.write_text(review_html(data), encoding="utf-8")
        diagnostics = _diagnose_review(html, self.root / "diagnostics.json")
        row = diagnostics["sources"][0]
        self.assertTrue(row["outOfFrame"])
        self.assertEqual(row["overflow"]["right"], 88)
        self.assertEqual(row["extentStatus"], "review-needed")
        self.assertEqual(row["reportedFitMetrics"]["sourceFitRms"], 80)
        self.assertEqual(row["reportedFitMetrics"]["previousPassRms"], 120)
        self.assertIn("may be clipped", html.read_text(encoding="utf-8"))
        self.assertEqual(data["sources"][0]["construction"]["analysis"]["edges"][0]["controls"], [[10, 20], [600, 400]])

    def test_bent_controls_outside_frame_do_not_trigger_false_warning(self):
        html = self.root / "review.html"
        html.write_text(review_html(review_data([[256, 200], [856, 200], [-344, 200], [256, 200]])), encoding="utf-8")
        diagnostics = _diagnose_review(html, self.root / "diagnostics.json")
        self.assertEqual(diagnostics["warnings"], [])
        self.assertEqual(diagnostics["sources"][0]["qualityStatus"], "authored-requires-review")


if __name__ == "__main__":
    unittest.main()
