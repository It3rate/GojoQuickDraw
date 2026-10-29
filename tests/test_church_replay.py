import json
from pathlib import Path
import tempfile
import unittest

from gojo_quickdraw.church_replay import compare_prefix, selection_rows
from gojo_quickdraw.church_study import ARMS


class ReplayTests(unittest.TestCase):
    def test_selection_refuses_duplicate_acquisitions_and_path_substitution(self):
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'selection.json';ident='a'*40
            path.write_text(json.dumps([dict(id=ident,reason='quiet control')]))
            self.assertEqual('quiet control',selection_rows(path)[0]['reason'])
            for rows in ([ident,ident],['../'+'a'*37],[],['A'*40]):
                path.write_text(json.dumps(rows))
                with self.assertRaises(ValueError):selection_rows(path)

    def test_long_run_must_preserve_every_earlier_pass_and_the_actual_source(self):
        original=dict(id='a'*40,keyId='1',sourceSha256='s',modelSha256='m',sourceProfiles=1,
                      unrepresentedProfiles=0,source=[[[0,0],[10,0]]],
                      results=[dict(arm=a,stages=[dict(passIndex=0,graph=[0]),dict(passIndex=1,graph=[1])]) for a in ARMS])
        replay=json.loads(json.dumps(original))
        for run in replay['results']:run['stages'].append(dict(passIndex=2,graph=[2]))
        self.assertEqual(4,len(compare_prefix(original,replay)))
        replay['results'][0]['stages'][1]['graph']=[99]
        with self.assertRaisesRegex(ValueError,'Earlier pass changed'):compare_prefix(original,replay)
        replay=json.loads(json.dumps(original));replay['source'][0][1][0]=99
        with self.assertRaisesRegex(ValueError,'retained source'):compare_prefix(original,replay)

    def test_quiet_short_runs_are_valid_exact_prefixes(self):
        original=dict(id='b'*40,results=[dict(arm=a,stages=[dict(index=0),dict(index=1)]) for a in ARMS])
        self.assertEqual(4,len(compare_prefix(original,json.loads(json.dumps(original)))))
        wrong=json.loads(json.dumps(original));wrong['results'].pop()
        with self.assertRaisesRegex(ValueError,'same four'):compare_prefix(original,wrong)


if __name__=='__main__':unittest.main()
