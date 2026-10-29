using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using GojoQuickDraw.ChurchStudy;
using GojoQuickDraw.Learning;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy.Tests;

public class ChurchStudyTests
{
    private static JsonElement Json(string text)=>JsonDocument.Parse(text).RootElement.Clone();
    private static Graph Ink()=>FromJson(Json("""{"sites":[[10.137,60.183],[180.167,60.229],[180.167,210.229]],"edges":[{"a":0,"b":1},{"a":1,"b":2}]}"""));
    private static Dictionary<string,IElement> Profiles(Graph ink)=>ink.Edges.Select((e,i)=>(Id:"p"+i,e.Source)).ToDictionary(p=>p.Id,p=>p.Source);
    private static JsonElement Layout()=>Json("""{"parts":[{"id":"wall","role":"building","status":"proposed","bounds":[22,60,180,210],"geometry":{"points":[[22,60],[180,60],[180,210],[22,210]],"segments":[[0,1],[1,2],[2,3],[3,0]],"siteSources":[null,null,null,null]},"participants":[]}]}""");

    [Fact] public void ReadingCoarselyDoesNotMoveFixedInkAndKeepsItsIdentity()
    {
        var ink=Ink();var world=new ChurchScene(ink,ink,Layout(),true,true,moveInk:false);
        var original=XY(ink.Sites[0]);var p=world.Sites[ink.Sites[0]].Port(0,0);
        world.Add("Disagreement","mapping",ChurchScene.Coordinate(90),ChurchScene.Coordinate(90),4,ink.Surface,(p,1));
        var step=ChurchSettlement.Step(world);
        Assert.True(step.Deferred>0);Assert.Same(ink.Surface,world.PublishGraph(step.Surface).Surface);
        Assert.Equal(original,XY(ink.Sites[0]));
        var correspondence=F(world.Correspondence[ink.Sites[0]]);
        Assert.True(ReadWitness.TryRead((Relation)correspondence[3],out _,out _,out _,out var residue,out _));
        Assert.Contains(F(residue),v=>ChurchScene.Read(v)!=0);
    }

    [Fact] public void ActualBidirectionalPortsMoveBothSidesAndRelieveTheirError()
    {
        var ink=Ink();var world=new ChurchScene(ink,ink,Layout(),true,true);
        var point=world.Sites[ink.Sites[0]];var wall=world.Parts[0];var x=XY(ink.Sites[0])[0];var l=world.Bounds(wall)[0];
        world.Add("Shared boundary","mapping",0,0,4,G(ink.Surface,wall.Geometry), (point.Port(0,0),1),(ChurchScene.Left(wall),-1));
        var before=ChurchSettlement.Errors(world).Single(e=>e.Name=="Shared boundary").Demand;
        for(var i=0;i<8;i++)ChurchSettlement.Step(world);
        var graph=world.PublishGraph(G());
        Assert.True(XY(graph.Sites[0])[0]>x);Assert.True(world.Bounds(wall)[0]<l);
        Assert.True(ChurchSettlement.Errors(world).Single(e=>e.Name=="Shared boundary").Demand<before);
        Assert.True(world.Parts[0].Machine.LastReceipt is not null);
    }

    [Fact] public void CoordinateEqualityDoesNotBindSeparateSites()
    {
        var graph=FromJson(Json("""{"sites":[[10,10],[40,10],[10,10],[10,40]],"edges":[{"a":0,"b":1},{"a":2,"b":3}]}"""));
        var world=new ChurchScene(graph,graph,Layout(),false,false,moveInk:false);
        ChurchSettlement.Step(world);var next=world.PublishGraph(G());
        Assert.Equal(2,Components(next));Assert.NotSame(next.Edges[0].A,next.Edges[1].A);
    }

    [Fact] public void CoupledPassesRestoreNativePortsAndKeepAllSourceIntervals()
    {
        var ink=Ink();var bundle=Json(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"providers.json")));
        var run=ChurchExperiment.Run(ink.Surface,ink,Layout(),Json("""{"targets":[]}"""),Profiles(ink),bundle,"fixture-model","bidirectional",2,2,16);
        var decoded=(Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(run.Surface))[0];ChurchExperiment.Validate(decoded);
        var final=Read((Relation)F(decoded)[3]);Assert.NotEmpty(final.Edges);
        foreach(var edge in final.Edges.Where(e=>!NeighborhoodClosure.IsInferred(e.Source)))
            Assert.True(edge.Span.TryReadSegment(out _,out var a,out var b)&&a.Ticks<b.Ticks);
    }

    [Fact] public void UnknownArmAndExcessEffortDoNotRun()
    {
        var ink=Ink();var bundle=Json(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"providers.json")));
        Assert.Throws<ArgumentException>(()=>ChurchExperiment.Run(ink.Surface,ink,Layout(),Json("{}"),Profiles(ink),bundle,"x","unbounded",2,2,16));
        Assert.Throws<ArgumentException>(()=>ChurchExperiment.Run(ink.Surface,ink,Layout(),Json("{}"),Profiles(ink),bundle,"x","bidirectional",ChurchExperiment.MaximumPasses+1,2,16));
    }

    [Fact] public void LargerEffortBudgetPreservesTheEarlierPassesAndRestoresItsController()
    {
        var ink=Ink();var bundle=Json(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"providers.json")));
        var shortRun=ChurchExperiment.Run(ink.Surface,ink,Layout(),Json("""{"targets":[]}"""),Profiles(ink),bundle,"fixture-model","shapes-only",6,1,16);
        var longRun=ChurchExperiment.Run(ink.Surface,ink,Layout(),Json("""{"targets":[]}"""),Profiles(ink),bundle,"fixture-model","shapes-only",ChurchExperiment.MaximumPasses,1,16);
        var shortView=Json(JsonSerializer.Serialize(shortRun.View,GojoQuickDraw.ChurchStudy.Program.Json));
        var longView=Json(JsonSerializer.Serialize(longRun.View,GojoQuickDraw.ChurchStudy.Program.Json));
        var stages=longView.GetProperty("stages");
        foreach(var (stage,i) in shortView.GetProperty("stages").EnumerateArray().Select((s,i)=>(s,i)))
            Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(stage.GetRawText()),System.Text.Json.Nodes.JsonNode.Parse(stages[i].GetRawText())));
        var restored=(Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(longRun.Surface))[0];ChurchExperiment.Validate(restored);
        Assert.True(F(F(restored)[4])[0] is Relation limit&&limit.TryReadSegment(out _,out _,out var end)&&end.Ticks==ChurchExperiment.MaximumPasses);
    }

    [Fact] public void GraphPublicationContainsOnlyAcceptedDisplacement()
    {
        var ink=Ink();var world=new ChurchScene(ink,ink,Layout(),false,false);
        var site=ink.Sites[0];var machine=world.Sites[site];var before=Gojo.Experiments.ConstructionMachines.Program.XY(machine.Point(0));
        world.Add("Nudge","line",ChurchScene.Coordinate(18),ChurchScene.Coordinate(18),4,site,(machine.Port(0,0),1));
        var round=ChurchSettlement.Step(world);var after=Gojo.Experiments.ConstructionMachines.Program.XY(machine.Point(0));
        var graph=world.PublishGraph(round.Surface);
        Assert.Equal(XY(site)[0]+(after[0]-before[0])/ChurchScene.Scale,XY(graph.Edges[0].A)[0],6);
        Assert.Equal(XY(site)[1],XY(graph.Edges[0].A)[1],6);
        Assert.True(round.Requested>=round.Accepted);
    }

    [Fact] public void ADiscardedTapCannotShiftLaterSourceAssociations()
    {
        var ink=Ink();var scene=new ChurchScene(ink,ink,Layout(),true,true);
        var bindings=new Dictionary<string,IElement>{{"before",ink.Edges[0].Source},{"tap",new Placeholder()},{"after",ink.Edges[1].Source}};
        scene.BindProfiles(bindings);
        scene.AddMappings(Json("""{"targets":[{"id":"right","part":"wall","segments":[1],"ink":[{"span":"after","from":0,"to":1,"targetFrom":0,"targetTo":1}]}]}"""));
        var mappings=scene.Rules.Where(r=>r.Layer=="mapping").ToArray();Assert.NotEmpty(mappings);
        Assert.All(mappings,r=>Assert.Same(ink.Edges[1].Witness,F(r.Error.Support)[0]));
        Assert.Contains(F(F(F(scene.Root)[6])[0]),e=>ReferenceEquals(e,ink.Edges[1].Source));
    }

    [Fact] public void RefreshedWorldImportsItsScaffoldThroughExposedPorts()
    {
        var ink=Ink();var first=new ChurchScene(ink,ink,Layout(),true,true);
        first.Add("Shift","mapping",ChurchScene.Coordinate(30),ChurchScene.Coordinate(30),4,ink.Surface,(ChurchScene.Left(first.Parts[0]),1));
        ChurchSettlement.Step(first);var previous=first.Parts.ToDictionary(p=>p.Id,p=>p);
        var second=new ChurchScene(ink,ink,Layout(),true,true,previous);
        Assert.Equal(first.Bounds(first.Parts[0]),second.Bounds(second.Parts[0]));
        Assert.Single(second.Imports);
        Assert.True(ReadWitness.TryRead(second.Imports[0],out var source,out var basis,out var target,out _,out _));
        Assert.Equal(4,F(source).Length);Assert.Equal(4,F(target).Length);Assert.Equal(4,F(basis).Length);
        Assert.NotSame(ChurchScene.Left(first.Parts[0]),ChurchScene.Left(second.Parts[0]));
        Assert.Same(ChurchScene.Left(first.Parts[0]),F(F(source)[0])[0]);
        Assert.Same(ChurchScene.Left(second.Parts[0]),F(F(target)[0])[0]);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void BoundaryImportUsesActualProducerRolesAndPreservesTheWorld(bool foreign)
    {
        var ink=Ink();var bundle=Json(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"providers.json")));
        var run=ChurchExperiment.Run(ink.Surface,ink,Layout(),Json("""{"targets":[]}"""),Profiles(ink),bundle,"fixture-model","shapes-only",1,1,8);
        var pass=F(F(run.Surface)[2])[0];var world=F(F(pass)[1]);var producer=(Relation)F(F(world[3])[0])[1];var roles=F(F(producer)[2]);
        var edges=G(new Relation(roles[0],foreign?new Placeholder():roles[1]),new Relation(roles[1],roles[2]),
            new Relation(roles[2],roles[3]),new Relation(roles[3],roles[0]));
        var boundary=ReadWitness.Create(new Carrier(),producer,F(producer)[2],edges,G(),G());
        var import=ReadWitness.Create(new Carrier(),run.Surface,G(),G(new Relation(pass,G(boundary))),G(),G());
        var wrapper=G(run.Surface,import);
        if(foreign){Assert.Throws<InvalidDataException>(()=>BoundaryImport.Validate(wrapper));return;}
        var restored=(Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(wrapper))[0];BoundaryImport.Validate(restored);
        var original=BoundaryImport.Unwrap(restored,out var receipt);Assert.Same(F(restored)[0],original);Assert.NotNull(receipt);
        Assert.Equal(F(run.Surface).Length,F(original).Length);
    }

    [Fact] public void PackedImportPreservesEveryOriginalRowAndRestoresTheSameRoleGraph()
    {
        var ink=Ink();var bundle=Json(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"providers.json")));
        var run=ChurchExperiment.Run(ink.Surface,ink,Layout(),Json("""{"targets":[]}"""),Profiles(ink),bundle,"fixture-model","shapes-only",2,1,8);
        var preview=Json(JsonSerializer.Serialize(run.View,GojoQuickDraw.ChurchStudy.Program.Json));
        var original=GojoEncodedSnapshot.CaptureGraph(run.Surface);
        var declared=Layout().GetProperty("parts").EnumerateArray().ToDictionary(p=>p.GetProperty("id").GetString()!,p=>p.Clone());
        var (packed,edges)=PackedBoundaryImport.Create(original,preview,declared,"fixture-source-hash");
        Assert.Equal(8,edges);Assert.True(PackedBoundaryImport.IsSealed(packed));
        Assert.Equal(original.Relations,packed.Relations.Take(original.Relations.Length));
        Assert.Equal(original.Carriers,packed.Carriers.Take(original.Carriers.Length));
        Assert.Equal(original.TickRefs,packed.TickRefs.Take(original.TickRefs.Length));
        var restored=(Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(packed))[0];BoundaryImport.Validate(restored);
        var raw=BoundaryImport.Unwrap(restored,out _);ChurchExperiment.Validate(raw);
        Assert.Equal(F(run.Surface).Length,F(raw).Length);
    }
}
