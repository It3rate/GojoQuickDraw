import json
import unittest
from unittest.mock import patch

import test_sampling
from gojo_quickdraw import dataset, selection
from test_dataset import SOURCE


class GrowthTests(unittest.TestCase):
    setUp = test_sampling.CollectionTests.setUp
    convert = test_sampling.CollectionTests.convert

    def grow(self, **options):
        return self.convert(target_total=True, append=True, **options)

    def test_raise_total_preserves_originals_even_after_code_and_fit_change(self):
        self.grow(categories=['church'], count=3, seed=42)
        before = {p: p.read_bytes() for p in self.output.rglob('*.gz')}
        with patch.object(dataset, '_pipeline_fingerprint', return_value='new-version'):
            result = self.grow(categories=['church'], count=7, seed=42, fit_config={'polylineTolerance': 4})
        self.assertEqual(4, result['converted'])
        rows = list(selection.selected_rows(self.output))
        self.assertEqual(7, len(rows))
        self.assertEqual(7, len({r['keyId'] for _, r in rows}))
        self.assertTrue(all(p.read_bytes() == value for p, value in before.items()))
        manifest = json.loads((self.output / 'church/manifest.json').read_text())
        self.assertEqual(2, len(manifest['processingVersions']))
        self.assertEqual(2, len({row['extractionFingerprint'] for _, row in rows}))

    def test_equal_or_lower_target_does_not_add_replace_or_delete(self):
        self.grow(categories=['church'], count=5, seed=1)
        before = {p: p.read_bytes() for p in self.output.rglob('*.gz')}
        for count, seed in [(5, 1), (5, 2), (2, 9)]:
            result = self.grow(categories=['church'], count=count, seed=seed)
            self.assertEqual(0, result['converted'])
            self.assertEqual(5, result['storedRecords'])
        self.assertTrue(all(p.read_bytes() == value for p, value in before.items()))

    def test_selected_categories_share_target_other_categories_remain(self):
        self.grow(categories=['church'], count=4, seed=1)
        self.grow(categories=['angel'], count=3, seed=1)
        result = self.grow(categories=['angel', 'church'], count=10, seed=2)
        self.assertEqual(3, result['converted'])
        self.assertEqual(10, result['storedRecords'])
        self.assertEqual(5, len(list(selection.selected_rows(self.output / 'angel'))))
        self.assertEqual(5, len(list(selection.selected_rows(self.output / 'church'))))

    def test_duplicate_source_records_and_exhaustion_do_not_exceed_unique_total(self):
        path = self.input / 'church.ndjson'
        path.write_text(path.read_text() * 2)
        result = self.grow(categories=['church'], count=25, seed=42)
        self.assertEqual(20, result['converted'])
        self.assertEqual(5, result['selectionShortfall'])
        self.assertEqual(20, len(list(selection.selected_rows(self.output))))

    def test_interrupted_addition_resumes_same_cohort_without_duplicates(self):
        self.grow(categories=['church'], count=2, seed=1)
        original = dataset.convert
        calls = 0
        def interrupted(*args, **kwargs):
            nonlocal calls
            calls += 1
            if calls == 2:
                raise OSError('interrupted append')
            return original(*args, **kwargs)
        with patch.object(dataset, 'convert', side_effect=interrupted), self.assertRaisesRegex(OSError, 'interrupted append'):
            self.grow(categories=['angel', 'church'], count=8, seed=1)
        plan = json.loads((self.output / '_sampling/active.json').read_text())['plan']
        result = self.grow(categories=['angel', 'church'], count=8, seed=1)
        self.assertEqual(plan, json.loads((self.output / '_sampling/active.json').read_text())['plan'])
        self.assertEqual(8, result['storedRecords'])
        self.assertEqual(8, len({row['keyId'] for _, row in selection.selected_rows(self.output)}))

    def test_failed_selection_is_not_resampled_and_can_be_retried_after_growth(self):
        path = self.input / 'church.ndjson'
        failed = dict(SOURCE, key_id='large', drawing=[[[0, 1, 2], [0, 1, 2]]])
        path.write_text(json.dumps(failed) + '\n')
        result = self.grow(categories=['church'], count=1, seed=1, max_points=2)
        self.assertEqual(1, result['storedErrors'])
        with path.open('a') as stream:
            for key in ('small-1', 'small-2'):
                stream.write(json.dumps(dict(SOURCE, key_id=key, drawing=[[[0, 1], [0, 1]]])) + '\n')
        grown = self.grow(categories=['church'], count=3, seed=1, max_points=2)
        self.assertEqual((2, 1), (grown['storedRecords'], grown['storedErrors']))
        repaired = self.grow(categories=['church'], count=3, seed=1, max_points=3, retry_errors=True)
        self.assertEqual((3, 0), (repaired['storedRecords'], repaired['storedErrors']))
        self.assertEqual(3, len(list(selection.selected_rows(self.output))))


