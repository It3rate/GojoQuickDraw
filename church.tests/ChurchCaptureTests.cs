using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using GojoQuickDraw.ChurchStudy;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy.Tests;

public class ChurchCaptureTests
{
    private static JsonElement Json(string text)=>JsonDocument.Parse(text).RootElement.Clone();
    private static Graph Ink()=>FromJson(Json("""{"sites":[[20,60],[180,60]],"edges":[{"a":0,"b":1}]}"""));
    private static JsonElement Layout(int roofBottom=58)=>Json(JsonSerializer.Serialize(new{parts=new[]{
        new{id="wall",role="building",status="source-supported",bounds=new[]{20,60,180,210},geometry=new{points=new[]{new[]{20,60},new[]{180,60},new[]{180,210},new[]{20,210}},segments=new[]{new[]{0,1},new[]{1,2},new[]{2,3},new[]{3,0}}}},
        new{id="roof",role="roof",status="source-supported",bounds=new[]{20,20,180,roofBottom},geometry=new{points=new[]{new[]{20,roofBottom},new[]{100,20},new[]{180,roofBottom}},segments=new[]{new[]{0,1},new[]{1,2},new[]{2,0}}}}
    }}));
    private static JsonElement Plan(string bindings="""[{"target":{"part":"roof","coordinate":3},"terms":[{"part":"wall","coordinate":1,"coefficient":1}]}]""")=>
        Json("""{"rules":[],"knots":[],"partitions":[],"connections":{"startPass":3,"recipes":[{"id":"contact","captureDistance":12,"releasePressure":24,"bindings":BINDINGS}]}}""".Replace("BINDINGS",bindings));
    private static ChurchScene Scene(Graph ink,ChurchCaptureController controller,JsonElement plan,int pass,ChurchScene? previous=null,int roofBottom=58)=>
        new(ink,ink,Layout(roofBottom),true,true,previous?.Parts.ToDictionary(p=>p.Id),hypotheses:plan,capture:controller,pass:pass);

    [Fact] public void CapturedContactUsesTheActualConstructionAndSurvivesNativeRestore()
    {
        var ink=Ink();var source=GojoNumericFormat.Encode(ink.Surface);var controller=new ChurchCaptureController();
        var scene=Scene(ink,controller,Plan(),3);
        Assert.Same(scene.Parts[0].Machine,scene.Parts[1].Machine);
        Assert.Equal(scene.Bounds(scene.Parts[0])[1],scene.Bounds(scene.Parts[1])[3]);
        Assert.Equal(scene.Points(scene.Parts[0])[0][1],scene.Points(scene.Parts[1])[0][1]);
        scene.Add("weak competing evidence","test",ChurchScene.Coordinate(62),ChurchScene.Coordinate(62),4,ink.Surface,(ChurchScene.Bottom(scene.Parts[1]),1));
        var round=ChurchSettlement.Step(scene);controller.ObservePressure(scene,round);
        Assert.Equal(scene.Bounds(scene.Parts[0])[1],scene.Bounds(scene.Parts[1])[3]);
        Assert.False(Assert.Single(scene.CaptureViews!.Cast<ChurchCaptureController.View>()).ReleasePending);
        var restored=(Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(scene.Root))[0];
        var rows=F(F(restored)[3]);Assert.All(rows,row=>Assert.Equal(3,F(row).Length));
        var geometries=rows.Select(row=>(Relation)F(row)[1]).ToArray();
        var a=Gojo.Experiments.ConstructionMachines.Program.XY(F(F(MachineRunner.Run(geometries[0]))[2])[0]);
        var b=Gojo.Experiments.ConstructionMachines.Program.XY(F(F(MachineRunner.Run(geometries[1]))[2])[0]);Assert.Equal(a[1],b[1]);
        Assert.Equal(source,GojoNumericFormat.Encode(ink.Surface));
    }

    [Fact] public void StrongConflictingRequestsReopenTheConstructionOnTheNextPass()
    {
        var ink=Ink();var controller=new ChurchCaptureController();var plan=Plan();var scene=Scene(ink,controller,plan,3);
        scene.Add("strong competing evidence","test",ChurchScene.Coordinate(500),ChurchScene.Coordinate(500),4,ink.Surface,(ChurchScene.Bottom(scene.Parts[1]),1));
        controller.ObservePressure(scene,ChurchSettlement.Step(scene));
        Assert.True(Assert.Single(scene.CaptureViews!.Cast<ChurchCaptureController.View>()).ReleasePending);
        var next=Scene(ink,controller,plan,4,scene);
        Assert.NotSame(next.Parts[0].Machine,next.Parts[1].Machine);
        Assert.Equal("released",Assert.Single(next.CaptureViews!.Cast<ChurchCaptureController.View>()).Action);
        for(var i=0;i<scene.Parts.Count;i++)Assert.Equal(scene.Bounds(scene.Parts[i]),next.Bounds(next.Parts[i]));
        next.Add("new evidence after release","test",ChurchScene.Coordinate(500),ChurchScene.Coordinate(500),4,ink.Surface,(ChurchScene.Bottom(next.Parts[1]),1));
        ChurchSettlement.Step(next);
        Assert.NotEqual(next.Bounds(next.Parts[0])[1],next.Bounds(next.Parts[1])[3]);
    }

    [Fact] public void DistanceAndWarmupDoNotTurnEveryCandidateIntoAnAcceptedConnection()
    {
        var ink=Ink();var plan=Plan();
        foreach(var (pass,bottom) in new[]{(1,58),(3,30)})
        {
            var scene=Scene(ink,new(),plan,pass,roofBottom:bottom);
            Assert.NotSame(scene.Parts[0].Machine,scene.Parts[1].Machine);
            Assert.False(Assert.Single(scene.CaptureViews!.Cast<ChurchCaptureController.View>()).Active);
        }
    }

    [Fact] public void CyclicConstructionIsKeptAsBlockedEvidence()
    {
        var ink=Ink();var plan=Plan("""[{"target":{"part":"roof","coordinate":3},"terms":[{"part":"wall","coordinate":1,"coefficient":1}]},{"target":{"part":"wall","coordinate":1},"terms":[{"part":"roof","coordinate":3,"coefficient":1}]}]""");
        var scene=Scene(ink,new(),plan,3);
        Assert.Equal("construction blocked",Assert.Single(scene.CaptureViews!.Cast<ChurchCaptureController.View>()).Action);
        Assert.NotEmpty(scene.Imports);Assert.NotSame(scene.Parts[0].Machine,scene.Parts[1].Machine);
    }
}
