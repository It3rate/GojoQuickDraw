using System.Text.Json;
using Gojo.Core;
using GojoQuickDraw.ChurchStudy;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy.Tests;

public class ChurchLiveExperimentTests
{
    private static JsonElement Json(string text)=>JsonDocument.Parse(text).RootElement.Clone();

    [Fact]
    public void LiveRunRetainsOnlyInitialAndFinalViewsAndRestoresCurrentNativeLaws()
    {
        var ink=FromJson(Json("""{"sites":[[10,60],[180,60],[180,210]],"edges":[{"a":0,"b":1},{"a":1,"b":2}]}"""));
        var sourceBefore=ink.Sites.SelectMany(XY).ToArray();
        var layout=Json("""{"parts":[{"id":"wall","role":"building","status":"proposed","bounds":[22,60,180,210],"geometry":{"points":[[22,60],[180,60],[180,210],[22,210]],"segments":[[0,1],[1,2],[2,3],[3,0]]}}]}""");
        var profiles=ink.Edges.Select((e,i)=>(Id:"p"+i,e.Source)).ToDictionary(p=>p.Id,p=>p.Source);
        var bundle=Json(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"providers.json")));
        var plan=Json("""{"rules":[{"name":"Move wall","layer":"fixture","terms":[{"part":"wall","coordinate":0,"coefficient":1}],"lower":38,"upper":40,"influence":3}],"knots":[],"partitions":[]}""");
        var result=ChurchExperiment.Run(ink.Surface,ink,layout,Json("""{"targets":[]}"""),profiles,bundle,"fixture",
            "shapes-only",8,2,16,plan,"current","live");
        var view=Json(JsonSerializer.Serialize(result.View,Program.Json));
        Assert.Equal("live",view.GetProperty("executionMode").GetString());
        Assert.Equal(1,view.GetProperty("live").GetProperty("workingSceneBuilds").GetInt32());
        Assert.True(view.GetProperty("live").GetProperty("stableIdentities").GetBoolean());
        var stages=view.GetProperty("stages").EnumerateArray().ToArray();
        Assert.Equal(2,stages.Length);Assert.Equal(0,stages[0].GetProperty("pass").GetInt32());
        Assert.Equal(view.GetProperty("passes").GetInt32(),stages[1].GetProperty("pass").GetInt32());
        Assert.True(view.GetProperty("moved").GetInt32()>0);
        Assert.Equal(sourceBefore,ink.Sites.SelectMany(XY));
        var restored=(Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(result.Surface))[0];
        ChurchCheckpoint.Validate(restored);
        var state=ChurchCheckpoint.Restore(restored);
        Assert.NotEmpty(state.Obligations);Assert.NotEmpty(state.Parts);Assert.NotEmpty(state.Machines);
        Assert.Contains(state.Machines,m=>m.LastReceipt is not null);
        Assert.All(state.Machines,m=>Assert.InRange(Gojo.Experiments.ConstructionMachines.MachineExpressions.F(
            Gojo.Experiments.ConstructionMachines.MachineExpressions.F(m.Surface)[7]).Length,0,1));
    }

    [Fact]
    public void LiveRunDoesNotPretendToRetainAdaptiveHistory()
    {
        var ink=FromJson(Json("""{"sites":[[0,0],[10,0]],"edges":[{"a":0,"b":1}]}"""));
        Assert.Throws<ArgumentException>(()=>ChurchExperiment.Run(ink.Surface,ink,default,default,new Dictionary<string,IElement>(),
            default,"fixture","bidirectional",archiveMode:"history",executionMode:"live"));
        Assert.Throws<ArgumentException>(()=>ChurchExperiment.Run(ink.Surface,ink,default,default,new Dictionary<string,IElement>(),
            default,"fixture","local-models",archiveMode:"current",executionMode:"live"));
    }
}
