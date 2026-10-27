using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using static GojoQuickDraw.Learning.PatchEvidence;
using static GojoQuickDraw.Learning.NeighborhoodGraph;
using static GojoQuickDraw.Learning.NeighborhoodProposals;

namespace GojoQuickDraw.Learning;

public static class NeighborhoodLearning
{
    public sealed record Example(int[] Bands,int Label,string Group,string Basis,string Kind,int Case,int Candidate,double BeforeCost,double AfterCost);
    public sealed record Batch(Relation Surface,List<Example> Lines,List<Example> Repairs);
    private static int? RouteTarget(Graph target,Edge first,Edge second,Relation at)
    {
        var a=XY(ReferenceEquals(first.A,at)?first.B:first.A);var b=XY(ReferenceEquals(second.A,at)?second.B:second.A);
        var start=target.Sites.MinBy(s=>PatchGeometry.Distance(a,XY(s)))!;var end=target.Sites.MinBy(s=>PatchGeometry.Distance(b,XY(s)))!;
        var reach=target.Edges.Select(e=>Length(e.Points)).DefaultIfEmpty(1).Max();
        if(ReferenceEquals(start,end)||PatchGeometry.Distance(a,XY(start))>.16*reach||PatchGeometry.Distance(b,XY(end))>.16*reach)return null;
        var queue=new Queue<(Relation Site,List<(Edge Edge,Relation From)> Path)>();queue.Enqueue((start,[]));var seen=new HashSet<Relation>(ReferenceEqualityComparer.Instance);
        while(queue.TryDequeue(out var item))
        {
            if(!seen.Add(item.Site))continue;
            if(ReferenceEquals(item.Site,end))
            {
                var samples=item.Path.SelectMany(p=>ReferenceEquals(p.Edge.A,p.From)?Sample(p.Edge.Points,12):Sample(p.Edge.Points,12).Reverse()).ToArray();
                var measured=PatchGeometry.Measure(samples,true);
                return item.Path.All(p=>p.Edge.Handles.Length==0)&&measured.Bend<.015&&measured.Turn<14?1:0;
            }
            foreach(var edge in target.Edges)
            {
                if(ReferenceEquals(edge.A,item.Site))queue.Enqueue((edge.B,[..item.Path,(edge,item.Site)]));
                if(ReferenceEquals(edge.B,item.Site))queue.Enqueue((edge.A,[..item.Path,(edge,item.Site)]));
            }
        }
        return null;
    }
    public static double TargetCost(Graph graph,Graph target)
    {
        var actual=graph.Sites.Select(s=>Kind(graph,s)).Where(k=>k!="endpoint").GroupBy(k=>k).ToDictionary(g=>g.Key,g=>g.Count());
        var expected=target.Sites.Select(s=>Kind(target,s)).Where(k=>k!="endpoint").GroupBy(k=>k).ToDictionary(g=>g.Key,g=>g.Count());
        var topology=actual.Keys.Union(expected.Keys).Sum(k=>Math.Abs(actual.GetValueOrDefault(k)-expected.GetValueOrDefault(k)));
        var a=graph.Edges.SelectMany(e=>Sample(e.Points,8)).ToArray();var b=target.Edges.SelectMany(e=>Sample(e.Points,8)).ToArray();
        var reach=target.Edges.Select(e=>Length(e.Points)).DefaultIfEmpty(1).Max();
        var geometry=a.Length==0||b.Length==0?1:(a.Average(p=>b.Min(q=>PatchGeometry.Distance(p,q)))+b.Average(p=>a.Min(q=>PatchGeometry.Distance(p,q))))/(2*Math.Max(.001,reach));
        return 3*topology+.25*Math.Abs(Components(graph)-Components(target))+geometry;
    }
    public static Batch Build(JsonElement request)
    {
        var cases=new List<IElement>();var lines=new List<Example>();var repairs=new List<Example>();var index=0;
        foreach(var entry in request.GetProperty("cases").EnumerateArray())
        {
            var before=FromJson(entry.GetProperty("source"));var target=FromJson(entry.GetProperty("target"));
            var group=entry.GetProperty("group").GetString()!;var kind=entry.GetProperty("kind").GetString()!;
            var lineTargets=entry.GetProperty("lineTargets").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            if(lineTargets.Length!=before.Edges.Length)throw new InvalidDataException("Controlled line targets lost source coverage.");
            var lineRows=new List<IElement>();
            for(var i=0;i<before.Edges.Length;i++)
            {
                var cells=NeighborhoodFeatures.Line(before.Edges[i]);var axis=new Carrier();
                lineRows.Add(ReadWitness.Create(new Carrier(),cells.Surface,target.Surface,new CarrierLocation(axis,lineTargets[i]),G(),before.Edges[i].Witness));
                lines.Add(new(cells.Bands,lineTargets[i],group,"controlled",kind,index,i,0,0));
            }
            foreach(var site in before.Sites)
            {
                var incident=before.Edges.Where(e=>ReferenceEquals(e.A,site)||ReferenceEquals(e.B,site)).ToArray();
                if(incident.Length>4)continue;
                for(var i=0;i<incident.Length;i++)for(var j=i+1;j<incident.Length;j++)
                {
                    var label=RouteTarget(target,incident[i],incident[j],site);if(label is null)continue;
                    var context=G(incident[i].Witness,incident[j].Witness,site);var cells=NeighborhoodFeatures.Route(before,context);var axis=new Carrier();
                    lineRows.Add(ReadWitness.Create(new Carrier(),cells.Surface,target.Surface,new CarrierLocation(axis,label.Value),G(),context));
                    lines.Add(new(cells.Bands,label.Value,group,"controlled-route",kind,index,-1,0,0));
                }
            }
            var sourceCost=TargetCost(before,target);var candidateRows=new List<IElement>();var candidates=Generate(before,80);
            for(var i=0;i<candidates.Count;i++)
            {
                var candidate=candidates[i];if(candidate.Straightening)continue;
                var cells=NeighborhoodFeatures.Repair(candidate);var cost=TargetCost(candidate.After,target);
                var label=candidate.Admissible&&cost<sourceCost-.2?1:0;var axis=new Carrier();
                candidateRows.Add(ReadWitness.Create(new Carrier(),new Relation(candidate.Surface,cells.Surface),target.Surface,new CarrierLocation(axis,label),G(),before.Surface));
                repairs.Add(new(cells.Bands,label,group,"controlled",kind,index,i,sourceCost,cost));
            }
            cases.Add(G(before.Surface,target.Surface,G(lineRows.ToArray()),G(candidateRows.ToArray())));index++;
        }
        return new(G(cases.ToArray()),lines,repairs);
    }
    public static void Validate(Relation root)
    {
        foreach(var row in Rows(root))
        {
            var f=Fields(row,4);var before=Read((Relation)f[0]);var target=Read((Relation)f[1]);
            var sources=before.Edges.Select(e=>e.Source).ToHashSet<IElement>(ReferenceEqualityComparer.Instance);
            foreach(var row2 in Rows(f[2]))
            {
                if(!ReadWitness.TryRead((Relation)row2,out var cells,out var truth,out var label,out _,out var source)||!ReferenceEquals(truth,target.Surface)||
                    !LineSource(before,source)||label is not CarrierLocation value||value.Ticks is <0 or >1)
                    throw new InvalidDataException("A line target lost its actual source/controlled construction.");
                if(NeighborhoodFeatures.ReadCells((Relation)cells).Length!=NeighborhoodFeatures.LineNames.Length)throw new InvalidDataException("Line feature interface changed.");
            }
            foreach(var row2 in Rows(f[3]))
            {
                if(!ReadWitness.TryRead((Relation)row2,out var input,out var truth,out var label,out _,out var source)||!ReferenceEquals(truth,target.Surface)||!ReferenceEquals(source,before.Surface)||
                    input is not Relation{Left:Relation candidate,Right:Relation cells}||label is not CarrierLocation value||value.Ticks is <0 or >1)
                    throw new InvalidDataException("A repair target lost its local source or construction.");
                if(!ReadWitness.TryRead(candidate,out var old,out _,out var result,out var remainder,out _)||!ReferenceEquals(old,before.Surface))throw new InvalidDataException("A candidate lost its before state.");
                var after=Read((Relation)result);if(after.Edges.Any(e=>!sources.Contains(e.Source)))throw new InvalidDataException("An unsupported source branch appeared.");
                if(NeighborhoodFeatures.ReadCells(cells).Length!=NeighborhoodFeatures.RepairNames.Length)throw new InvalidDataException("Repair feature interface changed.");
                foreach(var removed in Rows(remainder))
                    if(!ReadWitness.TryRead((Relation)removed,out var selected,out _,out _,out _,out _)||selected is not Relation selection||!sources.Contains(selection.Left))
                        throw new InvalidDataException("Trimmed participation lost its source interval.");
            }
        }
    }
    private static bool LineSource(Graph graph,IElement source)
    {
        if(graph.Edges.Any(e=>ReferenceEquals(e.Witness,source)))return true;
        if(source is not Relation r||!r.TryReadOrderedGroup(out var f)||f.Length!=3)return false;
        return graph.Edges.Any(e=>ReferenceEquals(e.Witness,f[0]))&&graph.Edges.Any(e=>ReferenceEquals(e.Witness,f[1]))&&graph.Sites.Any(s=>ReferenceEquals(s,f[2]));
    }
}
