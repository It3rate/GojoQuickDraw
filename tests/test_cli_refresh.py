from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
import json
from pathlib import Path
import unittest
from unittest.mock import patch

from gojo_quickdraw import cli


class CliRefreshTests(unittest.TestCase):
    def invoke(self, args):
        output, error = StringIO(), StringIO()
        with patch.object(cli.upstream, "activate", return_value=Path("linked-gojo")), \
                redirect_stdout(output), redirect_stderr(error):
            status = cli.main(args)
        return status, json.loads(output.getvalue()) if output.getvalue() else None, error.getvalue()

    def test_refresh_command_preserves_selected_scope_and_page_settings(self):
        with patch("gojo_quickdraw.refresh.run", return_value={"drawings": 9}) as refresh:
            status, result, error = self.invoke(["refresh", "--dataset", "selected/church", "--output", "views",
                                                "--page-size", "4", "--gallery-page-size", "12", "--order", "random", "--seed", "89"])
            self.assertEqual((0, {"drawings": 9}, ""), (status, result, error))
            refresh.assert_called_once_with(Path("selected/church"), Path("views"), review_page_size=4,
                                             gallery_page_size=12, order="random", seed=89)

    def test_batch_refresh_is_opt_in_and_uses_actual_output_including_resume(self):
        for command, worker, source_flag in (("convert", "gojo_quickdraw.dataset.convert", "--input"),
                                            ("sample", "gojo_quickdraw.sampling.convert_collection", "--input"),
                                            ("analyze", "gojo_quickdraw.analysis.run", "--dataset")):
            for refresh_requested in (False, True):
                with self.subTest(command=command, refresh=refresh_requested), \
                        patch(worker, return_value={"output": "actual/selected-output", "resumed": 9, "errors": 0}), \
                        patch("gojo_quickdraw.refresh.run", return_value={"drawings": 9}) as refresh:
                    args = [command, source_flag, "sources", "--output", "requested-output"]
                    status, result, error = self.invoke(args + (["--refresh"] if refresh_requested else []))
                    self.assertEqual((0, ""), (status, error))
                    if refresh_requested:
                        refresh.assert_called_once_with(Path("actual/selected-output"))
                        self.assertEqual(9, result["refresh"]["drawings"])
                    else:
                        refresh.assert_not_called()
                        self.assertNotIn("refresh", result)

    def test_partial_batch_preserves_error_status_and_refreshes_successful_records(self):
        with patch.object(cli.dataset, "convert", return_value={"output": "generated", "converted": 2, "errors": 1}), \
                patch("gojo_quickdraw.refresh.run", return_value={"drawings": 2}) as refresh:
            status, result, error = self.invoke(["convert", "--input", "source", "--output", "generated", "--refresh"])
            self.assertEqual((1, ""), (status, error))
            self.assertEqual(2, result["refresh"]["drawings"])
            refresh.assert_called_once_with(Path("generated"))

    def test_analysis_preserves_explicit_reviewed_key_subset(self):
        with patch("gojo_quickdraw.analysis.run", return_value={"converted": 2, "errors": 0}) as analyze:
            status, _, error = self.invoke(["analyze", "--dataset", "saved", "--output", "derived",
                                            "--key-id", "first-key", "--key-id", "second-key", "--limit", "2"])
            self.assertEqual((0, ""), (status, error))
            analyze.assert_called_once_with(Path("saved"), Path("derived"), Path("linked-gojo"),
            limit=2, workers=4, retry_errors=False, key_ids=["first-key", "second-key"], recognition="all")

    def test_empty_or_failed_batch_does_not_attempt_to_build_empty_reviews(self):
        with patch.object(cli.dataset, "convert", return_value={"output": "generated", "errors": 1}), \
                patch("gojo_quickdraw.refresh.run") as refresh:
            status, result, error = self.invoke(["convert", "--input", "source", "--output", "generated", "--refresh"])
            self.assertEqual((1, ""), (status, error))
            self.assertTrue(result["refresh"]["skipped"])
            refresh.assert_not_called()


if __name__ == "__main__":
    unittest.main()
