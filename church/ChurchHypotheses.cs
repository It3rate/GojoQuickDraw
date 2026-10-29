using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using GojoQuickDraw.Learning;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy;

/// <summary>Import generic affine opinions through actual exposed ports.
/// Semantic expert names do not select runtime behavior.</summary>
public static class ChurchHypotheses
{
    public static Relation Bound(ChurchScene.Part part,int coordinate)=>coordinate switch
    {0=>ChurchScene.Left(part),1=>ChurchScene.Top(part),2=>ChurchScene.Right(part),3=>ChurchScene.Bottom(part),_=>throw new InvalidDataException("Unknown exposed bound")};

    public static void Link(ChurchScene scene,JsonElement plan)
    {
        foreach(var rule in plan.GetProperty("rules").EnumerateArray())
        {
            var terms=rule.GetProperty("terms").EnumerateArray().Select(t=>
            {
                var part=scene.Parts.Single(p=>p.Id==t.GetProperty("part").GetString());
                var port=Bound(part,t.GetProperty("coordinate").GetInt32());
                var coefficient=checked((long)Math.Round(t.GetProperty("coefficient").GetDouble()*256));
                return (Port:port,Coefficient:coefficient);
            }).ToArray();
            var offset=ChurchScene.Offset*terms.Sum(t=>t.Coefficient);
            long Limit(string key)=>checked((long)Math.Round(rule.GetProperty(key).GetDouble()*ChurchScene.Scale*256)+offset);
            scene.AddCalibrated(rule.GetProperty("name").GetString()!,rule.GetProperty("layer").GetString()!,Limit("lower"),Limit("upper"),
                rule.GetProperty("influence").GetInt64(),G(terms.Select(t=>(IElement)t.Port).ToArray()),256,terms);
        }
    }

    public static (Graph Graph,Relation[] Witnesses) SplitKnots(Graph graph,JsonElement plan,IReadOnlyDictionary<string,IElement> profiles)
    {
        var result=new List<Relation>();
        foreach(var knot in plan.GetProperty("knots").EnumerateArray())
        {
            if(!profiles.TryGetValue(knot.GetProperty("span").GetString()!,out var source))throw new InvalidDataException("Unknown corner profile");
            var t=knot.GetProperty("t").GetDouble();
            var edge=graph.Edges.FirstOrDefault(e=>ReferenceEquals(e.Source,source)&&e.Span.TryReadSegment(out _,out var a,out var b)&&
                t>a.Ticks/(double)PatchEvidence.Q+.005&&t<b.Ticks/(double)PatchEvidence.Q-.005);
            if(edge is null)continue;
            edge.Span.TryReadSegment(out _,out var start,out var end);
            var u=(t*PatchEvidence.Q-start.Ticks)/(end.Ticks-start.Ticks);
            var point=Site(graph.Frame,PatchGeometry.Bezier(edge.Points,u),G(edge.Witness,edge.Span));
            var cut=Cut(graph,edge,u,point);
            var next=Create(graph.Frame,graph.Edges.Where(e=>!ReferenceEquals(e.Witness,edge.Witness)).Concat([cut.Left,cut.Right]));
            var witness=ReadWitness.Create(new Carrier(),graph.Surface,G(edge.Witness,point),next.Surface,G(),G(new Relation(edge.Witness,G(cut.Left.Witness,cut.Right.Witness))));
            result.Add(witness);graph=next;
        }
        return (graph,result.ToArray());
    }

    public static void LinkKnots(ChurchScene scene,JsonElement plan)
    {
        foreach(var knot in plan.GetProperty("knots").EnumerateArray())
        {
            var source=scene.ProfileIds[knot.GetProperty("span").GetString()!];var t=knot.GetProperty("t").GetDouble();
            var part=scene.Parts.Single(p=>p.Id==knot.GetProperty("part").GetString());
            var uv=part.Normalized[knot.GetProperty("point").GetInt32()];
            var ends=scene.Graph.Edges.Where(e=>ReferenceEquals(e.Source,source)).SelectMany(e=>
            {e.Span.TryReadSegment(out _,out var a,out var b);return new[]{(e.A,T:a.Ticks/(double)PatchEvidence.Q),(e.B,T:b.Ticks/(double)PatchEvidence.Q)};});
            foreach(var site in ends.Where(p=>Math.Abs(p.T-t)<.002).Select(p=>p.Item1).Distinct<Relation>(ReferenceEqualityComparer.Instance))
                for(var axis=0;axis<2;axis++)
                {
                    var u=(long)Math.Round(uv[axis]*256);var lo=Bound(part,axis);var hi=Bound(part,axis+2);
                    scene.AddCalibrated("Interior corner / source interval knot","corner",-256,256,3,G(site,part.Geometry,source),256,
                        (scene.Sites[site].Port(0,axis),256),(lo,-256+u),(hi,-u));
                }
        }
    }
}
