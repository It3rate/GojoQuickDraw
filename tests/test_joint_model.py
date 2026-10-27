import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from gojo_quickdraw import joint_model, training_data


class LocalModelTests(unittest.TestCase):
    def examples(self):
        # Deliberately distinct coarse local observations, with independent groups.
        patterns = [
            [0, 5, 0, 5, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5],
            [0, 5, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5],
            [0, 5, 0, 5, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 5],
            [3, 3, 0, 5, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 5],
            [3, 3, 3, 3, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 5]]
        return [{'group': f'independent-{i}', 'bands': patterns[i % 5], 'label': i % 5,
                 'labelBasis': 'controlled-construction'} for i in range(160)]

    def test_small_ranker_learns_and_reloads_without_geometry_or_identifiers(self):
        rows = self.examples()
        model = joint_model.fit(rows, 'partitions', epochs=18, seed=31)
        test = [row for row in rows if training_data.split(row['group']) == 'test']
        self.assertGreater(joint_model.metrics(model, test)['top1Agreement'], .95)
        restored = json.loads(json.dumps(model))
        for row in test:
            self.assertEqual(joint_model.predict(model, row['bands']), joint_model.predict(restored, row['bands']))
        self.assertLess(model['learningCurve'][-1]['validationCrossEntropy'], model['learningCurve'][0]['validationCrossEntropy'])

    def test_held_out_labels_cannot_influence_weights_or_temperature_selection(self):
        rows = self.examples(); changed = copy.deepcopy(rows)
        for row in changed:
            if training_data.split(row['group']) == 'test':
                row['label'] = (row['label'] + 2) % 5
        a = joint_model.fit(rows, 'partitions', epochs=6)
        b = joint_model.fit(changed, 'partitions', epochs=6)
        self.assertEqual(a['weights'], b['weights'])
        self.assertEqual(a['temperature'], b['temperature'])

    def test_uniform_scores_remain_unresolved_and_unknown_cells_are_reported(self):
        model = {'mode': 'curves', 'columns': ['bias'], 'weights': [[0] for _ in range(5)]}
        read = joint_model.predict(model, self.examples()[0]['bands'])
        self.assertIsNone(read['proposed'])
        self.assertEqual('unresolved', read['status'])
        self.assertAlmostEqual(1, sum(read['scores']))
        self.assertGreater(read['unseenFeatures'], 0)
        with self.assertRaises(ValueError):
            joint_model.predict(model, [True]*16)

    def test_run_reversal_and_role_swap_keep_type_features_while_source_order_can_survive_elsewhere(self):
        cells = [0, 5, 3, 3, 1, 2, 4, 0, 2, 1, 3, 0, 3, 1, 0, 4]
        swapped = [cells[2], cells[3], cells[0], cells[1], cells[5], cells[4], cells[6],
                   cells[8], cells[7], cells[10], cells[9], cells[12], cells[11], cells[14], cells[13], cells[15]]
        reversed_run = cells.copy(); reversed_run[0:2] = reversed_run[0:2][::-1]
        self.assertEqual(joint_model.tokens(cells), joint_model.tokens(swapped))
        self.assertEqual(joint_model.tokens(cells), joint_model.tokens(reversed_run))

    def test_synthetic_variations_never_cross_split_and_all_classes_are_present(self):
        requests = list(training_data.synthetic_requests(500, seed=31))
        patches = [row for request in requests for row in request['patches']]
        self.assertEqual(500, len(patches))
        self.assertEqual(set(range(5)), {row['label'] for row in patches})
        groups = {}
        for row in patches:
            groups.setdefault(row['group'], set()).add(training_data.split(row['group']))
        self.assertTrue(all(len(values) == 1 for values in groups.values()))
        self.assertEqual({'train', 'validation', 'test'}, {training_data.split(row['group']) for row in patches})

    def test_live_snapshot_ignores_uncommitted_tail_without_writing_an_index_cache(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder = Path(temporary)
            (folder/'manifest.json').write_text('{"schema":"gojo.quickdraw-dataset.v1"}')
            old = {'itemId': 'a', 'status': 'ok', 'sourceSha256': 'one', 'convertedUtc': '2026-10-26'}
            new = dict(old, itemId='b', convertedUtc='2026-10-27')
            content = (json.dumps(old)+'\n'+json.dumps(new)+'\n{"itemId":"uncommitted').encode()
            journal = folder/'index.ndjson'; journal.write_bytes(content)
            before = {p.name: p.read_bytes() for p in folder.iterdir()}
            rows, warnings = training_data.snapshot(folder, 300)
            self.assertEqual('b', rows[0][1]['itemId'])
            self.assertEqual(1, len(rows)); self.assertEqual(1, len(warnings))
            self.assertEqual(before, {p.name: p.read_bytes() for p in folder.iterdir()})

    def test_three_run_joint_does_not_label_a_two_run_fragment_as_a_tee(self):
        spans = [{'controls': [[0, 0], [20, 0]], 'pathLength': 20},
                 {'controls': [[20, 0], [40, 0]], 'pathLength': 20},
                 {'controls': [[20, 0], [20, 20]], 'pathLength': 20}]
        packet = {'source': {'key_id': 'k'}, 'sourceOriginalJson': '{}', 'program': {}, 'annotations': {},
                  'observation': {'spans': spans}, 'jointComparison': {
                      'source': {'nodes': [{'id': 'j', 'on': [{'edge': 'p0'}, {'edge': 'p1'}, {'edge': 'p2'}]}]},
                      'final': {'nodes': [{'id': 'f', 'kind': 'T'}]},
                      'matches': [{'target': 'f', 'candidates': ['j']}]}}
        request = training_data.real_request(packet, hashlib.sha256(b'source').hexdigest())
        self.assertTrue(request['patches'])
        self.assertTrue(all(row['label'] is None for row in request['patches']))

    def test_training_cli_is_independent_from_conversion_and_keeps_a_bounded_count(self):
        from gojo_quickdraw.cli import parser
        args = parser().parse_args(['train', '--dataset', 'saved', '--output', 'models', '--limit', '300'])
        self.assertEqual(300, args.limit)
        self.assertEqual(2500, args.synthetic)
        self.assertEqual(35, args.epochs)

    def test_zero_extent_profiles_have_no_fabricated_ratio_and_remain_unresolved(self):
        packet = {'source': {'key_id': 'k'}, 'sourceOriginalJson': '{}', 'program': {}, 'annotations': {},
                  'observation': {'spans': [{'controls': [[1, 1], [1.00001, 1]], 'pathLength': .00001},
                      {'controls': [[0, 0], [20, 0]], 'pathLength': 20},
                      {'controls': [[10, -10], [10, 1]], 'pathLength': 11}]}}
        request = training_data.real_request(packet, 'source')
        self.assertEqual([0], request['unresolvedProfiles'])
        self.assertEqual([[1, 2]], [row['profiles'] for row in request['patches']])


if __name__ == '__main__':
    unittest.main()
