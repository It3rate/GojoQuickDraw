from pathlib import Path
import unittest
from unittest.mock import patch

from gojo_quickdraw.analysis import run


class AnalysisSelectionTests(unittest.TestCase):
    def test_invalid_and_missing_source_keys_fail_before_creating_outputs(self):
        for keys in ([], "single-key", [""], [123]):
            with self.subTest(keys=keys), self.assertRaisesRegex(ValueError, "Key selection"):
                run(Path("source"), Path("output"), Path("gojo"), key_ids=keys)
        with patch("gojo_quickdraw.selection.selected_rows", return_value=iter([(Path("source"), {"keyId": "present"})])), \
             patch("gojo_quickdraw.bridge.executable") as bridge:
            with self.assertRaisesRegex(ValueError, "absent: missing"):
                run(Path("source"), Path("output"), Path("gojo"), key_ids=["present", "missing"])
            bridge.assert_not_called()


if __name__ == "__main__":
    unittest.main()
