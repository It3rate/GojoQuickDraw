import unittest

from tools.analyze_church_duration import geometry, source_movement


def edge(start,end,lo,hi,source='p0',inferred=False):
    return dict(controls=[start,end],interval=[lo,hi],source=source,inferred=inferred)


class DurationAnalysisTests(unittest.TestCase):
    def test_motion_uses_original_parameters_across_split_edges(self):
        initial=dict(edges=[edge([0,0],[100,0],0,1)])
        before=dict(edges=[edge([0,0],[50,0],0,.5),edge([50,0],[100,0],.5,1)])
        after=dict(edges=[edge([0,3],[50,3],0,.5),edge([50,3],[100,3],.5,1)])
        result=source_movement(initial,before,after)
        self.assertEqual(17,result['covered']);self.assertEqual(3,result['mean']);self.assertEqual(0,result['missing'])

    def test_missing_participation_and_inferred_lines_do_not_become_zero_error(self):
        initial=dict(edges=[edge([0,0],[100,0],0,1)])
        before=dict(edges=[edge([25,0],[75,0],.25,.75)])
        after=dict(edges=[edge([37.5,2],[62.5,2],.375,.625),edge([0,0],[1000,0],0,1,'closure',True)])
        result=source_movement(initial,before,after)
        self.assertEqual(5,result['covered']);self.assertEqual(12,result['missing']);self.assertEqual(2,result['mean'])

    def test_changing_view_ids_does_not_count_as_visible_motion(self):
        a=dict(graph=dict(edges=[dict(controls=[[0,0],[10,0]],id='e0')],sites=[dict(point=[0,0],kind='L',id='s0')]),parts=[])
        b=dict(graph=dict(edges=[dict(controls=[[0,0],[10,0]],id='other')],sites=[dict(point=[0,0],kind='L',id='new')]),parts=[])
        self.assertEqual(geometry(a),geometry(b))
        b['graph']['sites'][0]['point']=[0,1]
        self.assertNotEqual(geometry(a),geometry(b))


if __name__=='__main__':unittest.main()
