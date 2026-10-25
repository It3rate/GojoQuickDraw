import json
from pathlib import Path
import unittest
from unittest.mock import patch

from gojo_quickdraw import dataset, refresh
from test_dataset import FakeEncoder, GOJO, SOURCE
import test_gallery


class RefreshTests(unittest.TestCase):
    setUp = test_gallery.GalleryTests.setUp

    def test_refresh_updates_both_stable_views_and_links_after_more_records(self):
        before = {p: p.read_bytes() for p in self.dataset.rglob("*.gz")}
        first = refresh.run(self.dataset, review_page_size=5, gallery_page_size=12, order="random", seed=43)
        self.assertEqual(26, first["drawings"])
        self.assertEqual(26, first["gallery"]["linkedDrawings"])
        self.assertEqual((6, 3), (first["review"]["pages"], first["gallery"]["pages"]))
        self.assertTrue(all(p.read_bytes() == data for p, data in before.items()))
        with self.source.open("a", encoding="utf-8") as stream:
            stream.write(json.dumps(dict(SOURCE, key_id="new-record")) + "\n")
        dataset.convert(self.source, self.dataset, GOJO, encoder_factory=FakeEncoder)
        latest = refresh.run(self.dataset, review_page_size=5, gallery_page_size=12, order="random", seed=43)
        self.assertEqual(first["inspector"], latest["inspector"])
        self.assertEqual(first["comparison"], latest["comparison"])
        self.assertEqual((27, 27), (latest["drawings"], latest["gallery"]["linkedDrawings"]))
        entry = test_gallery.embedded(Path(latest["comparison"]))
        self.assertEqual(27, entry["pagination"]["total"])
        self.assertTrue(all(p.read_bytes() == data for p, data in before.items()))

    def test_refresh_refuses_a_writer_changing_the_collection_between_views(self):
        stamps = refresh.review.collection_stamp(self.dataset)
        original = refresh.review.build

        def change_after_review(*args, **kwargs):
            result = original(*args, **kwargs)
            stamp_reader.return_value = stamps + [["changed", 1, 1]]
            return result

        # Patch the reader only after the detailed review succeeds, so the
        # guard tests the handoff between the two independently generated views.
        with patch.object(refresh.review, "collection_stamp", wraps=refresh.review.collection_stamp) as stamp_reader, \
                patch.object(refresh.review, "build", side_effect=change_after_review), \
                patch.object(refresh.gallery, "build") as gallery_build:
            with self.assertRaisesRegex(ValueError, "collection changed"):
                refresh.run(self.dataset, self.output.parent)
            gallery_build.assert_not_called()
        self.assertFalse((self.output.parent / "refresh.json").exists())

    def test_invalid_output_or_capacity_does_not_write_into_source(self):
        with self.assertRaisesRegex(ValueError, "outside"):
            refresh.run(self.dataset, self.dataset / "inspector")
        for size in (0, -1, True, 1.5):
            with self.subTest(size=size), self.assertRaises(ValueError):
                refresh.run(self.dataset, review_page_size=size)


if __name__ == "__main__":
    unittest.main()
