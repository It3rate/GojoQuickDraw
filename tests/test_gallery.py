import copy
import json
from pathlib import Path
import re
import tempfile
import unittest
from unittest.mock import patch
from urllib.parse import parse_qs, unquote, urlsplit

from gojo_quickdraw import dataset, gallery, review, selection
from test_dataset import FakeEncoder, GOJO, SOURCE


def embedded(path, element="gallery-data"):
    return json.loads(re.search(r'<script id="' + element + r'" type="application/json">(.*?)</script>',
                                path.read_text(encoding="utf-8"), re.S).group(1))


class GalleryTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.dataset = self.root / "dataset"
        self.output = self.root / "inspector/comparison.html"
        self.inspector = self.output.parent / "evidence.html"
        self.source = self.root / "church.ndjson"
        self.source.write_text("".join(json.dumps(dict(SOURCE, key_id=f"sample-{i:02}")) + "\n" for i in range(26)), encoding="utf-8")
        for name, value in (("fingerprint", {"revision": "test", "sourceSha256": "test"}), ("_pipeline_fingerprint", "test")):
            mock = patch.object(dataset, name, return_value=value)
            mock.start(); self.addCleanup(mock.stop)
        original = dataset.build_packet

        def packet(*args, **kwargs):
            result = original(*args, **kwargs)
            if result["source"]["key_id"] != "sample-00":
                result["analysis"] = {"proposalNativeExecuted": False, "proposal": {"steps": [
                    {"id": "rough", "title": "Rough", "edges": [{"id": "rough-edge", "controls": [[0, 0], [20, 10]]}]},
                    {"id": "final", "title": "Synthesized", "edges": [{"id": "final-edge", "controls": [[-40, 450], [256, -600], [552, 450]]}],
                     "residueEdges": [{"id": "retained", "controls": [[100, 110], [120, 140]]}]}]}}
            return result

        with patch.object(dataset, "build_packet", side_effect=packet):
            dataset.convert(self.source, self.dataset, GOJO, encoder_factory=FakeEncoder)

    def test_all_inputs_are_paged_once_with_the_actual_last_interpretation(self):
        review.build(self.dataset, self.inspector, 5, paged=True, order="random", seed=19)
        before = {p: p.read_bytes() for p in self.dataset.rglob("*.gz")}
        result = gallery.build(self.dataset, self.output)
        self.assertEqual((26, 25, 3, 12, 26),
                         tuple(result[k] for k in ("drawings", "finals", "pages", "pageSize", "linkedDrawings")))
        first = embedded(self.output)
        sizes, keys = [], []
        expected = selection.catalog(self.dataset, 26)["records"]
        for relative in first["pagination"]["pages"]:
            path = self.output.parent / unquote(relative)
            data = embedded(path)
            sizes.append(len(data["records"]))
            html = path.read_text(encoding="utf-8")
            self.assertNotIn('id="dataset"', html, "Full packets should not be loaded for thumbnails")
            self.assertNotIn("rough-edge", html)
            for row in data["records"]:
                keys.append(row["selectionId"])
                url = urlsplit(row["detailsHref"])
                target = (path.parent / unquote(url.path)).resolve()
                detail = embedded(target, "dataset")
                self.assertIn(row["itemId"], [r["index"]["itemId"] for r in detail["records"]])
                self.assertEqual(row["itemId"], unquote(url.fragment))
                self.assertEqual(["step:1" if row["finalStepId"] else "observed-fit"], parse_qs(url.query)["view"])
                if row["finalStepId"]:
                    self.assertEqual((1, 1), (row["finalEdges"], row["residueEdges"]))
                    x, y, w, h = row["viewBox"]
                    self.assertLess(x, -40); self.assertLess(y, -75)
                    self.assertGreater(x + w, 552); self.assertGreater(y + h, 512)
            self.assertNotIn("fetch(", html)
        self.assertEqual([12, 12, 2], sizes)
        self.assertEqual([r["selectionId"] for r in expected], keys)
        self.assertTrue(all(p.read_bytes() == content for p, content in before.items()))
        self.assertIn("No final interpretation yet", self.output.read_text(encoding="utf-8"))

    def test_missing_or_stale_inspector_is_not_linked_to_an_unrelated_drawing(self):
        self.assertEqual(0, gallery.build(self.dataset, self.output)["linkedDrawings"])
        review.build(self.dataset, self.inspector, 5, paged=True)
        manifest = self.inspector.with_suffix(".pages.json")
        data = json.loads(manifest.read_text(encoding="utf-8"))
        data["journals"][0][1] += 1
        manifest.write_text(json.dumps(data), encoding="utf-8")
        self.assertEqual(0, gallery.build(self.dataset, self.output)["linkedDrawings"])

    def test_labels_are_escaped_and_unavailable_results_are_explicit(self):
        row, packet = next(dataset.load_records(self.dataset))
        row = dict(row, category='</script><img src="bad" onerror="bad()">')
        packet = copy.deepcopy(packet)
        card, meta = gallery._card(row, packet, 1, {}, self.output.parent)
        self.assertNotIn("<img", card)
        self.assertIn("&lt;img", card)
        self.assertIn("No final interpretation yet", card)
        self.assertIsNone(meta["finalStepId"])

    def test_destinations_and_page_capacity_are_validated(self):
        for size in (0, -1, True, 1.5):
            with self.subTest(size=size), self.assertRaises(ValueError):
                gallery.build(self.dataset, self.output, size)
        for path in (self.dataset / "manifest.json", self.dataset / "gallery.html", self.inspector,
                     self.root / "wrong.json"):
            with self.subTest(path=path), self.assertRaises(ValueError):
                gallery.build(self.dataset, path, inspector=self.inspector)

    def test_empty_acquisition_stays_reviewable_without_invented_area(self):
        row, packet = next(dataset.load_records(self.dataset))
        packet = copy.deepcopy(packet)
        packet['observation']['strokes'] = []
        packet['observation']['spans'] = []
        card, meta = gallery._card(row, packet, 1, {}, self.output.parent)
        self.assertEqual(0, meta['layoutParts'])
        self.assertEqual(0, meta['sourceStrokes'])
        self.assertIn('No area estimate.', card)
        self.assertNotIn('data-layout=', card)


if __name__ == "__main__":
    unittest.main()
