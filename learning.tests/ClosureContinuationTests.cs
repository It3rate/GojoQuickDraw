using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Experiments.QuickDraw;
using Gojo.Structures;
using GojoQuickDraw.Learning;
using static GojoQuickDraw.Learning.NeighborhoodGraph;
using static GojoQuickDraw.Learning.NeighborhoodProposals;

namespace GojoQuickDraw.Learning.Tests;

public sealed class ClosureContinuationTests
{
    private static Graph GraphOf(double[][] sites,int[][] edges)
    {
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(new{sites,edges=edges.Select(e=>new{a=e[0],b=e[1]})}));return FromJson(json.RootElement);
    }
    private static Graph OpenBox()=>GraphOf([[0,4],[0,100],[100,100],[100,0],[4,0]],[[0,1],[1,2],[2,3],[3,4]]);
    private static JsonDocument ConstantModels()
    {
        object Head(int count)=>new{columns=Enumerable.Range(0,count).SelectMany(i=>Enumerable.Range(0,6).Select(b=>$"{i}:{b}")).Prepend("bias").ToArray(),
            weights=Enumerable.Repeat(0.0,count*6).Prepend(Math.Log(.98/.02)).ToArray()};
        return JsonDocument.Parse(JsonSerializer.Serialize(new{line=Head(8),repair=Head(24),closure=Head(10)}));
    }
    [Fact]
    public void AContinuationExposesOuterPortsAndKeepsTheInternalSplitAndDeviation()
    {
        var g=GraphOf([[-100,0],[0,1],[100,0]],[[0,1],[1,2]]);var source=g.Surface;
        var context=NeighborhoodContinuations.Build(g);var read=Assert.Single(NeighborhoodContinuations.ReadAll(context,g));
        Assert.Same(g.Sites[1],read.Inner);Assert.Same(g.Sites[0],read.First);Assert.Same(g.Sites[2],read.Last);
        Assert.InRange(read.Deviation,.001,.02);Assert.Same(source,g.Surface);Assert.Equal(2,g.Edges.Length);
        var projected=Project(g,g.Edges.Select(e=>e.Source).ToArray(),continuations:[read]);
        Assert.True(Assert.Single(projected.Sites,s=>s.Kind=="I").Dormant);Assert.Single(projected.Continuations);
        var doc=GojoStructureCompactJson.Deserialize(PatchEvidence.Capture("continuation",context));
        Assert.True(GojoStructureCompactJson.TryToElement(doc,out var restored));var fields=Fields(restored.Root,2);
        Assert.Single(NeighborhoodContinuations.ReadAll((Relation)restored.Root,Read((Relation)fields[0])));
    }
    [Fact]
    public void ANewBranchReactivatesAnInternalIAsATeeWithoutDeletingItsHistory()
    {
        var g=GraphOf([[-100,0],[0,0],[100,0],[0,4],[0,100]],[[0,1],[1,2],[3,4]]);
        var memory=NeighborhoodContinuations.Build(g);Assert.Single(NeighborhoodContinuations.ReadAll(memory,g));
        var merge=Merge(g,[g.Sites[1],g.Sites[3]],Neighborhood(g,[0,0],g.Edges));Assert.True(merge.Admissible);
        Assert.Single(merge.After.Sites,s=>Kind(merge.After,s)=="T");Assert.Empty(NeighborhoodContinuations.ReadAll(NeighborhoodContinuations.Build(merge.After),merge.After));
        Assert.Single(NeighborhoodContinuations.ReadAll(memory,g));Assert.Equal(3,merge.After.Edges.Length);
    }
    [Fact]
    public void ACornerCanObserveAndAttachToAnotherSegmentButDistantObservationsStaySeparate()
    {
        var g=GraphOf([[-100,0],[0,0],[0,100],[4,-4],[70,-70]],[[0,1],[1,2],[3,4]]);
        var corner=g.Sites[1];var c=Assert.Single(NeighborhoodContacts.Discover(g),c=>ReferenceEquals(c.Endpoint,corner));
        Assert.Equal(2,Degree(g,corner));Assert.True(c.Gap<=c.Limit);
        var attached=Attach(g,c);Assert.True(attached.Admissible);Assert.Contains(attached.After.Sites,s=>Degree(attached.After,s)==3);
        Assert.Equal(2,Degree(g,corner));
        var far=GraphOf([[-100,0],[0,0],[0,100],[200,-4],[270,-70]],[[0,1],[1,2],[3,4]]);
        Assert.All(NeighborhoodContacts.Discover(far).Where(c=>ReferenceEquals(c.Endpoint,far.Sites[1])),c=>Assert.True(c.Gap>c.Limit));
    }
    [Fact]
    public void ClosureAddsAnUnobservedPassageWithoutMovingOrReplacingAcquiredEdges()
    {
        var g=OpenBox();var p=Assert.Single(NeighborhoodClosure.Discover(g));var c=NeighborhoodClosure.Candidate(p);Assert.True(c.Admissible);
        Assert.Equal(0,Cycles(g));Assert.Equal(1,Cycles(c.After));Assert.All(g.Edges,e=>Assert.Contains(c.After.Edges,a=>ReferenceEquals(a.Witness,e.Witness)));
        var inferred=Assert.Single(c.After.Edges,e=>NeighborhoodClosure.IsInferred(e.Source));Assert.Same(p.Witness,inferred.Source);
        Assert.Equal(10,NeighborhoodFeatures.ReadCells(NeighborhoodClosure.Features(p).Surface).Length);
        var view=Project(c.After,g.Edges.Select(e=>e.Source).ToArray());Assert.Equal(4,view.Curves.Length);Assert.Single(view.InferredCurves);
        Assert.StartsWith("inferred:",Assert.Single(view.Edges,e=>e.Inferred).Source);
        Assert.Empty(NeighborhoodClosure.Discover(c.After));
    }
    [Fact]
    public void ClosureIsIndependentlySelectableReanalyzesItsJointsAndSurvivesRestore()
    {
        var g=OpenBox();using var models=ConstantModels();
        var disabled=NeighborhoodRun.Execute(g.Surface,g,models.RootElement,"off",false,false,4,128,false);
        Assert.Same(g.Surface,NeighborhoodRun.RootFields(disabled.Surface)[3]);
        var run=NeighborhoodRun.Execute(g.Surface,g,models.RootElement,"fixed",false,false,4,128,true);
        var final=Read((Relation)NeighborhoodRun.RootFields(run.Surface)[3]);Assert.Equal(1,Cycles(final));Assert.Equal(5,final.Edges.Length);
        Assert.All(final.Sites,s=>Assert.Equal(2,Degree(final,s)));
        var document=GojoStructureCompactJson.Deserialize(PatchEvidence.Capture("closure-result",run.Surface));
        Assert.True(GojoStructureCompactJson.TryToElement(document,out var restored));NeighborhoodRun.Validate((Relation)restored.Root);
        var stages=JsonSerializer.SerializeToElement(NeighborhoodRun.Stages((Relation)restored.Root),Program.Json);
        Assert.Single(stages[1].GetProperty("graph").GetProperty("inferredCurves").EnumerateArray());
    }
    [Fact]
    public void ClosureRequiresARouteAndRejectsForgedGapMeasurements()
    {
        var separate=GraphOf([[0,0],[100,0],[0,2],[100,2]],[[0,1],[2,3]]);Assert.Empty(NeighborhoodClosure.Discover(separate));
        var g=OpenBox();var p=Assert.Single(NeighborhoodClosure.Discover(g));
        ReadWitness.TryRead(p.Witness,out var selected,out var basis,out var result,out var residue,out var before);var fields=Fields(basis,5);
        fields[1]=Relation.Segment(new Carrier(),0,1);var fake=ReadWitness.Create(new Carrier(),selected,PatchEvidence.G(fields),result,residue,before);
        Assert.Throws<InvalidDataException>(()=>NeighborhoodClosure.Read(fake));
    }
    [Fact]
    public void AddingAConnectorInvalidatesOtherProposalsThatReadTheOldEndpointDegree()
    {
        var g=GraphOf([[0,100],[0,0],[4,0],[-4,0]],[[0,1],[0,2],[0,3]]);using var models=ConstantModels();
        Assert.True(NeighborhoodClosure.Discover(g).Length>1);
        var run=NeighborhoodRun.Execute(g.Surface,g,models.RootElement,"fixed",false,false,4,128,true);
        var final=Read((Relation)NeighborhoodRun.RootFields(run.Surface)[3]);
        Assert.Single(final.Edges,e=>NeighborhoodClosure.IsInferred(e.Source));
        var read=JsonSerializer.SerializeToElement(run.Readback,Program.Json);
        Assert.Contains(read.GetProperty("decisions").EnumerateArray(),r=>r.GetProperty("status").GetString()=="stale; reevaluate next pass");
    }
    [Fact]
    public void AStrongerMergedNeighborhoodIsScheduledBeforeAWeakerOverlappingPair()
    {
        var g=GraphOf([[-100,0],[-6,0],[6,0],[100,0],[0,6],[0,100]],[[0,1],[2,3],[4,5]]);
        using var baseModel=ConstantModels();var model=JsonSerializer.SerializeToNode(baseModel.RootElement)!;
        var columns=model["repair"]!["columns"]!.AsArray();var weights=model["repair"]!["weights"]!.AsArray();
        weights[0]=Math.Log(.70/.30);
        for(var i=0;i<columns.Count;i++)if(columns[i]!.GetValue<string>()=="4:3")weights[i]=Math.Log(.98/.02)-Math.Log(.70/.30);
        using var document=JsonDocument.Parse(model.ToJsonString());
        var run=NeighborhoodRun.Execute(g.Surface,g,document.RootElement,"fixed",true,false,1,192,false);
        var final=Read((Relation)NeighborhoodRun.RootFields(run.Surface)[3]);
        Assert.Single(final.Sites,s=>Kind(final,s)=="T");Assert.DoesNotContain(final.Sites,s=>Kind(final,s)=="L");
    }
    [Fact]
    public void AcquiredStrokeOrderAndOptionalTimesRemainActualEvidence()
    {
        var g=OpenBox();var acquisition=new NativeAcquisition(QuickDrawRecord.Parse("""
            {"key_id":"time-control","word":"church","recognized":true,"drawing":[[[0,0,100,100,4],[4,100,100,0,0],[0,10,20,30,40]]]}
            """));
        var stroke=acquisition.StrokeReads[0];var edges=g.Edges.Select((e,i)=>
        {
            var selection=new Relation(stroke.Surface,PatchEvidence.G(stroke.Samples[i].Surface,stroke.Samples[i+1].Surface));
            var source=ReadWitness.Create(new Carrier(),selection,g.Frame,e.Witness,selection,acquisition.Surface);
            return Curve(g.Frame,source,e.Span,e.A,e.B,[],e.Witness);
        }).ToArray();
        var sourced=Create(g.Frame,edges);var p=Assert.Single(NeighborhoodClosure.Discover(sourced));var cells=NeighborhoodClosure.Features(p);
        Assert.Equal(0,NeighborhoodClosure.Order(p.Path[0].Edge,p.A).Stroke);Assert.Equal(0,NeighborhoodClosure.Order(p.Path[0].Edge,p.A).Time);
        Assert.Equal(1,cells.Values[8]);Assert.Equal(1,cells.Values[9]);
        Assert.Null(NeighborhoodClosure.Order(g.Edges[0],g.Edges[0].A).Time);
        var otherAcquisition=new NativeAcquisition(acquisition.Source);var otherStroke=otherAcquisition.StrokeReads[0];var lastEdge=edges[^1];
        var otherSelection=new Relation(otherStroke.Surface,PatchEvidence.G(otherStroke.Samples[3].Surface,otherStroke.Samples[4].Surface));
        var otherSource=ReadWitness.Create(new Carrier(),otherSelection,g.Frame,lastEdge.Witness,otherSelection,otherAcquisition.Surface);
        var mixed=Create(g.Frame,edges.SkipLast(1).Append(Curve(g.Frame,otherSource,lastEdge.Span,lastEdge.A,lastEdge.B,[],lastEdge.Witness)));
        var mixedFeatures=NeighborhoodClosure.Features(Assert.Single(NeighborhoodClosure.Discover(mixed)));
        Assert.Equal(0,mixedFeatures.Values[6]);Assert.Equal(0,mixedFeatures.Values[8]);
    }
}
