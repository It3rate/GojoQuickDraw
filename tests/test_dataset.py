from __future__ import annotations

import gzip
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from gojo_quickdraw import dataset
from gojo_quickdraw.upstream import ROOT


GOJO = ROOT.parent / "Gojo"
SOURCE = {"key_id": "sample-1", "word": "church", "drawing": [[[0, 20, 20, 0], [0, 0, 30, 30], [9, 7, 8, 6]]]}


class FakeEncoder:
    calls = 0
    def __init__(self, _, output):
        self.output = output
    def __enter__(self):
        return self
    def __exit__(self, *_):
        return None
    def encode(self, packet):
        type(self).calls += 1
        name = packet["itemKey"] + ".gojo.json.gz"
        dataset._gzip_json(self.output / name, {"source": packet["sourceOriginalJson"]})
        return {"ok": True, "itemKey": packet["itemKey"], "archive": name,
                "summary": {"testDouble": True, "sourceId": packet["source"]["key_id"],
                            "sourceSha256": hashlib.sha256(packet["sourceOriginalJson"].encode()).hexdigest()}}


class DatasetTests(unittest.TestCase):
    def test_native_archive_uses_returned_codec_and_preserves_identity(self):
        for suffix in ('.gojo.bin.gz', '.gojo.json.gz'):
            self.assertEqual(self.root/'native'/('abc'+suffix),
                dataset.native_archive_path(self.root,'abc',{'archive':'abc'+suffix}))
        for name in ('../abc.gojo.bin.gz', 'different.gojo.bin.gz', None):
            with self.assertRaises(ValueError):
                dataset.native_archive_path(self.root,'abc',{'archive':name})

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.input = self.root / "source.ndjson"
        self.output = self.root / "dataset"
        FakeEncoder.calls = 0
        self.patches = [patch.object(dataset, "fingerprint", return_value={"revision": "test", "sourceSha256": "test"}),
                        patch.object(dataset, "_pipeline_fingerprint", return_value="test")]
        for item in self.patches:
            item.start()
    def tearDown(self):
        for item in self.patches:
            item.stop()
        self.temp.cleanup()
    def run_conversion(self, **kwargs):
        return dataset.convert(self.input, self.output, GOJO, encoder_factory=FakeEncoder, **kwargs)
    def write(self, rows):
        self.input.write_text("\n".join(row if isinstance(row, str) else json.dumps(row) for row in rows) + "\n", encoding="utf-8")

    def test_source_order_negative_times_and_exact_text_are_preserved(self):
        original = '{"key_id":"9007199254740993","word":"church","drawing":[[[0.10,1e1],[3,4],[9,-2]]]}'
        packet = dataset.build_packet(original, GOJO, "test")
        self.assertEqual(original, packet["sourceOriginalJson"])
        self.assertEqual([9, -2], packet["observation"]["strokes"][0]["times"])
        self.assertEqual("9007199254740993", packet["source"]["key_id"])
        self.assertEqual("observed-fit", packet["annotations"]["mode"])
        self.assertNotIn("proposal", packet)

    def test_malformed_record_is_retained_and_next_record_converts(self):
        self.write(["{broken", SOURCE])
        result = self.run_conversion()
        self.assertEqual((1, 1), (result["converted"], result["errors"]))
        self.assertEqual(1, len(list(dataset.load_records(self.output))))
        retained = next((self.output / "errors").glob("*.gz"))
        with gzip.open(retained, "rt") as stream:
            self.assertEqual("{broken", json.load(stream)["sourceOriginalJson"])

    def test_resume_does_not_reencode_or_duplicate_records(self):
        self.write([SOURCE, SOURCE])
        result = self.run_conversion()
        self.assertEqual((1, 1), (result["converted"], result["resumed"]))
        before = (self.output / "index.ndjson").read_bytes()
        result = self.run_conversion()
        self.assertEqual((0, 2), (result["converted"], result["resumed"]))
        self.assertEqual(1, FakeEncoder.calls)
        self.assertEqual(before, (self.output / "index.ndjson").read_bytes())

    def test_resume_rejects_modified_native_archive(self):
        self.write([SOURCE])
        self.run_conversion()
        row, _ = next(dataset.load_records(self.output))
        (self.output / row["nativePath"]).write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "changed"):
            self.run_conversion()

    def test_changed_configuration_requires_new_dataset(self):
        self.write([SOURCE])
        self.run_conversion()
        with self.assertRaisesRegex(ValueError, "changed"):
            self.run_conversion(fit_config={"polylineTolerance": 3})

    def test_empty_drawing_keeps_open_context_without_inventing_program(self):
        source = dict(SOURCE, drawing=[])
        self.write([source])
        self.assertEqual(1, self.run_conversion()["converted"])
        _, packet = next(dataset.load_records(self.output))
        self.assertIsNone(packet["program"])
        self.assertIsNone(packet["annotations"])
        self.assertEqual([], packet["context"]["parts"])

    def test_filter_and_limit_count_selected_sources(self):
        self.write([dict(SOURCE, word="frog", key_id="f"), SOURCE, dict(SOURCE, key_id="other")])
        result = self.run_conversion(categories=["CHURCH"], limit=1)
        self.assertEqual(1, result["converted"])
        self.assertEqual("sample-1", next(dataset.load_records(self.output))[0]["keyId"])

    def test_gzip_folder_inputs_and_filename_context_are_explicit(self):
        folder = self.root / "inputs"
        folder.mkdir()
        source = dict(SOURCE)
        source.pop("word")
        for name in ("church", "frog"):
            with gzip.open(folder / (name + ".ndjson.gz"), "wt") as stream:
                stream.write(json.dumps(source) + "\n")
        result = dataset.convert(folder, self.output, GOJO, encoder_factory=FakeEncoder)
        self.assertEqual(2, result["converted"])
        records = list(dataset.load_records(self.output))
        self.assertNotEqual(records[0][0]["itemId"], records[1][0]["itemId"])
        self.assertEqual({"church", "frog"}, {p["categoryContext"]["value"] for _, p in records})
        self.assertTrue(all(p["categoryContext"]["basis"] == "input-filename" for _, p in records))

    def test_interrupted_tail_is_preserved_and_repaired_on_resume(self):
        self.write([SOURCE])
        self.run_conversion()
        index = self.output / "index.ndjson"
        with index.open("ab") as stream:
            stream.write(b'{"itemId":"unfinished"}')
        self.run_conversion()
        self.assertEqual(b'{"itemId":"unfinished"}', (self.output / "index-interrupted-tail.bin").read_bytes())
        self.assertEqual(1, len(index.read_text().splitlines()))

    def test_committed_corruption_is_not_silently_repaired(self):
        self.write([SOURCE])
        self.run_conversion()
        with (self.output / "index.ndjson").open("ab") as stream:
            stream.write(b"bad\n")
        with self.assertRaisesRegex(ValueError, "Corrupt committed"):
            self.run_conversion()

    def test_output_cannot_be_within_scanned_input(self):
        with self.assertRaisesRegex(ValueError, "outside"):
            dataset.convert(self.root, self.root / "inside", GOJO, encoder_factory=FakeEncoder)

    def test_duplicate_properties_boolean_coordinates_and_mixed_formats_rejected(self):
        for source in ('{"key_id":"a","key_id":"b","drawing":[]}',
                       json.dumps(dict(SOURCE, drawing=[[[True], [0]]])),
                       json.dumps(dict(SOURCE, drawing=[[[1], [2]], [[1], [2], [3]]]))):
            with self.assertRaises(ValueError):
                dataset.parse_source(source)

    def test_capacity_failures_retain_whole_original(self):
        self.write([SOURCE])
        result = self.run_conversion(max_points=2)
        self.assertEqual(1, result["errors"])
        self.assertEqual(0, FakeEncoder.calls)

    def test_live_writer_lock_rejects_second_writer(self):
        with dataset._writer_lock(self.output):
            with self.assertRaisesRegex(ValueError, "Another converter"):
                with dataset._writer_lock(self.output):
                    pass

    def test_verification_rejects_archive_for_different_acquisition(self):
        self.write([SOURCE])
        self.run_conversion()
        with patch.object(dataset.bridge, "inspect", return_value={"summary": {"sourceId": "different", "sourceSha256": "wrong"}}):
            with self.assertRaisesRegex(ValueError, "another acquisition"):
                dataset.verify(self.output, GOJO)


if __name__ == "__main__":
    unittest.main()
