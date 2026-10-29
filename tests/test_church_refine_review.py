import copy
import unittest
from gojo_quickdraw.church_refine_review import boundary_fill


class ChurchReviewBoundaryTests(unittest.TestCase):
    def test_each_version_reads_its_own_ring_instead_of_a_later_family_indices(self):
        triangle=dict(points=[[0,10],[5,0],[10,10]],segments=[[0,1],[1,2],[2,0]])
        before=copy.deepcopy(triangle)
        self.assertEqual([0,1,2],boundary_fill(triangle));self.assertEqual(before,triangle)

    def test_open_cross_does_not_acquire_a_synthetic_fill(self):
        self.assertEqual([],boundary_fill(dict(points=[[0,5],[10,5],[5,0],[5,10]],segments=[[0,1],[2,3]])))

    def test_window_panes_do_not_replace_the_outer_enclosure_fill(self):
        part=dict(points=[[0,0],[10,0],[10,10],[0,10],[0,5],[10,5],[5,0],[5,10]],
                  segments=[[0,1],[1,2],[2,3],[3,0],[4,5],[6,7]])
        self.assertEqual([0,1,2,3],boundary_fill(part))


if __name__=='__main__':unittest.main()
