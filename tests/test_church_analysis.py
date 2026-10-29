import importlib.util
from pathlib import Path
import unittest

_spec=importlib.util.spec_from_file_location('church_analysis',Path(__file__).resolve().parents[1]/'tools/analyze_church_study.py')
analysis=importlib.util.module_from_spec(_spec);_spec.loader.exec_module(analysis)


class ChurchAnalysisTests(unittest.TestCase):
    def test_fixed_probes_distinguish_missing_intervals_from_zero_error(self):
        original=dict(edges=[dict(source='p0',controls=[[0,0],[100,0]],interval=[0,1],inferred=False)])
        trimmed=dict(edges=[dict(source='p0',controls=[[25,3],[75,3]],interval=[.25,.75],inferred=False)])
        result=analysis.fixed_samples(original,trimmed)
        self.assertEqual(17,result['total']);self.assertEqual(8,result['missing']);self.assertEqual(9,result['covered'])
        self.assertEqual(3,result['mean'])

    def test_incidence_does_not_join_equal_coordinates(self):
        joined=dict(sites=[dict(id='a',kind='E'),dict(id='b',kind='L'),dict(id='c',kind='E')],edges=[
            dict(source='p0',start='a',end='b',inferred=False),dict(source='p1',start='b',end='c',inferred=False)])
        separate=dict(sites=[dict(id='a',kind='E'),dict(id='b',kind='E'),dict(id='b-copy',kind='E'),dict(id='c',kind='E')],edges=[
            dict(source='p0',start='a',end='b',inferred=False),dict(source='p1',start='b-copy',end='c',inferred=False)])
        self.assertNotEqual(analysis.incidence(joined),analysis.incidence(separate))

    def test_bootstrap_keeps_constant_whole_drawing_difference(self):
        result=analysis.bootstrap([.25]*8)
        self.assertEqual(.25,result['mean']);self.assertEqual([.25,.25],result['percentile95']);self.assertEqual(8,result['drawings'])


if __name__=='__main__':unittest.main()
