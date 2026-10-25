from copy import deepcopy
import unittest

from gojo_quickdraw.object_display import COLORS, profile_color, read


class ObjectDisplayTests(unittest.TestCase):
    def test_shared_assembly_is_not_an_extra_direct_role_and_conflicts_stay_unknown(self):
        packet = {"observation": {"spans": [{"id": key} for key in ("wall", "left", "right", "ambiguous", "loose")]},
                  "objects": {"candidates": [
                      {"id": "w", "role": "wall", "spans": ["wall"]},
                      {"id": "b", "role": "building", "focusKind": "assembly", "spans": ["wall", "left", "right"]},
                      {"id": "a", "role": "window", "spans": ["left"]},
                      {"id": "c", "role": "window", "spans": ["right"]},
                      {"id": "d", "role": "door", "spans": ["ambiguous"]},
                      {"id": "e", "role": "window", "spans": ["ambiguous"]}],
                      "unknownSpanIds": ["loose"], "ambiguousSpanIds": ["ambiguous"]}}
        before = deepcopy(packet)
        view = read(packet)
        self.assertEqual(view["spans"]["wall"]["role"], "wall")
        self.assertEqual(view["spans"]["wall"]["candidates"], ["w", "b"])
        self.assertEqual(view["spans"]["ambiguous"]["role"], "unknown")
        self.assertEqual(view["spans"]["loose"]["color"], COLORS["unknown"])
        self.assertEqual(profile_color(view, ["left", "right"]), COLORS["window"])
        self.assertEqual(profile_color(view, ["wall", "left"]), COLORS["unknown"])
        self.assertEqual(profile_color(view, []), COLORS["unknown"])
        self.assertEqual(packet, before)

    def test_old_packets_do_not_claim_object_classification(self):
        self.assertFalse(read({"observation": {"spans": [{"id": "a"}]}})["available"])


if __name__ == "__main__":
    unittest.main()
