using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using GojoQuickDraw.ChurchStudy;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy.Tests;

public class ChurchCadTests
{
    private static JsonElement Json(string text)=>JsonDocument.Parse(text).RootElement.Clone();
    private static Graph Ink()=>FromJson(Json("""{"sites":[[20,100],[200,100]],"edges":[{"a":0,"b":1}]}"""));
    private static object Part(string id,int[] b)=>new{id,role="shape",status="source-supported",bounds=b,
        geometry=new{points=new[]{new[]{b[0],b[1]},new[]{b[2],b[1]},new[]{b[2],b[3]},new[]{b[0],b[3]}},segments=new[]{new[]{0,1},new[]{1,2},new[]{2,3},new[]{3,0}}}};
    private static JsonElement Layout()=>Json(JsonSerializer.Serialize(new{parts=new[]{Part("body",[20,100,200,300]),Part("upper",[15,20,75,100]),Part("mark",[34,34,46,60])}}));

    [Fact] public void SupportingLineBindingPreservesWidthAndSeparateSourceSitesAfterRestore()
    {
        var ink=Ink();var original=GojoNumericFormat.Encode(ink.Surface);
        var plan=Json("""{"rules":[],"connections":{"startPass":3,"recipes":[{"id":"collinear","captureDistance":12,"releasePressure":24,"supportParts":["body","upper"],"bindings":[{"target":{"part":"upper","coordinate":0},"terms":[{"part":"body","coordinate":0,"coefficient":1}]},{"target":{"part":"upper","coordinate":2},"terms":[{"part":"body","coordinate":0,"coefficient":1},{"part":"upper","coordinate":4,"coefficient":1}]}]}]}}""");
        var scene=new ChurchScene(ink,ink,Layout(),true,true,hypotheses:plan,capture:new(),pass:3);
        var body=scene.Parts[0];var upper=scene.Parts[1];Assert.Equal(scene.Bounds(body)[0],scene.Bounds(upper)[0]);
        Assert.Equal(60,scene.Bounds(upper)[2]-scene.Bounds(upper)[0]);
        Assert.NotSame(ChurchScene.Left(body),ChurchScene.Left(upper));
        scene.Add("Neighbour asks to shift","test",ChurchScene.Coordinate(28),ChurchScene.Coordinate(28),4,ink.Surface,(ChurchScene.Left(body),1));
        ChurchSettlement.Step(scene);Assert.Equal(scene.Bounds(body)[0],scene.Bounds(upper)[0]);
        var restored=(Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(scene.Root))[0];
        var rows=F(F(restored)[3]);var a=Gojo.Experiments.ConstructionMachines.Program.XY(F(F(MachineRunner.Run((Relation)F(rows[0])[1]))[2])[0]);
        var b=Gojo.Experiments.ConstructionMachines.Program.XY(F(F(MachineRunner.Run((Relation)F(rows[1])[1]))[2])[0]);Assert.Equal(a[0],b[0]);
        Assert.Equal(original,GojoNumericFormat.Encode(ink.Surface));
    }

    [Fact] public void MidpointBindingCarriesDependentMarkAlongWithItsContainer()
    {
        var plan=Json("""{"rules":[],"connections":{"startPass":3,"recipes":[{"id":"upper-axis","captureDistance":100,"releasePressure":24,"supportParts":["body","upper","mark"],"bindings":[{"target":{"part":"upper","coordinate":0},"terms":[{"part":"body","coordinate":0,"coefficient":0.5},{"part":"body","coordinate":2,"coefficient":0.5},{"part":"upper","coordinate":4,"coefficient":-0.5}]},{"target":{"part":"upper","coordinate":2},"terms":[{"part":"body","coordinate":0,"coefficient":0.5},{"part":"body","coordinate":2,"coefficient":0.5},{"part":"upper","coordinate":4,"coefficient":0.5}]}]},{"id":"mark-axis","captureDistance":100,"releasePressure":24,"bindings":[{"target":{"part":"mark","coordinate":0},"terms":[{"part":"upper","coordinate":0,"coefficient":0.5},{"part":"upper","coordinate":2,"coefficient":0.5},{"part":"mark","coordinate":4,"coefficient":-0.5}]},{"target":{"part":"mark","coordinate":2},"terms":[{"part":"upper","coordinate":0,"coefficient":0.5},{"part":"upper","coordinate":2,"coefficient":0.5},{"part":"mark","coordinate":4,"coefficient":0.5}]}]}]}}""");
        var ink=Ink();var scene=new ChurchScene(ink,ink,Layout(),true,true,hypotheses:plan,capture:new(),pass:3);
        double Center(int i){var b=scene.Bounds(scene.Parts[i]);return (b[0]+b[2])/2;}
        Assert.Equal(Center(0),Center(1));Assert.Equal(Center(1),Center(2));
        scene.Add("Neighbour shifts body","test",ChurchScene.Coordinate(28),ChurchScene.Coordinate(28),4,ink.Surface,(ChurchScene.Left(scene.Parts[0]),1));
        ChurchSettlement.Step(scene);Assert.Equal(Center(0),Center(1));Assert.Equal(Center(1),Center(2));
    }

    [Fact] public void RepeatedPitchOpinionNegotiatesAllThreeFramesAndRetainsItsActualSupport()
    {
        var ink=Ink();var layout=Json(JsonSerializer.Serialize(new{parts=new[]{Part("a",[20,100,60,160]),Part("b",[100,100,140,160]),Part("c",[190,100,230,160])}}));
        var plan=Json("""{"rules":[{"name":"equal pitch","layer":"cad-spacing","lower":-0.5,"upper":0.5,"influence":2,"supportParts":["a","b","c"],"terms":[{"part":"a","coordinate":0,"coefficient":1},{"part":"a","coordinate":2,"coefficient":1},{"part":"b","coordinate":0,"coefficient":-2},{"part":"b","coordinate":2,"coefficient":-2},{"part":"c","coordinate":0,"coefficient":1},{"part":"c","coordinate":2,"coefficient":1}]}]}""");
        var scene=new ChurchScene(ink,ink,layout,true,true,hypotheses:plan);
        var rule=scene.Rules.Single(r=>r.Layer=="cad-spacing");var support=F(rule.Error.Support);
        Assert.Equal(3,F(support[1]).Length);Assert.All(scene.Parts,p=>Assert.Contains(F(support[1]),x=>ReferenceEquals(x,p.Geometry)));
        var before=ChurchSettlement.Errors(scene).Single(r=>r.Layer=="cad-spacing").Demand;
        for(var i=0;i<12;i++)ChurchSettlement.Step(scene);
        Assert.True(ChurchSettlement.Errors(scene).Single(r=>r.Layer=="cad-spacing").Demand<before);
    }
}
