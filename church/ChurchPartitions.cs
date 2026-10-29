using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy;

/// <summary>Conditional rectangle decomposition from current PUBLIC bounds.
/// Cells are derived observations, not independent actors with copied targets.</summary>
public static class ChurchPartitions
{
    private sealed record Bound(Relation Port,Relation Publication,double Value);
    public sealed record CellView(string Id,string Role,bool Prior,double[][] Points,int[][] Segments,double[] Bounds);
    public static (object[] Views,Relation Surface) Build(ChurchScene scene,JsonElement plan)
    {
        var views=new List<object>();var witnesses=new List<IElement>();var constructions=new List<IElement>();
        Bound[] Bounds(string id)
        {
            var part=scene.Parts.Single(p=>p.Id==id);
            return Enumerable.Range(0,4).Select(k=>{var port=ChurchHypotheses.Bound(part,k);var pub=part.Machine.Publish(port);return new Bound(port,pub,ChurchScene.Read(F(pub)[2]));}).ToArray();
        }
        foreach(var partition in plan.GetProperty("partitions").EnumerateArray())
        {
            var id=partition.GetProperty("body").GetString()!;var body=Bounds(id);var cells=new List<Bound[]>{body};
            foreach(var cutter in partition.GetProperty("cutters").EnumerateArray())
            {
                var cut=Bounds(cutter.GetString()!);var next=new List<Bound[]>();
                foreach(var cell in cells)
                {
                    var l=cell[0].Value>=cut[0].Value?cell[0]:cut[0];var t=cell[1].Value>=cut[1].Value?cell[1]:cut[1];
                    var r=cell[2].Value<=cut[2].Value?cell[2]:cut[2];var b=cell[3].Value<=cut[3].Value?cell[3]:cut[3];
                    if(r.Value<=l.Value||b.Value<=t.Value){next.Add(cell);continue;}
                    foreach(var piece in new[]{new[]{cell[0],cell[1],cell[2],t},new[]{cell[0],b,cell[2],cell[3]},
                        new[]{cell[0],t,l,b},new[]{r,t,cell[2],b}})
                        if(piece[2].Value>piece[0].Value&&piece[3].Value>piece[1].Value)next.Add(piece);
                }
                witnesses.Add(ReadWitness.Create(new Carrier(),G(cells.Select(c=>(IElement)G(c.Select(v=>(IElement)v.Publication).ToArray())).ToArray()),
                    G(cut.Select(v=>(IElement)v.Publication).ToArray()),G(next.Select(c=>(IElement)G(c.Select(v=>(IElement)v.Port).ToArray())).ToArray()),G(),G()));
                cells=next;
            }
            foreach(var (cell,i) in cells.Select((c,i)=>(c,i)))
            {
                IElement Coordinate(Bound bound,int axis)
                {
                    ((Relation)F(bound.Port)[1]).TryReadSegment(out var basis,out _,out _);
                    return scene.Space.Times(scene.Space.Point(axis==0?1:0,axis==1?1:0),F(bound.Publication)[2],basis);
                }
                var expressions=new[]{(0,1),(2,1),(2,3),(0,3)}.Select(p=>(IElement)scene.Space.Plus(Coordinate(cell[p.Item1],0),Coordinate(cell[p.Item2],1))).ToArray();
                var geo=TransformSurface.Create(new Carrier(),G(cell.Select(v=>(IElement)v.Publication).ToArray()),G(expressions),
                    G(expressions.Select(_=>(IElement)new Placeholder()).ToArray()),G(),G(scene.Space.Basis));
                var executed=MachineRunner.Run(geo);var points=F(F(executed)[2]);
                var outline=G(Enumerable.Range(0,4).Select(k=>(IElement)new Relation(points[k],points[(k+1)%4])).ToArray());
                witnesses.Add(G(geo,executed,outline));
                constructions.Add(geo);
                var xy=points.Select(p=>Gojo.Experiments.ConstructionMachines.Program.XY(p).Select(x=>(x-ChurchScene.Offset)/ChurchScene.Scale).ToArray()).ToArray();
                views.Add(new CellView(id+"-cell-"+i,"building",false,xy,[[0,1],[1,2],[2,3],[3,0]],cell.Select(v=>(v.Value-ChurchScene.Offset)/ChurchScene.Scale).ToArray()));
            }
        }
        return (views.ToArray(),G(G(witnesses.ToArray()),G(constructions.ToArray())));
    }
}
