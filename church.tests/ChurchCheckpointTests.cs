using System.Text.Json;
using Gojo.Core;
using Gojo.Equations;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using GojoQuickDraw.ChurchStudy;
using GojoQuickDraw.Learning;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy.Tests;

public class ChurchCheckpointTests
{
    private static JsonElement Json(string text)=>JsonDocument.Parse(text).RootElement.Clone();
    private static Graph Ink()=>FromJson(Json("""{"sites":[[10,60],[180,60],[180,210]],"edges":[{"a":0,"b":1},{"a":1,"b":2}]}"""));
    private static JsonElement Layout()=>Json("""{"parts":[{"id":"wall","role":"building","status":"proposed","bounds":[22,60,180,210],"geometry":{"points":[[22,60],[180,60],[180,210],[22,210]],"segments":[[0,1],[1,2],[2,3],[3,0]]}}]}""");
    private static Dictionary<string,IElement> Profiles(Graph ink)=>ink.Edges.Select((e,i)=>(Id:"p"+i,e.Source)).ToDictionary(p=>p.Id,p=>p.Source);
    private static double[][] Geometry(Relation geometry)=>F(F(MachineRunner.Run(geometry))[2]).Select(Gojo.Experiments.ConstructionMachines.Program.XY).ToArray();

    [Fact] public void CurrentSelectionPreservesEveryVisualStageAndIsMuchSmallerThanHistory()
    {
        var ink=Ink();var bundle=Json(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"providers.json")));
        var history=ChurchExperiment.Run(ink.Surface,ink,Layout(),Json("""{"targets":[]}"""),Profiles(ink),bundle,"fixture","bidirectional",3,2,16);
        var current=ChurchExperiment.Run(ink.Surface,ink,Layout(),Json("""{"targets":[]}"""),Profiles(ink),bundle,"fixture","bidirectional",3,2,16,archiveMode:"current");
        var h=Json(JsonSerializer.Serialize(history.View,GojoQuickDraw.ChurchStudy.Program.Json));
        var c=Json(JsonSerializer.Serialize(current.View,GojoQuickDraw.ChurchStudy.Program.Json));
        Assert.Equal(h.GetProperty("stages").GetRawText(),c.GetProperty("stages").GetRawText());
        var bytes=GojoNumericFormat.Encode(current.Surface);var old=GojoNumericFormat.Encode(history.Surface);
        Assert.True(bytes.Length<old.Length/2,$"Current={bytes.Length}; history={old.Length}");
        var restored=(Relation)GojoNumericFormat.Decode(bytes)[0];ChurchCheckpoint.Validate(restored);
        var checkpoint=ChurchCheckpoint.Restore(restored);
        Assert.Equal(c.GetProperty("stages").EnumerateArray().Last().GetProperty("graph").GetProperty("edges").GetArrayLength(),checkpoint.Current.Edges.Length);
        Assert.NotEmpty(checkpoint.Obligations);Assert.Contains("does not resume",ChurchCheckpoint.LossContract);
    }

    [Fact] public void RestoredCurrentMachineRespondsIdenticallyToAFreshNativeRequest()
    {
        var ink=Ink();var scene=new ChurchScene(ink,ink,Layout(),true,true);scene.BindProfiles(Profiles(ink));
        var part=scene.Parts[0];scene.Add("Shift","mapping",ChurchScene.Coordinate(40),ChurchScene.Coordinate(40),4,ink.Surface,(ChurchScene.Left(part),1));
        for(var i=0;i<3;i++)ChurchSettlement.Step(scene);
        var saved=ChurchCheckpoint.Capture(scene,3);
        var state=ChurchCheckpoint.Restore((Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(saved))[0]);
        var live=part.Machine;var loaded=state.Machines[^1];
        var before=Geometry((Relation)F(state.Parts[0])[0]);
        Assert.Equal(scene.Points(part).SelectMany(p=>p).Select(v=>v*4+512),before.SelectMany(p=>p));
        Relation Request(NegotiatingMachine m)
        {
            var port=m.Port(0,0);var own=m.Publish(port);var axis=((CarrierLocation)F(port)[0]).Carrier;
            var target=Equation.PredictWitness(new Carrier(),Equation.Sum(axis,Equation.UnitPort(axis),Equation.UnitPort(axis)),new Relation(F(own)[2],new CarrierLocation(axis,8)),axis);
            return EvidenceObligation.Signal(port,own,target,G()).Request;
        }
        Assert.True(live.Apply([Request(live)],out var a));Assert.True(loaded.Apply([Request(loaded)],out var b));
        Assert.Equal(live.Points.SelectMany(p=>Gojo.Experiments.ConstructionMachines.Program.XY(p)),loaded.Points.SelectMany(p=>Gojo.Experiments.ConstructionMachines.Program.XY(p)));
        Assert.Equal(NegotiatingMachine.Outcomes(a).Select(o=>ChurchScene.Read(o.Accepted)),NegotiatingMachine.Outcomes(b).Select(o=>ChurchScene.Read(o.Accepted)));
        Assert.NotEqual(before.SelectMany(p=>p),Geometry((Relation)F(state.Parts[0])[0]).SelectMany(p=>p));
    }

    [Fact] public void IndependentEqualSitesStaySeparateAndUnobservedSourcesStayOpen()
    {
        var ink=FromJson(Json("""{"sites":[[10,10],[40,10],[10,10],[10,40]],"edges":[{"a":0,"b":1},{"a":2,"b":3}]}"""));
        var scene=new ChurchScene(ink,ink,Layout(),false,false);scene.BindProfiles(Profiles(ink));
        var saved=ChurchCheckpoint.Capture(scene,1);
        var state=ChurchCheckpoint.Restore((Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(saved))[0]);
        Assert.Equal(2,Components(state.Current));Assert.NotSame(state.Current.Edges[0].A,state.Current.Edges[1].A);
        Assert.Equal(XY(state.Current.Edges[0].A),XY(state.Current.Edges[1].A));
        Assert.All(state.Current.Sites,s=>Assert.IsType<Placeholder>(s.Left));
        Assert.All(state.Current.Edges,e=>Assert.Contains(state.Source.Edges,s=>ReferenceEquals(e.Source,s.Source)));
    }

    [Fact] public void CapturedSharedBoundsAndCurrentErrorDemandsRestoreWithoutTheirOldImports()
    {
        var ink=Ink();var layout=Json("""
            {"parts":[
            {"id":"wall","role":"building","status":"proposed","bounds":[20,60,180,210],"geometry":{"points":[[20,60],[180,60],[180,210],[20,210]],"segments":[[0,1],[1,2],[2,3],[3,0]]}},
            {"id":"roof","role":"roof","status":"proposed","bounds":[20,20,180,58],"geometry":{"points":[[20,58],[100,20],[180,58]],"segments":[[0,1],[1,2],[2,0]]}}]}
            """);
        var plan=Json("""{"rules":[],"knots":[],"partitions":[],"connections":{"startPass":1,"recipes":[{"id":"contact","captureDistance":12,"releasePressure":24,"bindings":[{"target":{"part":"roof","coordinate":3},"terms":[{"part":"wall","coordinate":1,"coefficient":1}]}]}]}}""");
        var controller=new ChurchCaptureController();var scene=new ChurchScene(ink,ink,layout,true,true,hypotheses:plan,capture:controller,pass:1);scene.BindProfiles(Profiles(ink));
        var round=ChurchSettlement.Step(scene);scene.PublishGraph(round.Surface);scene.PrepareDerived(plan);
        var root=ChurchCheckpoint.Capture(scene,1);var state=ChurchCheckpoint.Restore((Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(root))[0]);
        var samples=state.Machines.SelectMany(m=>F(m.CurrentPublication).Cast<Relation>()).ToDictionary(p=>(Relation)F(p)[0],p=>p,(IEqualityComparer<Relation>)ReferenceEqualityComparer.Instance);
        var liveSamples=scene.Machines.SelectMany(m=>F(m.CurrentPublication).Cast<Relation>()).ToDictionary(p=>(Relation)F(p)[0],p=>p,(IEqualityComparer<Relation>)ReferenceEqualityComparer.Instance);
        Assert.Equal(scene.Rules.Select(r=>ChurchScene.Read(EvidenceObligation.Demand(r.Error.Assess(liveSamples)))),state.Obligations.Select(r=>ChurchScene.Read(EvidenceObligation.Demand(r.Assess(samples)))));
        var compound=state.Machines.Single(m=>m.Ports.Count>8);
        Assert.All(state.Parts.SelectMany(p=>F(F(p)[1])).Cast<Relation>(),port=>Assert.Contains(compound.Ports,p=>ReferenceEquals(p,port)));
        var wall=Geometry((Relation)F(state.Parts[0])[0]);var roof=Geometry((Relation)F(state.Parts[1])[0]);
        Assert.Equal(wall[0][1],roof[0][1]);Assert.Equal(roof[0][1],roof[2][1]);
    }

    [Fact] public void LocalOnlyCheckpointHasNoInventedShapeMachines()
    {
        var ink=Ink();var bundle=Json(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"providers.json")));
        var run=ChurchExperiment.Run(ink.Surface,ink,Layout(),Json("""{"targets":[]}"""),Profiles(ink),bundle,"fixture","local-models",2,1,16,archiveMode:"current");
        var restored=(Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(run.Surface))[0];ChurchCheckpoint.Validate(restored);
        var state=ChurchCheckpoint.Restore(restored);Assert.Empty(state.Parts);Assert.Equal(state.Current.Sites.Length,state.Machines.Length);
    }

    [Fact] public void InferredClosureKeepsItsTopologyAndAnExplicitUnobservedSourceSlot()
    {
        var ink=FromJson(Json("""{"sites":[[10,10],[110,10],[110,110],[10,110],[10,15]],"edges":[{"a":0,"b":1},{"a":1,"b":2},{"a":2,"b":3},{"a":3,"b":4}]}"""));
        var closure=NeighborhoodClosure.Propose(ink,ink.Sites[0],ink.Sites[^1]);Assert.NotNull(closure);
        var graph=NeighborhoodClosure.Candidate(closure).After;
        var scene=new ChurchScene(ink,graph,Layout(),false,false);scene.BindProfiles(Profiles(ink));
        var root=(Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(ChurchCheckpoint.Capture(scene,1)))[0];ChurchCheckpoint.Validate(root);
        var state=ChurchCheckpoint.Restore(root);var unknown=Assert.Single(F(F(root)[7]));Assert.IsType<Placeholder>(unknown);
        var inferred=Assert.Single(state.Current.Edges,e=>ReferenceEquals(e.Source,unknown));
        Assert.Contains(state.Current.Sites,p=>ReferenceEquals(p,inferred.A));Assert.Contains(state.Current.Sites,p=>ReferenceEquals(p,inferred.B));
        Assert.Equal(Cycles(graph),Cycles(state.Current));
    }
}
