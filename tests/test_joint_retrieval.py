import copy
import unittest
from gojo_quickdraw.joint_retrieval import descriptor, ranks, score


def graph():
    return {'nodes':[{'id':'a','kind':'L'},{'id':'b','kind':'I'},{'id':'c','kind':'T'}],
            'links':[{'from':'a','to':'b','order':'right'},{'from':'b','to':'c','order':'down-right'}],
            'orders':[{'from':'a','to':'c','horizontal':'right','vertical':'below'}]}


class RetrievalTests(unittest.TestCase):
    def test_id_coordinates_provenance_and_enumeration_cannot_identify_pairs(self):
        before=graph();after=copy.deepcopy(before);rename={'a':'zz','b':'yy','c':'aa'}
        for node in after['nodes']:
            node.update(id=rename[node['id']],point=[9999,-2000],sources=['secret-key'],basis='not a feature',added=True)
        for group in ('links','orders'):
            for link in after[group]:link.update({'from':rename[link['from']],'to':rename[link['to']]})
            after[group].reverse()
        after['nodes'].reverse()
        self.assertEqual(descriptor(before),descriptor(after))

    def test_connectivity_and_relative_orders_are_visible(self):
        a=descriptor(graph());other=graph();other['links']=[]
        other['orders'][0]['vertical']='above'
        b=descriptor(other)
        self.assertEqual(a['types'],b['types'])
        self.assertGreater(score(a,a),score(a,b))

    def test_ties_do_not_prefer_the_known_key(self):
        for i in range(20):
            rank,recall,wins=ranks([1]*20,i)
            self.assertEqual(10.5,rank);self.assertEqual(.05,recall['1']);self.assertEqual(.5,wins)


if __name__=='__main__':unittest.main()
