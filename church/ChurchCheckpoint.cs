using Gojo.Core;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using GojoQuickDraw.Learning;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy;

/// <summary>A qualified current-state selection, not a historical run archive.
/// Actual native definitions, response routes, error laws, live carrier slots
/// and shared sites survive. Historical proof paths are explicitly absent.</summary>
public static class ChurchCheckpoint
{
    // G(version, acquired graph, current graph, machines, parts, obligations,
    // current-site/port correspondence, inferred-source slots, derived programs,
    // G(executed pass, omitted-history slots), all imported source profiles).
    // No field is a domain opcode.
    public const string LossContract = "Current native state only: executable geometry, shared parameter/port identities, current source intervals and error laws are retained. Historical proposal alternatives, discarded intervals, transfer receipts, prior construction imports and per-pass provenance are omitted. Unobserved closure origins stay open. This checkpoint accepts fresh local numeric requests; it does not resume the adaptive topology/capture controller or replay earlier passes. Visual stages remain separate observations.";
    public static readonly string LiveLossContract = LossContract.Replace("transfer receipts, ", "older transfer receipts, ")+
        " Each machine's latest native response accounting is retained with actual capacity/actuator references and explicit omitted explanation slots; detailed request derivations and preview traces are not retained. This is current evidence, not a response history. Only initial and final visual observations are retained. Restoration exposes local numeric machines and current laws; it does not recreate the whole live scene scheduler/publication bridge.";
    public sealed record State(Graph Source,Graph Current,NegotiatingMachine[] Machines,Relation[] Parts,
        EvidenceObligation[] Obligations,Relation[] Derived,IElement[] UnobservedSources,IElement[] SourceProfiles);

    public static Relation Capture(ChurchScene scene,int pass,bool retainCurrentResponses=false)
    {
        var omission=new Placeholder();
        var sites=new Dictionary<Relation,Relation>(ReferenceEqualityComparer.Instance);
        Relation SiteNow(Relation site)
        {
            if(!sites.TryGetValue(site,out var selected))
                sites.Add(site,selected=new Relation(omission,site.Right));
            return selected;
        }
        var inferred=new Dictionary<IElement,IElement>(ReferenceEqualityComparer.Instance);
        var edges=scene.Graph.Edges.Select(edge=>
        {
            IElement source=edge.Source;
            if(NeighborhoodClosure.IsInferred(source))
            {
                if(!inferred.TryGetValue(source,out var slot))inferred.Add(source,slot=new Placeholder());
                source=slot;
            }
            var a=SiteNow(edge.A);var b=SiteNow(edge.B);var controls=edge.Handles.Select(SiteNow).ToArray();
            var witness=ReadWitness.Create(new Carrier(),new Relation(source,edge.Span),scene.Graph.Frame,
                G(new Relation(a,b),G(controls)),G(),omission);
            return new Edge(witness,source,edge.Span,a,b,controls,edge.Points);
        }).ToArray();
        var current=Create(scene.Graph.Frame,edges);
        var machines=scene.Machines.Select(machine=>
        {
            var f=F(machine.Surface);var clock=(Carrier)f[4];
            // Selected state starts a new observation epoch at the CURRENT
            // clock value. It does not invent an empty timeline since time zero.
            var timeline=G(Relation.Point(clock,checked((long)clock.Value)),f[5]);
            var latest=retainCurrentResponses&&machine.LastReceipt is {} response?G(CurrentAccounting(response,omission)):G();
            return G(f[0],f[1],f[2],f[3],f[4],f[5],timeline,latest,f[8],
                MachineRunner.Run(machine.Definition),f[10],f[11]);
        }).ToArray();
        Relation Geometry(Relation geometry)
        {
            var f=F(geometry);
            // Geometry expressions already reference their actual parameters.
            // The old input/provenance envelopes also referenced old machines;
            // expose the expressions and output roles without those ancestors.
            return G(G(),f[1],f[2],f[3],G(F(f[4])[0],omission));
        }
        var parts=scene.Parts.Select(p=>(IElement)G(Geometry(p.Geometry),
            G(ChurchScene.Left(p),ChurchScene.Top(p),ChurchScene.Right(p),ChurchScene.Bottom(p)),
            G(p.Segments.Select(e=>(IElement)new Relation(F(F(p.Geometry)[1])[e[0]],F(F(p.Geometry)[1])[e[1]])).ToArray()))).ToArray();
        var obligations=scene.Rules.Select(r=>
        {
            var f=F(r.Error.Surface);return (IElement)G(omission,f[1],f[2],f[3],f[4],f[5]);
        }).ToArray();
        // PublishGraph may have replaced the ink-site identity. Sites are
        // matched by edge endpoint correspondence, never by equal coordinates.
        var correspondence=new List<IElement>();
        foreach(var endpoint in scene.Graph.Sites)
        {
            var actor=scene.Sites.TryGetValue(endpoint,out var direct)?direct:null;
            if(actor is null&&endpoint.Left is Relation origin&&origin.TryReadOrderedGroup(out var originFields)&&
                originFields.Length==3&&originFields[0] is Relation previous)
                scene.Sites.TryGetValue(previous,out actor);
            if(actor is null)throw new InvalidDataException("A current ink site has no source-linked numeric actor.");
            correspondence.Add(G(SiteNow(endpoint),actor.Port(0,0),actor.Port(0,1)));
        }
        var derived=scene.DerivedParts.Length==0?[]:F(F(scene.Imports[^1])[1]).Cast<Relation>().Select(Geometry).ToArray();
        var version=new Carrier();var effort=new Carrier();
        var root=G(Relation.Segment(version,0,1),scene.Source.Surface,current.Surface,G(machines),G(parts),G(obligations),
            G(correspondence.ToArray()),G(inferred.Values.ToArray()),G(derived),
            G(new CarrierLocation(effort,pass),G(omission)),G(scene.ProfileIds.Values.ToArray()));
        Validate(root);return root;
    }

    private static Relation CurrentAccounting(Relation response,Placeholder omission)
    {
        var fields=F(response);
        if(fields.Length!=5||fields[2] is not Relation output||!output.TryReadOrderedGroup(out var result)||result.Length!=2)
            return response; // Preserve an unusual rejected/unsupported response in full.
        var outcomes=F(result[1]).Select(row=>
        {
            var f=F(row);var beforeAfter=F(f[4]);
            // Select native quantities, capacity and before/after sites without
            // copying the request forest or the private candidate world. The
            // open input/provenance explicitly says why reconstruction stops.
            return (IElement)ReadWitness.Create(new Carrier(),omission,f[1],f[2],f[3],
                G(beforeAfter[0],beforeAfter[1],omission));
        }).ToArray();
        return ReadWitness.Create(new Carrier(),omission,fields[1],G(result[0],G(outcomes)),fields[3],omission);
    }

    public static State Restore(Relation root)
    {
        var f=F(root);
        if(f.Length!=11||f[0] is not Relation version||!version.TryReadSegment(out _,out var zero,out var one)||zero.Ticks!=0||one.Ticks!=1)
            throw new InvalidDataException("Unsupported current church checkpoint.");
        return new(Read((Relation)f[1]),Read((Relation)f[2]),F(f[3]).Cast<Relation>().Select(NegotiatingMachine.Restore).ToArray(),
            F(f[4]).Cast<Relation>().ToArray(),F(f[5]).Cast<Relation>().Select(EvidenceObligation.Restore).ToArray(),F(f[8]).Cast<Relation>().ToArray(),F(f[7]),F(f[10]));
    }

    public static void Validate(Relation root)
    {
        var state=Restore(root);var f=F(root);var ports=state.Machines.SelectMany(m=>m.Ports).ToHashSet<Relation>(ReferenceEqualityComparer.Instance);
        var samples=state.Machines.SelectMany(m=>F(m.CurrentPublication).Cast<Relation>()).ToDictionary(p=>(Relation)F(p)[0],p=>p,(IEqualityComparer<Relation>)ReferenceEqualityComparer.Instance);
        foreach(var part in state.Parts)
        {
            var p=F(part);if(p.Length!=3||F(p[1]).Length!=4||F(p[1]).Any(v=>v is not Relation port||!ports.Contains(port)))
                throw new InvalidDataException("A checkpoint part lost its actual exposed bounds.");
            VerifyGeometry((Relation)p[0]);
        }
        foreach(var geometry in state.Derived)VerifyGeometry(geometry);
        foreach(var obligation in state.Obligations)
        {
            if(obligation.Terms.Any(t=>!ports.Contains((Relation)F(t)[0])))throw new InvalidDataException("A checkpoint error lost its public port.");
            _=obligation.Assess(samples); // Unresolved reads remain permissible.
        }
        var linked=new HashSet<IElement>(ReferenceEqualityComparer.Instance);
        foreach(var row in F(f[6]))
        {
            var c=F(row);if(c.Length!=3||!linked.Add(c[0])||!state.Current.Sites.Any(site=>ReferenceEquals(site,c[0]))||c.Skip(1).Any(v=>v is not Relation port||!ports.Contains(port)))
                throw new InvalidDataException("A current ink site lost its actual port correspondence.");
        }
        if(linked.Count!=state.Current.Sites.Length)throw new InvalidDataException("A current ink site is disconnected from its retained actor.");
        var inferred=F(f[7]).ToHashSet(ReferenceEqualityComparer.Instance);
        if(state.Current.Edges.Any(e=>!inferred.Contains(e.Source)&&!state.Source.Edges.Any(s=>ReferenceEquals(s.Source,e.Source))))
            throw new InvalidDataException("A current interval lost its acquired source or explicit unobserved origin.");
        var profiles=F(f[10]).ToHashSet(ReferenceEqualityComparer.Instance);
        if(state.Source.Edges.Any(e=>!profiles.Contains(e.Source)))throw new InvalidDataException("The current checkpoint lost its imported profile identity.");
        if(F(f[9]).Length!=2||F(F(f[9])[1]).Length==0)throw new InvalidDataException("The checkpoint omitted its history-loss contract.");
    }

    private static void VerifyGeometry(Relation geometry)
    {
        var result=F(MachineRunner.Run(geometry));
        if(F(result[4]).Length!=0||F(result[2]).Any(v=>!MachineRunner.TryNumbers(v,out _)))
            throw new InvalidDataException("A current construction cannot reproduce its geometry.");
    }
}
