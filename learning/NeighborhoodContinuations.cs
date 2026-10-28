using Gojo.Core;
using Gojo.Structures;
using static GojoQuickDraw.Learning.NeighborhoodGraph;
using static GojoQuickDraw.Learning.PatchEvidence;

namespace GojoQuickDraw.Learning;

// A coarse read, not deletion: two actual incidences expose two outer ports.
// The internal site, ordered source route and its deviation remain available.
public static class NeighborhoodContinuations
{
    public sealed record Read(Relation Witness,Relation Inner,Relation First,Relation Last,Edge[] Edges,double Deviation);
    public sealed record View(string Site,string[] OuterEndpoints,string[] Edges,double Deviation,string Status);
    public static Relation Build(Graph graph)
    {
        var rows=new List<IElement>();
        foreach(var site in graph.Sites.Where(s=>Kind(graph,s)=="I"))
        {
            var edges=graph.Edges.Where(e=>ReferenceEquals(e.A,site)||ReferenceEquals(e.B,site)).ToArray();
            if(edges.Length!=2)continue;
            var first=ReferenceEquals(edges[0].A,site)?edges[0].B:edges[0].A;
            var last=ReferenceEquals(edges[1].A,site)?edges[1].B:edges[1].A;
            if(ReferenceEquals(first,last))continue;
            var path=NeighborhoodFeatures.RoutePoints(edges[0],edges[1],site);
            var deviation=PatchGeometry.Measure(path,true).Bend;var axis=new Carrier();
            var exposed=new Relation(first,last);var route=G(edges.Select(e=>(IElement)e.Witness).ToArray());
            rows.Add(ReadWitness.Create(new Carrier(),G(site,route),G(graph.Frame,Relation.Segment(axis,0,Q)),
                G(exposed,route),new CarrierLocation(axis,checked((long)Math.Round(deviation*Q))),graph.Surface));
        }
        return G(graph.Surface,G(rows.ToArray()));
    }
    public static Read[] ReadAll(Relation surface,Graph graph)
    {
        var f=Fields(surface,2);
        if(!ReferenceEquals(f[0],graph.Surface))throw new InvalidDataException("Continuation read belongs to another graph.");
        return Rows(f[1]).Select(row=>
        {
            if(row is not Relation w||!ReadWitness.TryRead(w,out var source,out var basis,out var result,out var residue,out var prior)||!ReferenceEquals(prior,graph.Surface))
                throw new InvalidDataException("Continuation lost its source graph.");
            var s=Fields(source,2);var b=Fields(basis,2);var r=Fields(result,2);
            if(s[0] is not Relation inner||!graph.Sites.Any(p=>ReferenceEquals(p,inner))||Kind(graph,inner)!="I"||
                !ReferenceEquals(b[0],graph.Frame)||!ReferenceEquals(s[1],r[1])||r[0] is not Relation{Left:Relation first,Right:Relation last}||
                b[1] is not Relation span||!span.TryReadSegment(out var axis,out var lo,out var hi)||lo.Ticks!=0||hi.Ticks!=Q||
                residue is not CarrierLocation loss||!ReferenceEquals(loss.Carrier,axis))throw new InvalidDataException("Continuation lost its inner site, ports or deviation basis.");
            var edges=Rows(s[1]).Select(e=>graph.Edges.SingleOrDefault(p=>ReferenceEquals(p.Witness,e))??throw new InvalidDataException("Continuation lost a source interval.")).ToArray();
            if(edges.Length!=2||!ReferenceEquals(ReferenceEquals(edges[0].A,inner)?edges[0].B:edges[0].A,first)||
                !ReferenceEquals(ReferenceEquals(edges[1].A,inner)?edges[1].B:edges[1].A,last)||ReferenceEquals(first,last)||
                edges.Any(e=>!ReferenceEquals(e.A,inner)&&!ReferenceEquals(e.B,inner))||
                Math.Abs(loss.Ticks/(double)Q-PatchGeometry.Measure(NeighborhoodFeatures.RoutePoints(edges[0],edges[1],inner),true).Bend)>2e-6)
                throw new InvalidDataException("Continuation is not the selected actual two-edge route.");
            return new Read(w,inner,first,last,edges,loss.Ticks/(double)Q);
        }).ToArray();
    }
}
