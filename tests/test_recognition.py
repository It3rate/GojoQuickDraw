import gzip
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from gojo_quickdraw import dataset, pipeline, recognition, sampling, selection
from test_dataset import FakeEncoder, GOJO, SOURCE


class RecognitionTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.source = self.root / 'source'
        self.source.mkdir()
        self.output = self.root / 'output'
        self.cache = self.root / 'cache'
        self.originals = []
        for category in ('church', 'angel'):
            rows = []
            for i in range(8):
                record = dict(SOURCE, key_id=f'{category}-{i}', word=category)
                record.pop('recognized', None)
                if i < 6:
                    record['recognized'] = i % 2 == 0
                rows.append(json.dumps(record))
            self.originals.extend(rows)
            (self.source / f'{category}.ndjson').write_text('\n'.join(rows) + '\n', encoding='utf-8')
        for name in ('fingerprint', '_pipeline_fingerprint'):
            mock = patch.object(dataset, name, return_value={'revision': 'test', 'sourceSha256': 'test'} if name == 'fingerprint' else 'test')
            mock.start(); self.addCleanup(mock.stop)

    def convert(self, **kwargs):
        return sampling.convert_collection(self.source, self.output, GOJO, encoder_factory=FakeEncoder, cache=self.cache, **kwargs)

    def test_filter_precedes_balanced_sampling_and_reports_exhaustion(self):
        result = self.convert(count=20, seed=3, recognition='recognized')
        rows = list(dataset.load_records(self.output))
        self.assertEqual((6, 14), (result['converted'], result['selectionShortfall']))
        self.assertEqual({'angel': 3, 'church': 3}, {name: sum(row['category'] == name for row, _ in rows) for name in ('angel', 'church')})
        self.assertTrue(all(row['recognized'] is True and packet['source']['recognized'] is True for row, packet in rows))
        self.assertTrue(all(packet['sourceOriginalJson'] in self.originals for _, packet in rows))

    def test_gzip_indexes_keep_false_and_missing_distinct_and_reuse_cache(self):
        path = self.source / 'church.ndjson.gz'
        with gzip.open(path, 'wt', encoding='utf-8') as stream:
            stream.write('\n' + '\n'.join(self.originals[:8]) + '\n')
        for mode, expected in (('all', 8), ('recognized', 3), ('unrecognized', 3), ('unknown', 2)):
            with self.subTest(mode=mode):
                plan = sampling.make_plan({'church': path}, 20, 8, self.cache, mode)
                self.assertEqual(expected, plan['selected'])
                rows = list(sampling.read_group(plan['groups'][0]))
                self.assertTrue(all(line >= 2 and recognition.accepts(recognition.from_original(original), mode) for _, line, original, _ in rows))
                self.assertEqual(sampling.index_source(path, self.cache, mode), sampling.index_source(path, self.cache, mode))

    def test_growth_counts_only_matching_records_and_never_rewrites_others(self):
        self.convert(categories=['church'], count=8, seed=2, target_total=True, append=True)
        before = {path: path.read_bytes() for path in self.output.rglob('*.gz')}
        # Old journals have no cached recognition field. Read their retained source.
        journal = self.output / 'church/index.ndjson'
        journal.write_text('\n'.join(json.dumps({k: v for k, v in json.loads(line).items() if k != 'recognized'})
            for line in journal.read_text().splitlines()) + '\n', encoding='utf-8')
        result = self.convert(categories=['church'], count=4, seed=3, recognition='recognized', target_total=True, append=True)
        self.assertEqual(1, result['selectionShortfall'])
        self.assertEqual(8, result['storedRecords'])
        plan = json.loads((self.output / '_sampling/active.json').read_text())['plan']
        self.assertEqual((3, 0), (plan['retained'], plan['selected']))
        self.assertTrue(all(path.read_bytes() == content for path, content in before.items()))
        page = selection.catalog(self.output, 2, recognition='recognized')
        self.assertEqual((3, 2), (page['storedRecords'], page['pageCount']))
        self.assertEqual(1, len(selection.catalog(self.output, 2, offset=2, recognition='recognized')['records']))
        self.assertEqual(3, selection.catalog(self.output, 10, recognition='unrecognized')['storedRecords'])
        self.assertEqual(2, selection.catalog(self.output, 10, recognition='unknown')['storedRecords'])

    def test_switching_selection_can_add_new_matching_records_and_resumes_exactly(self):
        first = self.convert(categories=['church'], count=2, seed=4, recognition='unrecognized', target_total=True, append=True)
        before = {path: path.read_bytes() for path in self.output.rglob('*.gz')}
        second = self.convert(categories=['church'], count=3, seed=5, recognition='recognized', target_total=True, append=True)
        self.assertEqual((2, 3, 5), (first['converted'], second['converted'], second['storedRecords']))
        again = self.convert(categories=['church'], count=3, seed=5, recognition='recognized', target_total=True, append=True)
        self.assertEqual((0, 3), (again['converted'], again['resumed']))
        self.assertTrue(all(path.read_bytes() == content for path, content in before.items()))

    def test_pipeline_sends_only_eligible_keys_to_all_stages(self):
        real_sample = sampling.convert_collection
        def sample(*args, **kwargs):
            return real_sample(*args, **kwargs, encoder_factory=FakeEncoder, cache=self.cache)
        with patch.object(sampling, 'convert_collection', side_effect=sample), patch.object(pipeline.analysis, 'run', return_value={'errors': 0, 'storedRecords': 2, 'storedErrors': 0}) as analyze:
            pipeline.run(self.output, GOJO, input_path=self.source, count=2, categories=['church'], recognition='recognized')
            self.assertEqual('recognized', analyze.call_args.kwargs['recognition'])
            keys = analyze.call_args.kwargs['key_ids']
            self.assertEqual(2, len(keys))
            self.assertTrue(all(int(key.split('-')[1]) % 2 == 0 for key in keys))

    def test_direct_conversion_and_cli_reject_ambiguous_flags(self):
        from gojo_quickdraw.cli import parser
        self.assertEqual('recognized', parser().parse_args(['process', '--input', str(self.source), '--output', str(self.output), '--limit', '2', '--recognition', 'recognized']).recognition)
        result = dataset.convert(self.source / 'church.ndjson', self.output, GOJO, limit=2,
            recognition='unrecognized', encoder_factory=FakeEncoder)
        self.assertEqual(2, result['converted'])
        self.assertTrue(all(row['recognized'] is False for row, _ in dataset.load_records(self.output)))
        self.assertIsNone(recognition.from_original('{"recognized":true,"recognized":false}'))
        self.assertIsNone(recognition.outcome({'recognized': 'false'}))
        self.assertFalse(recognition.accepts(None, 'unrecognized'))
        with self.assertRaises(ValueError):
            sampling.make_plan({}, 2, 1, self.cache, 'human-approved')


if __name__ == '__main__':
    unittest.main()
