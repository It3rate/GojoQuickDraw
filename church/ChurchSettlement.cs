using Gojo.Core;
using Gojo.Encoding;
using Gojo.Equations;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy;

/// <summary>Synchronous observations, local error responses, explicit passage
/// admission, then native carrier tension. No global minimum is selected.</summary>
public static class ChurchSettlement
{
    public sealed record ErrorView(string Name,string Layer,double Demand,bool Resolved);
    public sealed record Round(Relation Surface, ErrorView[] Before, ErrorView[] After, int Moved,
        double Requested, double Accepted, double Blocked, double Deferred, double Quantum, double Rejected);
    private static Dictionary<Relation,Relation> Capture(ChurchScene scene) => scene.Machines
        .SelectMany(m=>F(m.CurrentPublication).Cast<Relation>()).ToDictionary(p=>(Relation)F(p)[0],p=>p,(IEqualityComparer<Relation>)ReferenceEqualityComparer.Instance);
    public static ErrorView[] Errors(ChurchScene scene) => Read(scene, Capture(scene)).Views;
    private static (Relation[] Reads,ErrorView[] Views) Read(ChurchScene scene,Dictionary<Relation,Relation> samples)
    {
        var reads=scene.Rules.Select(r=>r.Error.Assess(samples)).ToArray();
        return (reads,scene.Rules.Zip(reads,(r,read)=>
        {
            var resolved=EvidenceObligation.Resolved(read);
            var norm=Math.Sqrt(r.Error.Terms.Select(t=>
            {
                var map=(Relation)F(t)[1];((Relation)map.Right).TryReadSegment(out _,out var a,out var b);return Math.Pow(b.Ticks-a.Ticks,2);
            }).Sum());
            return new ErrorView(r.Name,r.Layer,resolved?ChurchScene.Read(EvidenceObligation.Demand(read))/Math.Max(1,norm)/ChurchScene.Scale:0,resolved);
        }).ToArray());
    }
    public static Round Step(ChurchScene scene,Dictionary<Relation,IElement>? memory=null,long cap=12)
    {
        if(cap<1||cap>96)throw new ArgumentOutOfRangeException(nameof(cap));
        var samples=Capture(scene);var before=Read(scene,samples);var occasion=G(samples.Values.Cast<IElement>().ToArray());
        var plans=scene.Rules.Select((r,i)=>r.Error.Propose(samples,before.Reads[i],
            memory is not null&&memory.TryGetValue(r.Error.Surface,out var previous)?previous:null,occasion)).ToArray();
        var requests=plans.SelectMany(EvidenceObligation.Requests).ToArray();
        var admitted=new List<Relation>();var passages=new List<IElement>();
        foreach(var group in requests.GroupBy(r=>(Relation)F(r)[0],(IEqualityComparer<Relation>)ReferenceEqualityComparer.Instance))
        {
            var port=group.Key;
            if(!((Relation)F(port)[1]).TryReadSegment(out var axis,out _,out _))throw new InvalidDataException("A receiver lost its public displacement frame.");
            foreach(var request in group)
            {
                var f=F(request);if(!ProjectionResidue.TryRead(f[3],out var actual,out var n,out var d)||!actual.SameIdentityAs(axis))
                    throw new InvalidDataException("A church passage received a foreign displacement basis.");
                var limit=Relation.Segment(axis,-cap,cap);var value=(double)n/(double)d;
                // A finite rational read, with explicit rejected participation.
                IElement amount=value < -cap?new CarrierLocation(axis,-cap):value>cap?new CarrierLocation(axis,cap):f[3];
                var difference=Equation.PredictWitness(new Carrier(),Equation.Difference(axis,axis,axis),new Relation(amount,f[3]),axis);
                var clip=ReadWitness.Create(new Carrier(),request,limit,amount,F(difference)[2],occasion);
                var target=Equation.PredictWitness(new Carrier(),Equation.Sum(axis,Equation.UnitPort(axis),Equation.UnitPort(axis)),new Relation(F(f[1])[2],amount),axis);
                var signal=EvidenceObligation.Signal(port,(Relation)f[1],target,G(request,clip));
                admitted.Add(signal.Request);passages.Add(G(clip,signal.Witness));
            }
        }
        var receipts=new List<IElement>();var moved=0;var totals=new double[6];
        foreach(var machine in scene.Machines)
        {
            var addressed=admitted.Where(r=>machine.Ports.Any(p=>ReferenceEquals(p,F(r)[0]))).ToArray();
            if(addressed.Length==0)continue;
            if(!machine.Apply(addressed,out var receipt))throw new InvalidDataException("A captured church batch became stale.");
            receipts.Add(receipt);
            foreach(var outcome in NegotiatingMachine.Outcomes(receipt))
            {
                var values=new[]{outcome.Requested,outcome.Accepted,outcome.BoundsBlocked,outcome.CapacityDeferred,outcome.QuantumRemainder,outcome.CandidateBlocked};
                var read=values.Select(v=>ProjectionResidue.TryRead(v,out _,out var n,out var d)?(double)n/(double)d:double.NaN).ToArray();
                if(read.Any(v=>!double.IsFinite(v))||Math.Abs(read[0]-read.Skip(1).Sum())>1e-7)
                    throw new InvalidDataException("Incoming displacement was not accounted for at its receiver.");
                if(Math.Abs(read[1])>.001)moved++;
                for(var i=0;i<read.Length;i++)totals[i]+=Math.Abs(read[i])/ChurchScene.Scale;
            }
        }
        var after=Read(scene,Capture(scene));
        if(memory is not null)for(var i=0;i<scene.Rules.Count;i++)if(EvidenceObligation.Resolved(before.Reads[i]))memory[scene.Rules[i].Error.Surface]=EvidenceObligation.Value(before.Reads[i]);
        var effects=scene.Rules.Select((r,i)=>(IElement)r.Error.Response(before.Reads[i],after.Reads[i],G(receipts.ToArray()))).ToArray();
        var surface=G(G(before.Reads),G(plans),G(passages.ToArray()),G(receipts.ToArray()),G(after.Reads),G(effects));
        return new(surface,before.Views,after.Views,moved,totals[0],totals[1],totals[2],totals[3],totals[4],totals[5]);
    }
}
