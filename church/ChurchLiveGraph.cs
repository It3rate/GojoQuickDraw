using System.Numerics;
using Gojo.Core;
using Gojo.Equations;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using GojoQuickDraw.Learning;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy;

/// <summary>A separately owned, live publication of admitted ink topology.
/// Coordinate imports retarget its existing TickRefs; acquired sites never
/// change. Native affine maps connect each published coordinate to accepted
/// machine displacement, including interpolated curve handles. No ancestry is
/// attached to the publication sites or accumulated between publications.</summary>
public sealed class ChurchLiveGraph
{
    private sealed record Input(NegotiatingMachine Machine,Relation Port,Relation Map);
    private sealed record Route(CarrierLocation Target,CarrierLocation Origin,Input[] Inputs,Relation Surface);
    private readonly List<Route> routes=[];
    private readonly Edge[] admittedEdges;
    private Relation? surface;
    public Graph Graph {get;private set;}
    public Relation Surface=>surface??throw new InvalidOperationException("Live graph ports are not bound.");
    public Relation? CurrentPublication {get;private set;}

    public ChurchLiveGraph(Graph admitted)
    {
        var sites=new Dictionary<Relation,Relation>(ReferenceEqualityComparer.Instance);
        Relation Own(Relation source)
        {
            if(!sites.TryGetValue(source,out var copy))
                sites.Add(source,copy=Site(admitted.Frame,XY(source),source));
            return copy;
        }
        admittedEdges=admitted.Edges.Select(edge=>
        {
            var a=Own(edge.A);var b=Own(edge.B);
            // Handles are observation sites, not contacts. Reuse only actual
            // source identity; equal coordinates do not merge independent sites.
            var handles=edge.Handles.Select(Own).ToArray();
            var witness=ReadWitness.Create(new Carrier(),new Relation(edge.Source,edge.Span),admitted.Frame,
                G(new Relation(a,b),G(handles)),G(),edge.Witness);
            return new Edge(witness,edge.Source,edge.Span,a,b,handles,[XY(a),..handles.Select(XY),XY(b)]);
        }).ToArray();
        Graph=Create(admitted.Frame,admittedEdges);
    }

    public void Bind(IReadOnlyDictionary<Relation,NegotiatingMachine> sites)
    {
        if(surface is not null)throw new InvalidOperationException("Live graph ports are bound once during admission.");
        CarrierLocation Coordinate(Relation site,int axis)=>(CarrierLocation)(axis==0?((Relation)site.Right).Left:((Relation)site.Right).Right);
        Input From(Relation site,int axis,long numerator,long denominator,Carrier target)
        {
            var machine=sites[site];var port=machine.Port(0,axis);
            ((Relation)F(port)[1]).TryReadSegment(out var basis,out _,out _);
            var publication=machine.Publish(port);
            if(!ProjectionResidue.TryRead(F(publication)[2],out _,out var n,out var d)||d<=0||n%d!=0)
                throw new InvalidDataException("Initial live point needs an exact finite machine coordinate.");
            var start=checked((long)(n/d));
            // Displacement only. The exact acquisition-coordinate remainder
            // lives in Origin rather than being rounded into machine state.
            var map=Equation.Affine(basis,start,checked(start+ChurchScene.Scale*denominator),target,0,checked(PatchEvidence.Q*numerator));
            return new(machine,port,map);
        }
        void Add(Relation site,int axis,Input[] inputs)
        {
            var target=Coordinate(site,axis);var origin=new CarrierLocation(target.Carrier,target.Ticks);
            var relation=G(new Relation(target,target.Carrier),origin,
                G(inputs.Select(i=>(IElement)new Relation(i.Port,i.Map)).ToArray()));
            routes.Add(new(target,origin,inputs,relation));
        }
        foreach(var site in Graph.Sites)
            for(var axis=0;axis<2;axis++)
                Add(site,axis,[From(site,axis,1,1,Coordinate(site,axis).Carrier)]);
        foreach(var edge in admittedEdges)
            for(var i=0;i<edge.Handles.Length;i++)
                for(var axis=0;axis<2;axis++)
                {
                    var handle=edge.Handles[i];var carrier=Coordinate(handle,axis).Carrier;
                    var count=edge.Handles.Length+1;
                    Add(handle,axis,[From(edge.A,axis,count-i-1,count,carrier),From(edge.B,axis,i+1,count,carrier)]);
                }
        surface=G(Graph.Surface,G(routes.Select(r=>(IElement)r.Surface).ToArray()));
    }

    public Graph Publish()
    {
        _=Surface;
        var captures=new Dictionary<Relation,Relation>(ReferenceEqualityComparer.Instance);
        foreach(var input in routes.SelectMany(r=>r.Inputs))
            if(!captures.ContainsKey(input.Port))captures.Add(input.Port,input.Machine.Publish(input.Port));
        var reads=new List<IElement>();
        foreach(var route in routes)
        {
            var basis=route.Target.Carrier;IElement total=route.Origin;var trace=new List<IElement>();
            foreach(var input in route.Inputs)
            {
                var sample=captures[input.Port];
                var displacement=Equation.PredictWitness(new Carrier(),input.Map,F(sample)[2],basis);
                var sum=Equation.PredictWitness(new Carrier(),Equation.Sum(basis,Equation.UnitPort(basis),Equation.UnitPort(basis)),
                    new Relation(total,F(displacement)[2]),basis);
                total=F(sum)[2];trace.Add(G(sample,displacement,sum));
            }
            if(!ProjectionResidue.TryRead(total,out var actual,out var n,out var d)||!actual.SameIdentityAs(basis)||d<=0)
                throw new InvalidDataException("Live coordinate import lost its affine basis.");
            var whole=BigInteger.DivRem(n,d,out var remainder);
            // Nearest representable publication tick, with ties to even, and
            // explicit remainder. This is a read-resolution decision, never an
            // extra displacement sent to the machine's material parameters.
            if(BigInteger.Abs(remainder)*2>d||(BigInteger.Abs(remainder)*2==d&&!whole.IsEven))whole+=Math.Sign(n);
            var tick=checked((long)whole);
            var before=new CarrierLocation(basis,route.Target.Ticks);
            var after=new CarrierLocation(basis,tick);
            var residue=ProjectionResidue.Create(basis,checked((long)(n-whole*d)),d);
            // Explicit structural output import. Only the independently owned
            // publication TickRef changes; physical parameters changed earlier
            // through NegotiatingMachine's signed-tension settlement.
            route.Target.Tick.Set(tick);
            reads.Add(ReadWitness.Create(new Carrier(),G(before,G(trace.ToArray())),route.Surface,after,residue,G()));
        }
        CurrentPublication=G(reads.ToArray()); // replace, never append history
        // Edge.Points are a convenience read cache, not additional geometry.
        // The native root, endpoint sites, edge witnesses and handle sites stay.
        Graph=Graph with {Edges=admittedEdges.Select(e=>e with {Points=[XY(e.A),..e.Handles.Select(XY),XY(e.B)]}).ToArray()};
        return Graph;
    }
}
