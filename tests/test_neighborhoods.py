import copy
import json
from pathlib import Path
import tempfile
import unittest

from gojo_quickdraw import consolidation, neighborhood_data, neighborhood_models, neighborhood_report, training_data
from gojo_quickdraw.neighborhood_training import source_packet


class NeighborhoodTests(unittest.TestCase):
    def test_templates_split_before_their_four_distortions(self):
        cases=[c for batch in neighborhood_data.controls(288) for c in batch['cases']]
        groups={c['group'] for c in cases}
        self.assertEqual(72,len(groups))
        self.assertTrue(all(sum(c['group']==g for c in cases)==4 for g in groups))
        self.assertTrue(all(len(c['lineTargets'])==len(c['source']['edges']) for c in cases))
        self.assertEqual(18,len({c['kind'] for c in cases}))

    def test_test_labels_cannot_select_weights(self):
        rows=[]
        for i in range(80):
            label=i%2
            rows.append({'bands':[label]*8,'label':label,'group':f'group-{i}','basis':'controlled'})
        model=neighborhood_models.fit(rows,['f']*8,epochs=6)
        changed=copy.deepcopy(rows)
        for row in changed:
            if training_data.split(row['group'],43)=='test':row['label']=1-row['label']
        second=neighborhood_models.fit(changed,['f']*8,epochs=6)
        self.assertEqual(model,second)

    def test_inference_interface_omits_final_interpretations_and_labels(self):
        packet={k:{} for k in ('source','observation','program','annotations')}
        packet.update(sourceOriginalJson='{}',jointComparison='cannot be read',constructionStages='cannot be read')
        minimal=source_packet(packet)
        self.assertNotIn('jointComparison',minimal)
        self.assertNotIn('constructionStages',minimal)
        self.assertEqual(5,len(minimal))

    def test_weak_goal_requires_every_mapped_ray_not_just_two_of_a_tee(self):
        comparison={'source':{'nodes':[{'id':'source-j2'}]},'final':{'nodes':[
            {'id':'f','kind':'T','on':[{'edge':'a'},{'edge':'b'},{'edge':'c'}],'directions':[0,90,180]}]},
            'lineMatches':[{'finalEdge':'a','sourceProfiles':['p0']},{'finalEdge':'b','sourceProfiles':['p1']}],
            'matches':[{'target':'f','candidates':['source-j2']}]}
        goals,skipped=neighborhood_data.weak_goals({'jointComparison':comparison})
        self.assertEqual([],goals);self.assertEqual(1,skipped)
        comparison['lineMatches'].append({'finalEdge':'c','sourceProfiles':['p2']})
        goals,skipped=neighborhood_data.weak_goals({'jointComparison':comparison})
        self.assertEqual(0,skipped);self.assertEqual([2],goals[0]['sourceJoints']);self.assertEqual(3,len(goals[0]['rays']))
        comparison['final']['nodes'][0]['added']=True
        self.assertEqual([],neighborhood_data.weak_goals({'jointComparison':comparison})[0])

    def test_model_and_output_contracts_fail_before_mutating_dataset(self):
        def model(n):return {'schema':'gojo.neighborhood-provider.v1','featureCount':n,'featureNames':['f']*n,'columns':['bias'],'weights':[0.]}
        bundle={'schema':'gojo.neighborhood-models.v1','line':model(8),'repair':model(24)}
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);path=root/'model.json';path.write_text(json.dumps(bundle))
            self.assertEqual(bundle,consolidation.load_model(path))
            for value in (float('inf'),True,'bad'):
                invalid=copy.deepcopy(bundle);invalid['repair']['weights']=[value];path.write_text(json.dumps(invalid))
                with self.assertRaises(ValueError):consolidation.load_model(path)
            source=root/'drawings';source.mkdir()
            with self.assertRaises(ValueError):consolidation.run(source,source/'inside','unused',model=path)
            self.assertEqual([],list(source.iterdir()))

    def test_portable_review_embeds_changed_graphs_and_escapes_source_text(self):
        with tempfile.TemporaryDirectory() as temp:
            output=Path(temp)
            neighborhood_report.write(output,'Repair','<p>results</p>',[{'sourceKey':'</script><script>bad()', 'stages':[]}])
            page=(output/'report.html').read_text(encoding='utf-8')
            self.assertNotIn('</script><script>bad()',page)
            self.assertIn('\\u003c/script>',page)
            self.assertIn('g.curves',page);self.assertIn('farEndpoints',page);self.assertIn('slice(offset,offset+12)',page)

    def test_cli_has_bounded_independently_selectable_providers(self):
        from gojo_quickdraw.cli import parser
        args=parser().parse_args(['consolidate','--dataset','saved','--output','run','--model','m.json','--no-repair'])
        self.assertEqual(25,args.limit);self.assertTrue(args.no_repair);self.assertFalse(args.no_line)
        self.assertEqual(4,args.passes);self.assertEqual(192,args.max_candidates)
        self.assertFalse(args.no_closure);self.assertEqual('recognized',args.recognition)
        args=parser().parse_args(['train-neighborhoods','--dataset','saved','--output','run'])
        self.assertEqual(60,args.limit);self.assertEqual(1800,args.synthetic)
        self.assertEqual('recognized',args.recognition)

    def test_closure_controls_keep_template_groups_and_counterexamples(self):
        cases=[c for b in neighborhood_data.closure_controls(128) for c in b['closureCases']]
        self.assertEqual(8,len({c['kind'] for c in cases}))
        self.assertEqual({0,1},{c['label'] for c in cases})
        self.assertTrue(all(sum(c['group']==g for c in cases)==4 for g in {c['group'] for c in cases}))
        self.assertTrue(all('acquisition' not in c for c in cases))

    def test_processing_cli_defaults_to_game_recognition_but_catalog_can_show_all(self):
        from gojo_quickdraw.cli import parser
        args=parser().parse_args(['process','--input','raw','--output','data','--limit','3'])
        self.assertEqual('recognized',args.recognition)
        args=parser().parse_args(['catalog','--dataset','data'])
        self.assertEqual('all',args.recognition)

    def test_index_cache_can_be_kept_in_a_declared_work_area(self):
        import os
        from unittest.mock import patch
        from gojo_quickdraw import sampling
        with tempfile.TemporaryDirectory() as temp, patch.dict(os.environ,{'GOJO_QUICKDRAW_CACHE':temp}):
            self.assertEqual(Path(temp)/'source-index',sampling._cache_root())


if __name__=='__main__':unittest.main()
