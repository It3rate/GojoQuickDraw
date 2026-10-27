using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Experiments.QuickDraw;
using Gojo.Structures;
using static GojoQuickDraw.Learning.PatchEvidence;
using static GojoQuickDraw.Learning.NeighborhoodGraph;
using static GojoQuickDraw.Learning.NeighborhoodProposals;

namespace GojoQuickDraw.Learning;

public static class NeighborhoodWeak
{
    public sealed record Goal(Graph Graph,string Kind,QuickDrawAnnotationEvidence.Joint[] SourceJoints,int[] Profiles);
    public sealed record Export(Relation Surface,List<NeighborhoodLearning.Example> Repairs,int Goals,int Unsupported,int Ambiguous);
    public static Export Build(JsonElement request)
    {
        var annotationSurface=QuickDrawAnnotationEvidence.Import(request.GetProperty("packet"));
        if(!QuickDrawAnnotationEvidence.TryRead(annotationSurface,out var annotation))throw new InvalidDataException("Weak source annotations do not read.");
        var graph=FromAnnotation(annotation);var sourceHash=request.GetProperty("sourceHash").GetString()!;
        var provenance=EncodedTextSurface.Create(new Carrier(),request.GetProperty("evidenceSha256").GetString()!);
        var goals=new List<Goal>();
        foreach(var input in request.GetProperty("goals").EnumerateArray())
        {
            var joints=input.GetProperty("sourceJoints").EnumerateArray().Select(p=>annotation.Joints[p.GetInt32()]).ToArray();
            var rays=input.GetProperty("rays").EnumerateArray().ToArray();
            var frame=Frame();var center=Site(frame,[0,0],G(provenance,G(joints.Select(j=>(IElement)j.Witness).ToArray())));var edges=new List<Edge>();var profiles=new HashSet<int>();
            foreach(var ray in rays)
            {
                var indices=ray.GetProperty("profiles").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                if(indices.Length==0||indices.Any(i=>i<0||i>=annotation.Profiles.Length))throw new InvalidDataException("A weak goal contains an unsupported branch.");
                foreach(var i in indices)profiles.Add(i);
                var angle=ray.GetProperty("direction").GetDouble()*Math.PI/180;
                var selected=G(indices.Select(i=>(IElement)annotation.Profiles[i].Witness).ToArray());
                var far=Site(frame,[Math.Cos(angle),Math.Sin(angle)],selected);
                edges.Add(Curve(frame,selected,Relation.Segment(new Carrier(),0,Q),center,far,[],provenance));
            }
            var target=Create(frame,edges);goals.Add(new(target,Kind(target,center),joints,profiles.ToArray()));
        }
        var rows=new List<IElement>();var examples=new List<NeighborhoodLearning.Example>();var unsupported=0;var ambiguous=0;
        var generated=Generate(graph,160);
        foreach(var c in generated.Where(c=>!c.Straightening))
        {
            var members=c.Focus.Edges.Select(e=>Array.FindIndex(annotation.Profiles,p=>ReferenceEquals(p.Witness,e.Source))).ToHashSet();
            var applicable=goals.Where(g=>g.Profiles.All(members.Contains)&&g.SourceJoints.All(j=>j.Participants.Length>0&&
                PatchGeometry.Distance(c.Focus.Center,[j.Participants.Average(p=>p.Position.X/(double)QuickDrawAnnotationEvidence.CoordinateQuantum),j.Participants.Average(p=>p.Position.Y/(double)QuickDrawAnnotationEvidence.CoordinateQuantum)])<=c.Focus.Radius)).ToArray();
            if(applicable.Length!=1){if(applicable.Length>1)ambiguous++;continue;}
            var goal=applicable[0];
            int Cost(Graph value)
            {
                var kinds=value.Sites.Where(s=>PatchGeometry.Distance(XY(s),c.Focus.Center)<=c.Focus.Radius)
                    .Select(s=>Kind(value,s)).Where(k=>k!="endpoint").ToArray();
                return kinds.Count(k=>k!=goal.Kind)+Math.Abs(kinds.Count(k=>k==goal.Kind)-1);
            }
            // A final T is not a label for two unsupported source branches.
            // A complete candidate must actually realize its required incidence.
            if(!generated.Any(p=>p.Admissible&&!p.Straightening&&p.Focus.Edges.Any(e=>c.Focus.Edges.Any(x=>ReferenceEquals(x.Witness,e.Witness)))&&
                p.After.Sites.Any(s=>PatchGeometry.Distance(XY(s),c.Focus.Center)<=c.Focus.Radius&&Kind(p.After,s)==goal.Kind)))
            {unsupported++;continue;}
            var before=Cost(graph);var after=Cost(c.After);if(before==after)continue;
            var label=c.Admissible&&after<before?1:0;var cells=NeighborhoodFeatures.Repair(c);var axis=new Carrier();
            var row=ReadWitness.Create(new Carrier(),new Relation(c.Surface,cells.Surface),goal.Graph.Surface,new CarrierLocation(axis,label),G(),graph.Surface);
            rows.Add(G(graph.Surface,goal.Graph.Surface,G(),G(row)));
            examples.Add(new(cells.Bands,label,sourceHash,"weak-final-complete-rays",goal.Kind,0,examples.Count,before,after));
        }
        return new(G(rows.ToArray()),examples,goals.Count,unsupported,ambiguous);
    }
}
