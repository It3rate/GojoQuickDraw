using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using GojoQuickDraw.Learning;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy.Tests;

public class ChurchRecoveryTests
{
    private static JsonElement Json(string text)=>JsonDocument.Parse(text).RootElement.Clone();
    private static object Part(string id)=>new{id,role="shape",status="source-supported",bounds=new[]{20,40,100,160},
        geometry=new{points=new[]{new[]{20,40},new[]{100,40},new[]{100,160},new[]{20,160}},segments=new[]{new[]{0,1},new[]{1,2},new[]{2,3},new[]{3,0}}}};

    [Fact] public void RecoveryRetainsEarlierConstructionActualProfileIntervalsAndLiveTarget()
    {
        var source=FromJson(Json("""{"sites":[[20,40],[100,160]],"edges":[{"a":0,"b":1}]}"""));
        var original=GojoNumericFormat.Encode(source.Surface);
        var layout=JsonSerializer.SerializeToElement(new{parts=new[]{Part("recovered")}});
        var plan=JsonSerializer.SerializeToElement(new{rules=Array.Empty<object>(),recovery=new{
            initialParts=new[]{Part("first")},decisions=new[]{new{parts=new[]{"recovered"},sourceIntervals=new[]{new{span="p0",ranges=new[]{new[]{.25,.75}}}}}}}});
        var scene=new ChurchScene(source,source,layout,true,true,hypotheses:plan);
        scene.BindProfiles(new Dictionary<string,IElement>{{"p0",source.Edges[0].Source}});
        var boundsBefore=scene.Bounds(scene.Parts[0]);
        ChurchRecovery.Attach(scene,plan);
        Assert.Equal(boundsBefore,scene.Bounds(scene.Parts[0]));
        Assert.Single(scene.Imports,x=>ReadWitness.TryRead(x,out var before,out _,out _,out _,out _)&&
            before is Relation r&&r.TryReadOrderedGroup(out var f)&&f.Length==2&&f[0] is Relation first&&F(first).Length==7);
        var witness=scene.Imports.Last();Assert.True(ReadWitness.TryRead(witness,out var input,out _,out var result,out var residue,out _));
        var firstWorld=F(F(input)[0]);Assert.Same(source.Surface,firstWorld[0]);Assert.Single(F(firstWorld[3]));
        Assert.NotSame(scene.Parts[0].Geometry,F(F(firstWorld[3])[0])[1]);
        var selection=F(F(F(input)[1])[0]);Assert.Same(source.Edges[0].Source,selection[0]);
        Assert.True(((Relation)selection[1]).TryReadSegment(out var axis,out var a,out var b));
        source.Edges[0].Span.TryReadSegment(out var actualAxis,out _,out _);Assert.Same(actualAxis,axis);
        Assert.Equal(PatchEvidence.Q/4,a.Ticks);Assert.Equal(3*PatchEvidence.Q/4,b.Ticks);
        Assert.Same(scene.Parts[0].Geometry,F(result)[0]);Assert.IsType<Placeholder>(F(residue)[0]);
        var restored=GojoNumericFormat.Decode(GojoNumericFormat.Encode(scene.Root));Assert.Single(restored);
        Assert.Equal(original,GojoNumericFormat.Encode(source.Surface));
    }
}
