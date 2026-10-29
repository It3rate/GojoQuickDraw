import copy
import unittest
from gojo_quickdraw.church_cad import extend
from gojo_quickdraw.church_heuristics import compile_plan
from gojo_quickdraw.layout import estimate
from test_layout import church


def part(ident,bounds,parent=None,role='tower'):
    a,t,b,d=bounds
    return dict(id=ident,bounds=bounds,parent=parent,role=role,shape='rectangle',status='source-supported',spans=[ident+'-ink'],
        geometry=dict(points=[[a,t],[b,t],[b,d],[a,d]],segments=[[0,1],[1,2],[2,3],[3,0]]))


def build(parts,pairs=(),front=True,planned=None,symmetries=()):
    proposed=copy.deepcopy({p['id']:p for p in parts})
    for key,bounds in (planned or {}).items():proposed[key]['bounds']=bounds
    rules=[];connections=dict(recipes=[],startPass=3)
    cad=extend(parts,proposed,{p['id']:p['id'] for p in parts},
        dict(view=dict(selected='front-like' if front else 'side-or-oblique'),symmetries=list(symmetries)),
        rules,dict(pairs=list(pairs)),connections)
    return cad,rules,connections


def assembly():
    parts=[part('body',[0,100,400,400],role='building'),part('upper',[150,10,270,100],'body'),
           part('door',[170,280,230,400],'body','door'),part('left',[60,180,100,230],'body','window'),
           part('right',[300,180,340,230],'body','window')]
    pair=dict(left='left',right='right',axisPart='door')
    return parts,pair


class ChurchCadTests(unittest.TestCase):
    def test_supported_overhang_can_propose_continuation_without_merging_segment_identity(self):
        parts=[part('body',[100,200,400,450],role='building'),part('upper',[80,80,220,200],'body')]
        before=copy.deepcopy(parts)
        cad,rules,connections=build(parts,front=False,planned={'upper':[100,80,240,200]})
        self.assertEqual(before,parts)
        recipe=connections['recipes'][0];self.assertEqual('collinearity',recipe['family'])
        self.assertEqual([0,2],[b['target']['coordinate'] for b in recipe['bindings']])
        self.assertEqual(dict(part='upper',coordinate=4,coefficient=1),recipe['bindings'][1]['terms'][1])
        self.assertEqual(['cad-collinearity'],[r['layer'] for r in rules])
        self.assertEqual(['body','upper'],sorted(recipe['supportParts']))

    def test_intentional_inset_and_contained_window_margin_are_not_boundary_continuation(self):
        parts=[part('body',[0,100,400,400],role='building'),part('upper',[30,0,180,100],'body'),
               part('window',[5,180,60,230],'body','window')]
        _,rules,connections=build(parts)
        self.assertFalse(rules);self.assertFalse(connections['recipes'])

    def test_source_motif_proposes_outward_axis_without_counting_duplicate_routes(self):
        parts,pair=assembly()
        cad,rules,connections=build(parts,[pair,pair])
        decision=next(d for d in cad['decisions'] if d['kind']=='outward assembly symmetry')
        self.assertTrue(decision['proposed']);self.assertEqual(1,len(decision['evidenceGroups']))
        self.assertIn('cad-symmetry',{r['layer'] for r in rules})
        self.assertEqual(['body','door','left','right','upper'],sorted(connections['recipes'][0]['supportParts']))

    def test_shared_ink_alone_cannot_supply_two_motif_members(self):
        parts,pair=assembly();parts[-1]['spans']=parts[-2]['spans']
        cad,_,connections=build(parts,[pair])
        self.assertFalse(connections['recipes'])
        self.assertFalse(next(d for d in cad['decisions'] if d['kind']=='outward assembly symmetry')['proposed'])

    def test_side_view_or_two_upper_frames_do_not_center_every_frame(self):
        for side,twins in ((True,False),(False,True)):
            parts,pair=assembly()
            if twins:parts.append(part('other',[280,0,350,100],'body'))
            _,_,connections=build(parts,[pair],front=not side)
            self.assertFalse([r for r in connections['recipes'] if r['family']=='shared assembly axis'])

    def test_regular_row_has_local_spacing_and_spanning_extent_opinions(self):
        parts=[part('body',[0,100,500,400],role='building')]+[
            part('w'+str(i),[x,180,x+w,230],'body','window') for i,(x,w) in enumerate(((50,40),(155,42),(265,39),(370,41)))]
        mirror=dict(left='w0',right='w3',axisPart='body')
        cad,rules,_=build(parts,symmetries=[mirror])
        self.assertEqual(1,len(cad['groups']))
        self.assertEqual(2,sum(r['layer']=='cad-spacing' for r in rules))
        # Four members need three spanning links; one existing mirror already
        # supplies one, leaving two sets of width/height/head opinions.
        self.assertEqual(6,sum(r['layer']=='cad-repetition' for r in rules))
        for rule in rules:self.assertEqual(0,sum(t['coefficient'] for t in rule['terms']))

    def test_irregular_row_is_retained_as_an_alternative(self):
        parts=[part('body',[0,100,600,400],role='building')]+[
            part('w'+str(i),[x,180,x+40,230],'body','window') for i,x in enumerate((40,100,430))]
        cad,rules,_=build(parts)
        self.assertFalse(cad['groups']);self.assertFalse(rules)

    def test_unselected_cad_preserves_the_existing_connected_plan(self):
        observed=church();layout=estimate(observed)
        before=compile_plan(observed,layout,True)
        self.assertEqual(before,compile_plan(observed,layout,True,False))
        self.assertNotIn('cad',before[2])
        with self.assertRaises(ValueError):compile_plan(observed,layout,False,True)


if __name__=='__main__':unittest.main()
