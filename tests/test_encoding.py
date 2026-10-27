import copy
import json
from pathlib import Path
import tempfile
import unittest

from gojo_quickdraw import encoding, encoding_report, training_data


class EncodingTests(unittest.TestCase):
    def test_inference_proposals_are_independent_of_final_interpretation_and_labels(self):
        packet = {'source': {'key_id': 'k'}, 'sourceOriginalJson': '{}', 'program': {}, 'annotations': {},
                  'observation': {'spans': [{'controls': [[0, 0], [100, 0]], 'pathLength': 100},
                                            {'controls': [[50, -50], [50, 4]], 'pathLength': 54}]}}
        before = copy.deepcopy(packet)
        first = training_data.real_request(packet, 'g', inference=True)
        packet['jointComparison'] = 'deliberately unreadable final interpretation'
        second = training_data.real_request(packet, 'g', inference=True)
        self.assertEqual(first, second)
        self.assertTrue(all(row['label'] is None for row in first['patches']))
        del packet['jointComparison']
        self.assertEqual(before, packet)

    def test_model_contract_rejects_different_candidates_nonfinite_weights_and_undefined_temperature(self):
        original = {'schema': 'gojo.local-joint-model.v1', 'candidateOrder': list(training_data.NAMES),
                    'mode': 'curves', 'columns': ['bias'], 'weights': [[0] for _ in range(5)]}
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)/'model.json'
            for field, value in [('candidateOrder', ['anything']), ('weights', [[float('nan')]]*5),
                                 ('columns', [['not text']]), ('temperature', 0), ('temperature', True)]:
                model = dict(original, **{field: value}); path.write_text(json.dumps(model))
                with self.assertRaises(ValueError):
                    encoding.load_model(path)
            path.write_text(json.dumps(original))
            self.assertEqual(original, encoding.load_model(path))

    def test_inference_cli_keeps_selection_and_model_separate_from_training(self):
        from gojo_quickdraw.cli import parser
        args = parser().parse_args(['encode', '--dataset', 'saved', '--output', 'run', '--model', 'model.json', '--limit', '12'])
        self.assertEqual([Path('model.json')], args.model)
        self.assertEqual(12, args.limit)
        self.assertEqual(128, args.max_patches)
        self.assertFalse(hasattr(args, 'epochs'))

    def test_output_and_duplicate_providers_fail_before_source_or_output_mutation(self):
        with tempfile.TemporaryDirectory() as folder:
            source = Path(folder)/'drawings'; source.mkdir()
            for target, models in [(source/'inside', []), (Path(folder)/'run', ['a', 'b'])]:
                with self.assertRaises(ValueError):
                    encoding.run(source, target, 'unused', models=models)
                self.assertFalse(target.exists() if target != source/'inside' else (source/'inside').exists())
            self.assertEqual([], list(source.iterdir()))

    def test_portable_review_paginates_all_drawings_and_escapes_source_text(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary); (folder/'drawings').mkdir()
            items = []
            for i in range(13):
                relative = f'drawings/{i}.json'; (folder/relative).write_text(json.dumps({'sourceKey': '</script><script>bad()', 'index': i}))
                items.append({'path': relative})
            manifest = {'drawings': items, 'sourceDrawings': 13, 'localPatches': 0, 'changedPatches': 0, 'provider': 'default'}
            encoding_report.build(folder, manifest)
            first = (folder/'report.html').read_text(encoding='utf-8'); last = (folder/'page-00002.html').read_text(encoding='utf-8')
            self.assertIn('Page 1 of 2', first); self.assertIn('Page 2 of 2', last)
            self.assertIn('\\u003c/script>', first)
            self.assertNotIn('</script><script>bad()', first)


if __name__ == '__main__':
    unittest.main()
