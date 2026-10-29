using System.Numerics;
using System.Text.Json;
using Gojo.Core;
using Gojo.Equations;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;
using static GojoQuickDraw.ChurchStudy.ChurchBoundConstruction;

namespace GojoQuickDraw.ChurchStudy;

/// <summary>Exploratory capture/hold/release controller. Broad attraction is
/// separate. A captured relationship changes the actual construction and its
/// response routes. Large uncoupled requested displacement can reopen it.</summary>
public sealed class ChurchCaptureController
{
    private sealed class State
    {
        public readonly Carrier Active=new();
        public Relation? Pressure;
        public int Cooldown;
    }
    public sealed record View(string Id,string Action,bool Active,double Distance,double Pressure,bool ReleasePending=false);
    private readonly Dictionary<string,State> states=[];
    private readonly List<(Recipe Recipe,State State,double Distance)> selected=[];

    private static EvidenceObligation Probe(ChurchScene scene,Binding binding,double distance)
    {
        var terms=new List<(Relation Port,long Coefficient)>();
        void Add(Slot slot,double coefficient)
        {
            var part=scene.Parts.Single(p=>p.Id==slot.Part);var n=checked((long)Math.Round(coefficient*256));
            if(slot.Coordinate<4)terms.Add((ChurchHypotheses.Bound(part,slot.Coordinate),n));
            else {terms.Add((ChurchHypotheses.Bound(part,slot.Coordinate-2),n));terms.Add((ChurchHypotheses.Bound(part,slot.Coordinate-4),-n));}
        }
        Add(binding.Target,1);foreach(var term in binding.Terms)Add(term.Slot,-term.Coefficient);
        var combined=terms.GroupBy(t=>t.Port,(IEqualityComparer<Relation>)ReferenceEqualityComparer.Instance)
            .Select(g=>(Port:g.Key,Coefficient:g.Sum(t=>t.Coefficient))).Where(t=>t.Coefficient!=0).ToArray();
        var norm=Math.Sqrt(combined.Sum(t=>(double)t.Coefficient*t.Coefficient));
        var limit=checked((long)Math.Round(distance*ChurchScene.Scale*norm));
        return new(combined,-limit,limit,[2,2,2],1,1,G(combined.Select(t=>(IElement)t.Port).ToArray()));
    }
    private static Dictionary<Relation,Relation> Samples(ChurchScene scene)=>scene.Machines.SelectMany(m=>F(m.CurrentPublication).Cast<Relation>())
        .ToDictionary(p=>(Relation)F(p)[0],p=>p,(IEqualityComparer<Relation>)ReferenceEqualityComparer.Instance);
    private static double Number(IElement value)=>ProjectionResidue.TryRead(value,out _,out var n,out var d)?(double)n/(double)d:double.NaN;
    private static bool Violated(Relation? witness)=>witness is not null&&F(witness)[2] is Relation claim&&
        OperationClaim.TryReadResolvedBetweenViolation(claim,out var demand)&&demand.Ticks>0;

    public void Begin(ChurchScene scene,JsonElement plan,int pass)
    {
        if(!plan.TryGetProperty("connections",out var setup))return;
        selected.Clear();var views=new List<View>();var proposals=new List<Recipe>();var reads=new List<IElement>();var samples=Samples(scene);
        foreach(var row in setup.GetProperty("recipes").EnumerateArray())
        {
            var id=row.GetProperty("id").GetString()!;
            if(!states.TryGetValue(id,out var state))states[id]=state=new State();
            var held=state.Active.Value==1;var release=held&&Violated(state.Pressure);
            if(release){reads.Add(Carrier.ReceiveTogether(G(new CarrierLocation(state.Active,-1))));state.Cooldown=pass+1;held=false;}
            var bindings=ReadBindings(row);var radius=row.GetProperty("captureDistance").GetDouble();
            var probes=bindings.Select(b=>Probe(scene,b,radius)).ToArray();var assessed=probes.Select(p=>p.Assess(samples)).ToArray();
            var near=assessed.All(r=>EvidenceObligation.Resolved(r)&&Number(EvidenceObligation.Demand(r))==0);
            var distance=probes.Zip(assessed,(p,r)=>EvidenceObligation.Resolved(r)?Math.Abs(Number(EvidenceObligation.Value(r)))/
                ChurchScene.Scale/Math.Sqrt(p.Terms.Sum(t=>{var f=F(t);((Relation)((Relation)f[1]).Right).TryReadSegment(out _,out var a,out var b);return Math.Pow(b.Ticks-a.Ticks,2);})):double.NaN).DefaultIfEmpty(0).Max();
            var eligible=pass>=setup.GetProperty("startPass").GetInt32()&&(held||near&&pass>state.Cooldown);
            var recipe=new Recipe(id,bindings,row.Clone(),held);
            if(eligible){proposals.Add(recipe);selected.Add((recipe,state,distance));}
            reads.Add(ReadWitness.Create(new Carrier(),G(assessed.Cast<IElement>().ToArray()),G(state.Active,new CarrierLocation(state.Active,held?1:0)),
                eligible?G(probes.Select(p=>(IElement)p.Surface).ToArray()):new Placeholder(),
                G(state.Pressure??(IElement)new Placeholder()),G()));
            views.Add(new(id,release?"released":eligible?held?"held":"capture proposed":"open",held,distance,0));
        }
        var composed=Compose(scene,proposals);scene.Imports.Add(composed.Witness);
        var accepted=composed.Accepted.ToHashSet();
        foreach(var item in selected)
        {
            var before=(long)item.State.Active.Value;var active=accepted.Contains(item.Recipe.Id);
            reads.Add(Carrier.ReceiveTogether(G(new CarrierLocation(item.State.Active,(active?1:0)-before))));
            reads.Add(ReadWitness.Create(new Carrier(),new CarrierLocation(item.State.Active,before),composed.Witness,
                new CarrierLocation(item.State.Active,active?1:0),G(),item.State.Active));
            var i=views.FindIndex(v=>v.Id==item.Recipe.Id);views[i]=views[i] with {Action=active?before==1?"held":"captured":"construction blocked",Active=active};
        }
        selected.RemoveAll(p=>!accepted.Contains(p.Recipe.Id));scene.Imports.Add(G(reads.ToArray()));scene.CaptureViews=views.Cast<object>().ToArray();
    }

    public void ObservePressure(ChurchScene scene,ChurchSettlement.Round round)
    {
        if(selected.Count==0)return;
        var requests=F(F(round.Surface)[1]).Cast<Relation>().SelectMany(EvidenceObligation.Requests).ToArray();
        var sums=new Dictionary<Relation,(IElement Value,Relation Witness)>(ReferenceEqualityComparer.Instance);
        foreach(var group in requests.GroupBy(r=>(Relation)F(r)[0],(IEqualityComparer<Relation>)ReferenceEqualityComparer.Instance))
        {
            var port=group.Key;((Relation)F(port)[1]).TryReadSegment(out var axis,out _,out _);IElement sum=new CarrierLocation(axis,0);var trace=new List<IElement>();
            foreach(var request in group){var read=Equation.PredictWitness(new Carrier(),Equation.Sum(axis,Equation.UnitPort(axis),Equation.UnitPort(axis)),new Relation(sum,F(request)[3]),axis);sum=F(read)[2];trace.Add(read);}
            sums[port]=(sum,ReadWitness.Create(new Carrier(),G(group.Cast<IElement>().ToArray()),F(port)[1],sum,G(),G(trace.ToArray())));
        }
        var views=scene.CaptureViews!.Cast<View>().ToArray();
        foreach(var item in selected)
        {
            var reads=new List<IElement>();var max=0d;Relation? strongest=null;
            foreach(var binding in item.Recipe.Bindings)
            {
                var probe=Probe(scene,binding,0);var axis=probe.Axis;IElement sum=new CarrierLocation(axis,0);var trace=new List<IElement>();
                foreach(var term in probe.Terms)
                {
                    var f=F(term);var port=(Relation)f[0];if(!sums.TryGetValue(port,out var signal))continue;
                    var mapped=Equation.PredictWitness(new Carrier(),(Relation)f[1],signal.Value,axis);
                    var added=Equation.PredictWitness(new Carrier(),Equation.Sum(axis,Equation.UnitPort(axis),Equation.UnitPort(axis)),new Relation(sum,F(mapped)[2]),axis);
                    sum=F(added)[2];trace.Add(G(signal.Witness,mapped,added));
                }
                var norm=Math.Sqrt(probe.Terms.Sum(t=>{((Relation)((Relation)F(t)[1]).Right).TryReadSegment(out _,out var a,out var b);return Math.Pow(b.Ticks-a.Ticks,2);}));
                // Pressure is an advisory uncoupled displacement read. Four is
                // the selected receiver damping, not a physical force unit.
                var pressure=Math.Abs(Number(sum))/norm/ChurchScene.Scale/4;
                if(!ProjectionResidue.TryRead(sum,out _,out var n,out var d)||d>long.MaxValue||BigInteger.Abs(n)>long.MaxValue)continue;
                var limit=checked((long)Math.Round(item.Recipe.Input.GetProperty("releasePressure").GetDouble()*norm*ChurchScene.Scale*4));
                if((BigInteger)limit*d>long.MaxValue)continue;
                var fine=new Carrier(UnitShape.SubdivisionOf(axis.UnitShape,(long)d));
                var map=Equation.Affine(axis,0,1,fine,0,(long)d);
                var projected=Equation.PredictWitness(new Carrier(),map,sum,fine);
                var claim=OperationClaim.Between(new Carrier(),F(projected)[2],new CarrierLocation(fine,checked((long)(-limit*d))),new CarrierLocation(fine,checked((long)(limit*d))),new Carrier(fine.UnitShape));
                var witness=ReadWitness.Create(new Carrier(),G(trace.ToArray()),G(probe.Surface,map),claim,G(),projected);
                reads.Add(witness);if(pressure>=max){max=pressure;strongest=witness;}
            }
            item.State.Pressure=strongest;
            var i=Array.FindIndex(views,v=>v.Id==item.Recipe.Id);views[i]=views[i] with {Pressure=max,ReleasePending=Violated(strongest)};
            scene.Imports.Add(G(reads.ToArray()));
        }
        scene.CaptureViews=views.Cast<object>().ToArray();
    }
}
