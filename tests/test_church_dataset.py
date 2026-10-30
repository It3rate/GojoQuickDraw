import gzip
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from gojo_quickdraw import church_dataset as refine


class ChurchDatasetTests(unittest.TestCase):
    def test_selection_counts_recognized_churches_after_filtering_and_deduplicates_sources(self):
        rows=[(Path('x'),dict(category=category,sourceSha256=sha,convertedUtc='2026-10-29T00:00:00+00:00'))
              for category,sha in [('angel','a'),('church','b'),('church','b'),('church','c'),('church','d')]]
        with patch.object(refine,'selected_rows',return_value=iter(rows)) as read:
            chosen=refine.select('input',2)
        self.assertEqual(['b','c'],[row['sourceSha256'] for _,row in chosen])
        self.assertEqual('recognized',read.call_args.kwargs['recognition'])
        for count in (0,None,True,10001):
            with self.assertRaises(ValueError):refine.select('input',count)

    def test_saved_final_shapes_are_comparison_only_and_never_become_source_features(self):
        packet=dict(sourceOriginalJson='{}',source={'recognized':True},observation={'spans':[]},
                    program={},annotations={},layout={'parts':[]},cleanup={'parts':[]})
        with patch('gojo_quickdraw.layout.estimate',return_value={'new':True}) as estimate, \
             patch.object(refine,'compile_plan',return_value=({'parts':[]},{},{},{})) as compile:
            request,baseline,_=refine.prepare(packet,'a'*40,Path('model.json'))
        self.assertEqual({'new':True},compile.call_args.args[1])
        self.assertTrue(all(compile.call_args.kwargs[k] for k in ('connections','cad','recover_missing')))
        self.assertNotIn('layout',request['packet']);self.assertNotIn('cleanup',request['packet'])
        self.assertEqual(2,len(baseline['results'][0]['stages']))
        self.assertEqual(packet['observation'],estimate.call_args.args[0])

    def test_completed_receipts_resume_without_native_work_and_detect_artifact_changes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);source=root/'source';source.mkdir();output=root/'results';work=root/'work'
            text='{"word":"church"}';sha=hashlib.sha256(text.encode()).hexdigest();ident=sha[:40]
            packet=dict(sourceOriginalJson=text,source={'recognized':True},observation={},program={},annotations={})
            with gzip.open(source/'source.gz','wt') as stream:json.dump(packet,stream)
            row=dict(itemId='saved',sourceSha256=sha,evidenceSha256='unchanged',evidencePath='source.gz')
            model=root/'model.json';model.write_text('{}')
            def execute(command,path,output,log):
                self.assertEqual('current', json.loads(path.read_text())['archiveMode'])
                self.assertEqual('live', json.loads(path.read_text())['executionMode'])
                folder=output/'drawings'/ident;folder.mkdir(parents=True,exist_ok=True)
                (folder/'view.json').write_text(json.dumps(dict(sourceSha256=sha,modelSha256=refine.dataset.sha256(model))))
                (folder/'bidirectional.gojo.bin.gz').write_bytes(b'native test artifact')
                return [dict(id=ident,ok=True)]
            with patch.object(refine,'select',return_value=[(source,row)]), \
                 patch.object(refine.dataset,'verify_row'), patch.object(refine,'fingerprint',return_value='test'), \
                 patch.object(refine.upstream,'resolve',return_value=root),patch.object(refine.upstream,'fingerprint',return_value={}), \
                 patch.object(refine,'prepare',return_value=({'heuristics':{}},{},{})), \
                 patch.object(refine,'build',return_value=['native']) as build, \
                 patch.object(refine,'execute_batch',side_effect=execute) as native,patch.object(refine,'write') as render:
                render.side_effect=RuntimeError('interrupted after a completed drawing')
                with self.assertRaisesRegex(RuntimeError,'interrupted'):
                    refine.run(source,output,root,limit=1,model=model,work=work)
                self.assertTrue((output/'drawings'/ident/'receipt.json').is_file())
                state=json.loads((output/'study.json').read_text())
                self.assertEqual('current',state['config']['archiveMode'])
                self.assertEqual('live',state['config']['executionMode'])
                render.side_effect=None
                second=refine.run(source,output,root,limit=1,model=model,work=work)
                self.assertEqual(1,second['resumed']);self.assertEqual(1,native.call_count);self.assertEqual(1,build.call_count)
                self.assertEqual(0,second['timingMs']['nativeWorker'])
                with self.assertRaisesRegex(ValueError,'another selection'):
                    refine.run(source,output,root,limit=1,model=model,work=work,archive_mode='history',execution_mode='audit')
                with self.assertRaisesRegex(ValueError,'current state only'):
                    refine.run(source,output,root,limit=1,model=model,work=work,archive_mode='history')
                opened=refine.review(output)
                self.assertEqual(1,opened['completed']);self.assertEqual(1,native.call_count)
                (output/'proposals'/(ident+'.json')).write_text('{}')
                with self.assertRaisesRegex(ValueError,'changed'):
                    refine.run(source,output,root,limit=1,model=model,work=work)
            with gzip.open(source/'source.gz','rt') as stream:self.assertEqual(packet,json.load(stream))


if __name__=='__main__':unittest.main()
