using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using GojoQuickDraw.ChurchStudy;
using GojoQuickDraw.Learning;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

namespace GojoQuickDraw.ChurchStudy.Tests;

public class ChurchHypothesesTests
{
    private static JsonElement Json(string s)=>JsonDocument.Parse(s).RootElement.Clone();
    private static JsonElement Layout()=>Json("""{"parts":[{"id":"wall","role":"building","shape":"frame","status":"source-supported","bounds":[20,60,180,210],"geometry":{"points":[[20,60],[180,60],[180,210],[20,210]],"segments":[[0,1],[1,2],[2,3],[3,0]]}},{"id":"tower","role":"tower","shape":"rectangle","status":"source-supported","bounds":[60,40,100,130],"geometry":{"points":[[60,40],[100,40],[100,130],[60,130]],"segments":[[0,1],[1,2],[2,3],[3,0]]}}]}""");
    private static JsonElement Plan()=>Json("""{"rules":[{"name":"support","layer":"support","lower":0,"upper":0,"influence":4,"terms":[{"part":"tower","coordinate":3,"coefficient":1},{"part":"wall","coordinate":1,"coefficient":-1}]}],"knots":[],"partitions":[{"body":"wall","cutters":["tower"]}]}""");

    [Fact] public void GenericExpertUsesBidirectionalPortsAndConservedReceiverTransfers()
    {
        var ink=FromJson(Json("""{"sites":[[20,60],[180,60]],"edges":[{"a":0,"b":1}]}"""));
        var scene=new ChurchScene(ink,ink,Layout(),true,true,hypotheses:Plan());
        var before=ChurchSettlement.Errors(scene).Single(e=>e.Layer=="support").Demand;
        var wall=scene.Bounds(scene.Parts[0]);var tower=scene.Bounds(scene.Parts[1]);
        for(var i=0;i<6;i++)ChurchSettlement.Step(scene);
        Assert.True(ChurchSettlement.Errors(scene).Single(e=>e.Layer=="support").Demand<before);
        Assert.NotEqual(wall[1],scene.Bounds(scene.Parts[0])[1]);Assert.NotEqual(tower[3],scene.Bounds(scene.Parts[1])[3]);
    }

    [Fact] public void PartitionsReadLivePublicBoundsWithoutCreatingIndependentCellActuators()
    {
        var ink=FromJson(Json("""{"sites":[[20,60],[180,60]],"edges":[{"a":0,"b":1}]}"""));
        var scene=new ChurchScene(ink,ink,Layout(),true,true,hypotheses:Plan());
        var count=scene.Machines.Count;var read=ChurchPartitions.Build(scene,Plan());
        Assert.NotEmpty(read.Views);Assert.Equal(count,scene.Machines.Count);
        var cells=read.Views.Cast<ChurchPartitions.CellView>().ToArray();
        var area=cells.Sum(c=>(c.Bounds[2]-c.Bounds[0])*(c.Bounds[3]-c.Bounds[1]));
        Assert.Equal(160*150-40*70,area,5);
        foreach(var cell in cells)Assert.False(cell.Bounds[0]<100&&cell.Bounds[2]>60&&cell.Bounds[1]<130&&cell.Bounds[3]>60);
        var decoded=(Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(read.Surface))[0];
        foreach(var construction in F(F(decoded)[1]))Assert.Equal(4,F(F(MachineRunner.Run((Relation)construction))[2]).Length);
        ChurchSettlement.Step(scene);var next=ChurchPartitions.Build(scene,Plan());
        Assert.NotEqual(cells[0].Bounds[1],((ChurchPartitions.CellView)next.Views[0]).Bounds[1]);
    }

    [Fact] public void InteriorKnotKeepsBothSourceIntervalsAndDoesNotMoveByBeingRead()
    {
        var ink=FromJson(Json("""{"sites":[[0,0],[100,100]],"edges":[{"a":0,"b":1}]}"""));
        var plan=Json("""{"knots":[{"span":"p0","t":0.5,"part":"wall","point":0}]}""");
        var split=ChurchHypotheses.SplitKnots(ink,plan,new Dictionary<string,IElement>{{"p0",ink.Edges[0].Source}});
        Assert.Single(split.Witnesses);Assert.Equal(2,split.Graph.Edges.Length);
        Assert.Same(split.Graph.Edges[0].B,split.Graph.Edges[1].A);
        Assert.Equal(new[]{50d,50d},XY(split.Graph.Edges[0].B));
        Assert.All(split.Graph.Edges,e=>Assert.Same(ink.Edges[0].Source,e.Source));
        Assert.Equal(0,split.Graph.Edges[0].Span.TryReadSegment(out _,out var a,out var b)?a.Ticks:-1);
        Assert.True(split.Graph.Edges[1].Span.TryReadSegment(out _,out var c,out var d));Assert.Equal(b.Ticks,c.Ticks);Assert.Equal(PatchEvidence.Q,d.Ticks);
    }

    [Fact] public void ExpertWorldRestoresItsRealPortRelationsAndDerivedConstructionTopology()
    {
        var ink=FromJson(Json("""{"sites":[[20,60],[180,60]],"edges":[{"a":0,"b":1}]}"""));
        var model=Json(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"providers.json")));
        var run=ChurchExperiment.Run(ink.Surface,ink,Layout(),Json("""{"targets":[]}"""),
            new Dictionary<string,IElement>{{"p0",ink.Edges[0].Source}},model,"fixture","bidirectional",2,2,8,Plan());
        var restored=(Relation)GojoNumericFormat.Decode(GojoNumericFormat.Encode(run.Surface))[0];ChurchExperiment.Validate(restored);
        var world=F(F(F(restored)[2])[^1]);var scene=F(world[1]);var imports=F(F(scene[6])[1]);
        var derived=F(imports[^1]);Assert.NotEmpty(F(derived[1]));
        foreach(var geometry in F(derived[1]))Assert.Equal(4,F(F(MachineRunner.Run((Relation)geometry))[2]).Length);
    }

    [Fact] public void FeatureResolutionDoesNotSilentlyWeakenTheSamePhysicalOpinion()
    {
        var ink=FromJson(Json("""{"sites":[[20,60],[180,60]],"edges":[{"a":0,"b":1}]}"""));
        var plain=new ChurchScene(ink,ink,Layout(),true,true);
        var fine=new ChurchScene(ink,ink,Layout(),true,true);
        foreach(var (scene,scale) in new[]{(plain,1L),(fine,256L)})
        {
            scene.AddCalibrated("same displacement","calibration",0,0,4,ink.Surface,scale,
                (ChurchScene.Bottom(scene.Parts[1]),scale),(ChurchScene.Top(scene.Parts[0]),-scale));
        }
        var plainMemory=new Dictionary<Relation,IElement>(ReferenceEqualityComparer.Instance);
        var fineMemory=new Dictionary<Relation,IElement>(ReferenceEqualityComparer.Instance);
        for(var i=0;i<8;i++)
        {
            var a=ChurchSettlement.Step(plain,plainMemory);var b=ChurchSettlement.Step(fine,fineMemory);
            Assert.Equal(a.Accepted,b.Accepted,8);Assert.Equal(a.Quantum,b.Quantum,8);
            for(var j=0;j<plain.Parts.Count;j++)Assert.Equal(plain.Bounds(plain.Parts[j]),fine.Bounds(fine.Parts[j]));
        }
    }
}
