using System.Text.Json;
using Gojo.Core;
using Gojo.Structures;
using GojoQuickDraw.Learning;

namespace GojoQuickDraw.Learning.Tests;

public sealed class PatchTests
{
    private static JsonDocument Request(double[][] a,double[][] b)=>JsonDocument.Parse(JsonSerializer.Serialize(new{
        sourceKey="controlled",patches=new[]{new{id="p",group="g",label=3,labelBasis="controlled-construction",runs=new[]{a,b}}}}));
    [Fact]
    public void TargetConstructionsRemainDistinguishableWithoutAnyNames()
    {
        for(var i=0;i<5;i++)Assert.Equal(i,PatchEvidence.Kind(PatchEvidence.Construction(i)));
        var tee=PatchEvidence.Construction(3);
        Assert.Equal(3,tee.Edges.Count(e=>ReferenceEquals(e.Left,tee.Sites[1])||ReferenceEquals(e.Right,tee.Sites[1])));
        Assert.False(ReferenceEquals(tee.Sites[0],tee.Sites[2]));
        var x=new Carrier();var y=new Carrier(x.UnitShape);
        Relation Site(long a,long b)=>new(new Placeholder(),new Relation(new CarrierLocation(x,a),new CarrierLocation(y,b)));
        var sites=new[]{Site(-866,-500),Site(0,0),Site(866,-500),Site(0,1000)};
        var edges=new[]{new Relation(sites[0],sites[1]),new Relation(sites[1],sites[2]),new Relation(sites[1],sites[3])};
        var fork=new PatchEvidence.Machine(PatchEvidence.G(PatchEvidence.G(sites),PatchEvidence.G(edges)),sites,edges);
        Assert.Throws<InvalidDataException>(()=>PatchEvidence.Kind(fork));
    }
    [Fact]
    public void SmoothTurningAndSharpCornersHaveDifferentLocalityAndTinyHooksStayVisible()
    {
        var arc=Enumerable.Range(0,21).Select(i=>new[]{Math.Cos(i*Math.PI/40),Math.Sin(i*Math.PI/40)}).ToArray();
        var smooth=PatchGeometry.Measure(arc,true);var corner=PatchGeometry.Measure([[-1,0],[0,0],[0,1]],true);
        var hook=PatchGeometry.Measure([[0,0],[1,0],[1,.025]],true);
        Assert.True(smooth.Bend>.05);Assert.True(smooth.Turn>75);
        Assert.True(smooth.Locality<.1);Assert.True(corner.Locality>.9);
        Assert.InRange(hook.Hook,.02,.03);Assert.True(hook.Bend<.03);
    }
    [Fact]
    public void CoarseReadsSurviveIndependentRestoreAndSourceSitesAreNotJoinedByCoincidence()
    {
        using var input=Request([[-100,0],[100,0]],[[0,-100],[0,3]]);
        var (root,rows)=PatchEvidence.Build(input.RootElement,"unused");
        var restored=PatchEvidence.Restore(PatchEvidence.Capture("p",root));
        Assert.Equal(rows[0].Bands,Assert.Single(PatchEvidence.Vectors(restored)));
        restored.TryReadOrderedGroup(out var fields);((Relation)fields[1]).TryReadOrderedGroup(out var patches);
        ((Relation)patches[0]).TryReadOrderedGroup(out var patch);var selected=(Relation)patch[0];
        Assert.False(ReferenceEquals(selected.Left,selected.Right));
        ((Relation)selected.Left).TryReadOrderedGroup(out var a);((Relation)selected.Right).TryReadOrderedGroup(out var b);
        ((Relation)a[1]).TryReadOrderedGroup(out var sa);((Relation)b[1]).TryReadOrderedGroup(out var sb);
        Assert.All(sa,site=>Assert.DoesNotContain(site,sb));
    }
    [Fact]
    public void RatiosAndBendBandsAreInvariantUnderUniformScaleAndRotation()
    {
        double[][] a=[[-100,0],[0,0],[100,0]],b=[[0,-100],[0,4]];
        var first=PatchGeometry.ReadPair(a,b,true);
        double[][] Change(double[][] run)=>run.Select(p=>new[]{70+3*(.6*p[0]-.8*p[1]),-11+3*(.8*p[0]+.6*p[1])}).ToArray();
        var second=PatchGeometry.ReadPair(Change(a),Change(b),true);
        Assert.Equal(first.Metrics.Select(PatchGeometry.Band),second.Metrics.Select(PatchGeometry.Band));
    }
    [Fact]
    public void ModelOpinionsKeepAllFiveCandidatesAndDoNotChangePublishedSourceCells()
    {
        using var input=Request([[-100,0],[100,0]],[[0,-100],[0,3]]);
        var (root,rows)=PatchEvidence.Build(input.RootElement,"unused");
        var before=PatchEvidence.Vectors(root)[0];
        using var scores=JsonDocument.Parse("{\"modelSha256\":\"external-model\",\"predictions\":[{\"scores\":[0.03,0.02,0.10,0.75,0.10]}]}");
        var opinion=PatchEvidence.Scores(root,scores.RootElement);
        Assert.Equal(1,PatchEvidence.ValidateScores(opinion));
        Assert.Equal(before,PatchEvidence.Vectors(root)[0]);
        opinion.TryReadOrderedGroup(out var f);((Relation)f[2]).TryReadOrderedGroup(out var o);
        ReadWitness.TryRead((Relation)o[0],out _,out _,out var result,out _,out _);
        ((Relation)result).TryReadOrderedGroup(out var choices);Assert.Equal(5,choices.Length);
        using var invalid=JsonDocument.Parse("{\"modelSha256\":\"m\",\"predictions\":[{\"scores\":[-1,0,0,0,2]}]}");
        Assert.Throws<InvalidDataException>(()=>PatchEvidence.Scores(root,invalid.RootElement));
        var wrong=ReadWitness.Create(new Carrier(),new Placeholder(),f[1],result,new Placeholder(),new Placeholder());
        var disconnected=PatchEvidence.G(f[0],f[1],PatchEvidence.G(wrong));
        Assert.Throws<InvalidDataException>(()=>PatchEvidence.ValidateScores(disconnected));
    }
    [Fact]
    public void OrderedBeforeAndAfterIntervalsShareTheirWholeAndCutInNativeStructure()
    {
        using var input=Request([[-100,0],[100,0]],[[0,-100],[0,3]]);
        var (root,_)=PatchEvidence.Build(input.RootElement,"unused");
        var read=PatchEvidence.Restore(PatchEvidence.Capture("p",root));
        read.TryReadOrderedGroup(out var fields);((Relation)fields[1]).TryReadOrderedGroup(out var patches);
        ((Relation)patches[0]).TryReadOrderedGroup(out var patch);((Relation)patch[1]).TryReadOrderedGroup(out var cells);
        Relation Definition(IElement cell)
        {
            ReadWitness.TryRead((Relation)cell,out var metric,out _,out _,out _,out _);
            ReadWitness.TryRead((Relation)metric,out _,out var basis,out _,out _,out _);
            ((Relation)basis).TryReadOrderedGroup(out var b);return (Relation)b[0];
        }
        var before=Definition(cells[0]);var after=Definition(cells[1]);
        Assert.Same(before.Left,after.Left);
        Assert.Same(((Relation)before.Right).Right,((Relation)after.Right).Left);
    }
}
