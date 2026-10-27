import gzip
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from gojo_quickdraw import dataset, pipeline, pipeline_artifacts, sampling, selection
from test_dataset import FakeEncoder, GOJO, SOURCE


class PipelineTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.source = self.root / "sources"
        self.source.mkdir()
        self.output = self.root / "completed"
        (self.source / "church.ndjson").write_text("".join(json.dumps(dict(SOURCE, key_id=str(i))) + "\n" for i in range(12)), encoding="utf-8")
        for name, value in (("fingerprint", {"revision": "test", "sourceSha256": "test"}), ("_pipeline_fingerprint", "test")):
            mock = patch.object(dataset, name, return_value=value)
            mock.start(); self.addCleanup(mock.stop)

    def test_same_bounded_random_cohort_reaches_analysis_and_reuses_after_interruption(self):
        real_sample = sampling.convert_collection
        def sample(*args, **kwargs):
            return real_sample(*args, **kwargs, encoder_factory=FakeEncoder, cache=self.root / "cache")
        with patch.object(sampling, "convert_collection", side_effect=sample), patch.object(pipeline.analysis, "run", side_effect=RuntimeError("interrupted")) as analyze:
            with self.assertRaisesRegex(RuntimeError, "interrupted"):
                pipeline.run(self.output, GOJO, input_path=self.source, count=3, categories=["church"])
            keys = analyze.call_args.kwargs["key_ids"]
            self.assertEqual(3, len(keys))
            self.assertEqual(3, analyze.call_args.kwargs["limit"])
            self.assertTrue(analyze.call_args.kwargs["execute_stages"])
            self.assertEqual(set(keys), {row["keyId"] for _, row in selection.selected_rows(self.output / "acquired")})
            before = {p: p.read_bytes() for p in self.output.rglob("*.gz")}
            analyze.side_effect = None
            analyze.return_value = {"converted": 3, "errors": 0, "storedRecords": 3, "storedErrors": 0}
            result = pipeline.run(self.output, GOJO, input_path=self.source, count=3, categories=["church"])
            self.assertEqual(3, result["acquisition"]["resumed"])
            self.assertEqual(keys, analyze.call_args.kwargs["key_ids"])
            self.assertTrue(all(path.read_bytes() == value for path, value in before.items()))
            result = pipeline.run(self.output, GOJO, input_path=self.source, count=4, categories=["church"])
            self.assertEqual(1, result['acquisition']['converted'])
            self.assertEqual(4, len(analyze.call_args.kwargs['key_ids']))
            self.assertTrue(set(keys).issubset(analyze.call_args.kwargs['key_ids']))
            self.assertTrue(all(path.read_bytes() == value for path, value in before.items()))
            self.assertTrue(analyze.call_args.kwargs['append'])
            self.assertEqual(['church'], analyze.call_args.kwargs['categories'])
            pipeline.run(self.output, GOJO, input_path=self.source, count=2, categories=['church'])
            self.assertEqual(4, len(list(selection.selected_rows(self.output / 'acquired'))))

    def test_invalid_counts_and_source_overlap_fail_before_creating_workspace(self):
        for count in (None, 0, -1, True, 1.5):
            with self.subTest(count=count), self.assertRaisesRegex(ValueError, "positive drawing count"):
                pipeline.run(self.output, GOJO, input_path=self.source, count=count)
        self.assertFalse(self.output.exists())
        with self.assertRaisesRegex(ValueError, "separate"):
            pipeline.run(self.source / "output", GOJO, input_path=self.source, count=1)
        self.assertFalse((self.source / "output").exists())

    def test_existing_dataset_runs_only_requested_count_without_resampling(self):
        with patch.object(pipeline.analysis, "run", return_value={"converted": 2, "errors": 0, "storedRecords": 2, "storedErrors": 0}) as analyze, patch.object(sampling, "convert_collection") as sample:
            pipeline.run(self.output, GOJO, source_dataset=self.source, count=2)
            self.assertEqual(self.source.resolve(), analyze.call_args.args[0])
            self.assertEqual(2, analyze.call_args.kwargs["limit"])
            sample.assert_not_called()

    def test_workspace_discovery_never_counts_acquisition_and_result_as_two_drawings(self):
        self.output.mkdir()
        (self.output / "pipeline.json").write_text(json.dumps({"schema": pipeline.SCHEMA}))
        for name in ("acquired", "results"):
            folder = self.output / name / "church"
            folder.mkdir(parents=True)
            (folder / "manifest.json").write_text(json.dumps({"schema": dataset.SCHEMA}))
        self.assertEqual("acquired", selection.datasets(self.output)[0][0].parent.name)
        (self.output / "results/collection.json").write_text("{}")
        found = selection.datasets(self.output)
        self.assertEqual(1, len(found))
        self.assertEqual("results", found[0][0].parent.name)

    def test_corrupt_stage_artifact_cannot_be_reused_as_a_complete_record(self):
        self.output.mkdir()
        row = {}
        for field, digest in (("evidencePath", "evidenceSha256"), ("nativePath", "nativeSha256"), ("stagesPath", "stagesSha256")):
            path = self.output / (field + ".gz")
            dataset._gzip_json(path, {"test": True})
            row[field], row[digest] = path.name, dataset.sha256(path)
        dataset.verify_row(self.output, row)
        (self.output / row["stagesPath"]).write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "changed dataset artifact"):
            dataset.verify_row(self.output, row)


if __name__ == "__main__":
    unittest.main()
