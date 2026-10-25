import json
from pathlib import Path
import re
import tempfile
import unittest
from unittest.mock import patch

from gojo_quickdraw.review import build


class ReviewContractTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.output = self.root / "review.html"
        self.row = {"itemId": "abc", "keyId": "new-key", "category": "church",
                    "evidencePath": "records/abc.evidence.json.gz", "nativePath": "native/abc.gojo.json.gz"}
        self.packet = {"sourceOriginalJson": '{"word":"</script><script>bad()</script>"}',
                       "observation": {"sourceId": "new-key", "spans": [], "strokes": []},
                       "annotations": {"lines": [], "joints": []},
                       "context": {"parts": [{"id": "wall", "status": "seeded-candidate"}]}}

    def test_portable_document_safely_embeds_exact_evidence(self):
        with patch("gojo_quickdraw.dataset.load_records", return_value=iter([(self.row, self.packet)])) as records:
            result = build(self.root, self.output, limit=1)
        records.assert_called_once_with(self.root.resolve(), limit=1, order="category", seed=0)
        html = self.output.read_text(encoding="utf-8")
        encoded = re.search(r'<script id="dataset" type="application/json">(.*?)</script>', html, re.S).group(1)
        embedded = json.loads(encoded)
        self.assertEqual(embedded["records"][0]["packet"], self.packet)
        self.assertNotIn("<script>bad()", html)
        self.assertNotIn("fetch(", html)
        self.assertNotIn('<script src=', html)
        self.assertTrue(result["portable"])
        self.assertIn("Seeded expectation", html)

    def test_review_cannot_overwrite_dataset_artifacts(self):
        for name in ("manifest.json", "index.ndjson", self.row["evidencePath"], self.row["nativePath"]):
            with self.subTest(name=name), patch("gojo_quickdraw.dataset.load_records", return_value=iter([(self.row, self.packet)])):
                with self.assertRaisesRegex(ValueError, "overwrite"):
                    build(self.root, self.root / name)

    def test_empty_dataset_and_invalid_limits_are_explained(self):
        with patch("gojo_quickdraw.dataset.load_records", return_value=iter([])):
            with self.assertRaisesRegex(ValueError, "No successfully encoded"):
                build(self.root, self.output)
        for limit in (0, -1, True, 2.5):
            with self.subTest(limit=limit), self.assertRaises(ValueError):
                build(self.root, self.output, limit)


if __name__ == "__main__":
    unittest.main()
