import gzip
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from gojo_quickdraw import dataset, sampling, selection
from test_dataset import FakeEncoder, GOJO, SOURCE


class CollectionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.input = self.root / "sources"
        self.input.mkdir()
        self.output = self.root / "generated"
        self.cache = self.root / "indexes"
        for category in ("angel", "church", "frog"):
            (self.input / (category + ".ndjson")).write_text("".join(json.dumps(dict(SOURCE, key_id=f"{category}-{i}", word=category)) + "\n" for i in range(20)), encoding="utf-8")
        for name, value in (("fingerprint", {"revision": "test", "sourceSha256": "test"}), ("_pipeline_fingerprint", "test")):
            mock = patch.object(dataset, name, return_value=value)
            mock.start(); self.addCleanup(mock.stop)
        FakeEncoder.calls = 0

    def convert(self, **kwargs):
        return sampling.convert_collection(self.input, self.output, GOJO, cache=self.cache, encoder_factory=FakeEncoder, **kwargs)

    def test_static_catalog_is_unique_and_does_not_include_alternate_raw_copies(self):
        names = sampling.category_names()
        self.assertEqual(345, len(names))
        self.assertEqual(len(names), len({name.casefold() for name in names}))
        self.assertFalse(any(name.startswith("full_raw_") for name in names))

    def test_total_is_balanced_random_without_replacement_and_preserves_source_location(self):
        result = self.convert(categories=["angel", "church", "frog"], count=8, seed=42)
        self.assertEqual(8, result["converted"])
        folders = selection.datasets(self.output)
        self.assertEqual({"angel", "church", "frog"}, {folder.name for folder, _ in folders})
        counts = [manifest["lastRun"]["storedRecords"] for _, manifest in folders]
        self.assertEqual([2, 3, 3], sorted(counts))
        rows = list(dataset.load_records(self.output))
        self.assertEqual(8, len({row["keyId"] for row, _ in rows}))
        for row, packet in rows:
            self.assertEqual(str(self.input / (row["category"] + ".ndjson")), row["sourceFile"])
            self.assertEqual(row["sourceLine"], packet["provenance"]["sourceLine"])
            self.assertEqual("input-filename", packet["categoryContext"]["basis"])
        self.assertTrue(any(row["sourceLine"] > 8 for row, _ in rows))
        before = {p: p.read_bytes() for p in self.output.rglob("*.gz")}
        repeated = self.convert(categories=["angel", "church", "frog"], count=8, seed=42)
        self.assertEqual((0, 8), (repeated["converted"], repeated["resumed"]))
        self.assertTrue(all(p.read_bytes() == content for p, content in before.items()))

    def test_file_selection_can_sample_sibling_category_files_and_ignores_alternates(self):
        alternate = self.input / "full_raw_church.ndjson"
        alternate.write_text("alternate copy", encoding="utf-8")
        files = sampling.source_files(self.input / "angel.ndjson", ["Angel", "church"])
        self.assertEqual({"angel", "church"}, set(files))
        self.assertEqual(self.input / "church.ndjson", files["church"])
        with self.assertRaisesRegex(ValueError, "at least one"):
            sampling.source_files(self.input, [])
        with self.assertRaisesRegex(ValueError, "not in the input"):
            sampling.source_files(self.input, ["moon"])

    def test_saved_selection_resumes_after_interrupted_category_conversion(self):
        convert = dataset.convert
        calls = 0
        def interrupted(*args, **kwargs):
            nonlocal calls
            calls += 1
            if calls == 2:
                raise OSError("interrupted between categories")
            return convert(*args, **kwargs)
        with patch.object(dataset, "convert", side_effect=interrupted), self.assertRaisesRegex(OSError, "interrupted"):
            self.convert(categories=["angel", "church"], count=6)
        seed = json.loads((self.output / "_sampling/active.json").read_text())["plan"]["seed"]
        result = self.convert(categories=["angel", "church"], count=6)
        self.assertEqual(seed, result["seed"])
        self.assertEqual((3, 3), (result["converted"], result["resumed"]))

    def test_gzip_blank_lines_and_original_line_numbers(self):
        source = self.input / "moon.ndjson.gz"
        with gzip.open(source, "wt", encoding="utf-8") as stream:
            stream.write("\n" + json.dumps(dict(SOURCE, key_id="moon", word="moon")) + "\n\n")
        plan = sampling.make_plan({"moon": source}, 4, 1, self.cache)
        self.assertEqual(1, plan["selected"])
        original = list(sampling.read_group(plan["groups"][0]))
        self.assertEqual(2, original[0][1])
        self.assertEqual("moon", json.loads(original[0][2])["key_id"])

    def test_index_corruption_rebuilds_and_source_changes_invalidate_saved_positions(self):
        source = self.input / "church.ndjson"
        first = sampling.index_source(source, self.cache)
        Path(first["locations"]).write_bytes(b"damaged")
        self.assertEqual(20, sampling.index_source(source, self.cache)["count"])
        plan = sampling.make_plan({"church": source}, 2, 4, self.cache)
        with source.open("a", encoding="utf-8") as stream:
            stream.write(json.dumps(dict(SOURCE, key_id="new")) + "\n")
        self.assertEqual(21, sampling.index_source(source, self.cache)["count"])
        with self.assertRaisesRegex(ValueError, "changed after selection"):
            list(sampling.read_group(plan["groups"][0]))

    def test_all_records_is_explicit_and_does_not_require_random_indexes(self):
        with patch.object(sampling, "index_source", side_effect=AssertionError("unnecessary index")):
            result = self.convert(categories=["angel"], all_records=True)
        self.assertEqual(20, result["converted"])

    def test_recursive_inspection_folder_filter_recent_order_and_random_prefix(self):
        self.convert(categories=["angel", "church", "frog"], count=9, seed=9)
        all_rows = selection.catalog(self.output, 9)
        self.assertEqual(["angel"] * 3 + ["church"] * 3 + ["frog"] * 3, [row["category"] for row in all_rows["records"]])
        self.assertEqual(3, len(selection.catalog(self.output / "church")["records"]))
        self.assertEqual("frog", selection.catalog(self.output, 1, order="recent")["records"][0]["category"])
        small = selection.catalog(self.output, 2, order="random", seed=12)["records"]
        large = selection.catalog(self.output, 8, order="random", seed=12)["records"]
        self.assertEqual([row["selectionId"] for row in small], [row["selectionId"] for row in large[:2]])
        self.assertEqual(2, len(list(dataset.load_records(self.output, limit=2, order="random", seed=12))))
        key = small[0]["keyId"]
        self.assertEqual([key], [row["keyId"] for row, _ in dataset.load_records(self.output, key_id=key, order="random", seed=12)])

    def test_filename_context_does_not_rewrite_conflicting_original_word(self):
        file = self.input / "angel.ndjson"
        file.write_text(json.dumps(dict(SOURCE, word="church")) + "\n", encoding="utf-8")
        self.convert(categories=["angel"], count=1, seed=4)
        row, packet = next(dataset.load_records(self.output))
        self.assertEqual("angel", row["category"])
        self.assertEqual("angel", packet["categoryContext"]["value"])
        self.assertEqual("church", packet["source"]["word"])

    def test_single_dataset_is_inspectable_but_not_overwritten_by_collection(self):
        dataset.convert(self.input / "church.ndjson", self.output, GOJO, limit=1, encoder_factory=FakeEncoder)
        before = (self.output / "manifest.json").read_bytes()
        self.assertEqual(1, len(selection.catalog(self.output)["records"]))
        with self.assertRaisesRegex(ValueError, "single dataset"):
            self.convert(categories=["church"], count=1)
        self.assertEqual(before, (self.output / "manifest.json").read_bytes())


if __name__ == "__main__":
    unittest.main()
