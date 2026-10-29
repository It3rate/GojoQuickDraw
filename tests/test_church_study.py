import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from gojo_quickdraw.church_study import select, execute_batch
from gojo_quickdraw.church_study_review import movement, summarize, write


class ChurchStudyTests(unittest.TestCase):
    def test_selection_is_bounded_recognized_deterministic_and_deduplicated(self):
        with tempfile.TemporaryDirectory() as temp:
            path=Path(temp)/'church.ndjson'
            records=[dict(key_id=str(i),recognized=i%3==0) for i in range(60)]
            records.extend([dict(key_id='missing'),dict(key_id='0',recognized=True)])
            path.write_text(''.join(json.dumps(r)+'\n' for r in records)+'invalid\n',encoding='utf-8')
            a,counts,digest=select(path,7,3);b,_,other=select(path,7,3)
            self.assertEqual(a,b);self.assertEqual(digest,other);self.assertEqual(7,len(a))
            self.assertEqual(1,counts['duplicates']);self.assertEqual(1,counts['unknown']);self.assertEqual(1,counts['malformed'])
            self.assertTrue(all(json.loads(r['original'])['recognized'] is True for r in a))
            self.assertEqual(len(a),len({r['id'] for r in a}))
            with self.assertRaises(ValueError):select(path,0,3)

    def test_no_recognition_is_not_silently_treated_as_recognized(self):
        with tempfile.TemporaryDirectory() as temp:
            path=Path(temp)/'source.ndjson';path.write_text('{"key_id":"a"}\n{"key_id":"b","recognized":false}\n')
            rows,counts,_=select(path,3,4)
            self.assertEqual([],rows);self.assertEqual(1,counts['unknown']);self.assertEqual(1,counts['unrecognized'])

    def test_worker_keeps_stdout_separate_from_log(self):
        with tempfile.TemporaryDirectory() as temp:
            output=Path(temp)
            with patch('gojo_quickdraw.church_study.subprocess.run') as process:
                process.return_value.returncode=0;process.return_value.stdout='{"id":"x","ok":true}\n'
                rows=execute_batch(['dotnet','study.dll'],output/'input.ndjson',output,output/'worker.log')
                self.assertTrue(rows[0]['ok']);self.assertNotIn('capture_output',process.call_args.kwargs)
                self.assertIn('stdout',process.call_args.kwargs);self.assertIn('stderr',process.call_args.kwargs)

    def test_source_interval_drift_does_not_count_removed_or_inferred_ink(self):
        initial=dict(edges=[dict(source='p0',controls=[[0,0],[100,0]],interval=[0,1],inferred=False)])
        final=dict(edges=[dict(source='p0',controls=[[25,3],[75,3]],interval=[.25,.75],inferred=False),
                          dict(source='inferred:a',controls=[[0,0],[1000,0]],interval=[0,1],inferred=True)])
        self.assertEqual(3,movement(initial,final))

    def test_summary_does_not_present_no_numeric_observations_as_zero_strain(self):
        stage=dict(graph=dict(edges=[]),errors=[],components=1,cycles=0,sourceEdges=0,inferredEdges=0,applied=0)
        run=dict(arm='local-models',stages=[stage,stage],accepted=0,moved=0,passes=1)
        result=summarize([dict(results=[run])])
        self.assertIsNone(result['local-models']['mappingStrain'])
        self.assertEqual(0,result['local-models']['sourceMovement'])

    def test_empty_review_is_readable_before_any_worker_completes(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);(root/'drawings').mkdir();write(root)
            self.assertTrue((root/'review.html').is_file());self.assertTrue((root/'pages/page-00001.js').is_file())
            self.assertEqual(0,json.loads((root/'summary.json').read_text())['drawings'])


if __name__=='__main__':unittest.main()
