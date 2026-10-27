using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using GojoQuickDraw.Learning;
using static GojoQuickDraw.Learning.NeighborhoodGraph;
using static GojoQuickDraw.Learning.NeighborhoodProposals;

namespace GojoQuickDraw.Learning.Tests;

public sealed class NeighborhoodTests
{
    private static Graph GraphOf(double[][] sites,int[][] edges)
    {
        using var input=JsonDocument.Parse(JsonSerializer.Serialize(new{sites,edges=edges.Select(e=>new{a=e[0],b=e[1]})}));
        return FromJson(input.RootElement);
    }
    private static JsonDocument ConstantModels(double probability=.98)
    {
        object Model(int count)=>new{columns=Enumerable.Range(0,count).SelectMany(i=>Enumerable.Range(0,6).Select(b=>$"{i}:{b}")).Concat(new[]{"bias"}).ToArray(),
            weights=Enumerable.Repeat(0.0,count*6).Concat(new[]{Math.Log(probability/(1-probability))}).ToArray()};
        return JsonDocument.Parse(JsonSerializer.Serialize(new{repair=Model(24),line=Model(8)}));
    }
    [Fact]
    public void CoordinateEqualityIsNotSourceSiteIdentityAndCutReallyCreatesThreeBranches()
    {
        var g=GraphOf([[-100,0],[100,0],[0,4],[0,100]],[[0,1],[2,3]]);
        var focus=Neighborhood(g,[0,2],g.Edges);var c=Join(g,g.Edges[0],g.Edges[1],Closest(g.Edges[0],g.Edges[1]),focus,false);
        var tee=Assert.Single(c.After.Sites,s=>Kind(c.After,s)=="T");
        Assert.Equal(3,Degree(c.After,tee));
        var view=Project(c.After,g.Edges.Select(e=>e.Source).ToArray());
        var site=Assert.Single(view.Sites,s=>s.Kind=="T");Assert.Equal(3,site.Edges.Length);Assert.Equal(3,site.FarEndpoints.Distinct().Count());
        var pieces=c.After.Edges.Where(e=>ReferenceEquals(e.Source,g.Edges[0].Source)).ToArray();
        Assert.Equal(2,pieces.Length);Assert.Same(pieces[0].Span.Right,pieces[1].Span.Left);
        Assert.Same(g.Edges[0].Span.Left,pieces[0].Span.Left);Assert.Same(g.Edges[0].Span.Right,pieces[1].Span.Right);
        Assert.Equal(4,g.Sites.Length);Assert.All(g.Sites,s=>Assert.Equal(1,Degree(g,s)));
        var equal=GraphOf([[-10,0],[0,0],[0,0],[0,10]],[[0,1],[2,3]]);
        Assert.Equal(XY(equal.Sites[1]),XY(equal.Sites[2]));Assert.NotSame(equal.Sites[1],equal.Sites[2]);Assert.Equal(2,Components(equal));
    }
    [Fact]
    public void TrimmingKeepsRemovedSourceIntervalAndCannotInventAnExtraArm()
    {
        var g=GraphOf([[-100,0],[100,0],[0,-3],[0,100]],[[0,1],[2,3]]);
        var c=Join(g,g.Edges[0],g.Edges[1],Closest(g.Edges[0],g.Edges[1]),Neighborhood(g,[0,0],g.Edges),true);
        Assert.True(c.Admissible);var removed=Assert.Single(c.Removed);Assert.Same(g.Edges[1].Source,removed.Source);
        Assert.True(removed.Span.TryReadSegment(out _,out var a,out var b));Assert.InRange((b.Ticks-a.Ticks)/(double)PatchEvidence.Q,.02,.04);
        Assert.Single(c.After.Sites,s=>Kind(c.After,s)=="T");
        var corner=GraphOf([[-100,0],[0,0],[0,100]],[[0,1],[1,2]]);
        Assert.DoesNotContain(Generate(corner),c=>c.After.Sites.Any(s=>Kind(c.After,s)=="T"));
    }
    [Fact]
    public void SeveralSitesCanMergeWithAllTheirActualFarEndpoints()
    {
        var g=GraphOf([[-100,0],[-2,0],[2,0],[100,0],[0,3],[0,100]],[[0,1],[2,3],[4,5]]);
        var near=new[]{g.Sites[1],g.Sites[2],g.Sites[4]};var c=Merge(g,near,Neighborhood(g,[0,1],g.Edges));
        Assert.True(c.Admissible);Assert.Equal(3,c.Bindings.Length);Assert.Single(c.After.Sites,s=>Kind(c.After,s)=="T");Assert.Equal(1,Components(c.After));
    }
    [Fact]
    public void ParallelObjectsCannotCollapseToCoincidentRaysAndEnclosuresCannotDisappear()
    {
        var g=GraphOf([[0,0],[100,0],[0,2],[100,2]],[[0,1],[2,3]]);
        var c=Merge(g,[g.Sites[0],g.Sites[2]],Neighborhood(g,[0,1],g.Edges));Assert.False(c.Admissible);
        var box=GraphOf([[0,0],[100,0],[100,100],[0,100]],[[0,1],[1,2],[2,3],[3,0]]);
        var cut=Trim(box,box.Edges[0],box.Sites[0],Neighborhood(box,[50,0],box.Edges));Assert.False(cut.Admissible);Assert.Contains(cut.Issues,s=>s.Contains("enclosure"));
    }
    [Fact]
    public void NeighborhoodRadiusScalesWithItsLocalAnchorsAndCellsIgnorePageRotation()
    {
        double[][] sites=[[-100,0],[100,0],[0,3],[0,100]];int[][] edges=[[0,1],[2,3]];
        var a=GraphOf(sites,edges);var b=GraphOf(sites.Select(p=>new[]{51+3*(.6*p[0]-.8*p[1]),-19+3*(.8*p[0]+.6*p[1])}).ToArray(),edges);
        Candidate C(Graph g)=>Join(g,g.Edges[0],g.Edges[1],Closest(g.Edges[0],g.Edges[1]),Neighborhood(g,Closest(g.Edges[0],g.Edges[1]).P,g.Edges),false);
        var first=C(a);var second=C(b);Assert.InRange(second.Focus.Radius/first.Focus.Radius,2.999,3.001);
        Assert.Equal(NeighborhoodFeatures.Repair(first).Bands,NeighborhoodFeatures.Repair(second).Bands);
    }
    [Fact]
    public void DisabledProvidersAreAnIdentityNoOpAndAppliedGraphSurvivesIndependentRestore()
    {
        var g=GraphOf([[-100,0],[100,0],[0,4],[0,100]],[[0,1],[2,3]]);using var models=ConstantModels();
        var none=NeighborhoodRun.Execute(g.Surface,g,models.RootElement,"test",false,false);
        Assert.Same(g.Surface,NeighborhoodRun.RootFields(none.Surface)[3]);
        var active=NeighborhoodRun.Execute(g.Surface,g,models.RootElement,"test",true,false);
        var doc=GojoStructureCompactJson.Deserialize(PatchEvidence.Capture("repair",active.Surface));
        Assert.True(GojoStructureCompactJson.TryToElement(doc,out var restored));var root=Assert.IsType<Relation>(restored.Root);NeighborhoodRun.Validate(root);
        var f=NeighborhoodRun.RootFields(root);var final=Read((Relation)f[3]);Assert.Single(final.Sites,s=>Kind(final,s)=="T");
        var wrong=PatchEvidence.G(f[0],f[1],f[2],f[1],f[4]);Assert.Throws<InvalidDataException>(()=>NeighborhoodRun.Validate(wrong));
    }
    [Fact]
    public void HandleCoordinatesCannotMasqueradeAsTheDeclaredGraphBasis()
    {
        var g=GraphOf([[0,0],[100,0]],[[0,1]]);var alien=Frame();var handle=Site(alien,[50,20],new Placeholder());
        var edge=g.Edges[0];var bad=Gojo.Structures.ReadWitness.Create(new Carrier(),new Relation(edge.Source,edge.Span),g.Frame,
            PatchEvidence.G(new Relation(edge.A,edge.B),PatchEvidence.G(handle)),PatchEvidence.G(),edge.Witness);
        Assert.Throws<InvalidDataException>(()=>Read(PatchEvidence.G(g.Frame,PatchEvidence.G(g.Sites),PatchEvidence.G(bad))));
    }
    [Fact]
    public void OneSourceIntervalCannotBeDuplicatedToInventAdditionalBranches()
    {
        var g=GraphOf([[0,0],[100,0],[50,40]],[[0,1],[1,2]]);var first=g.Edges[0];
        var duplicate=Curve(g.Frame,first.Source,first.Span,g.Sites[0],g.Sites[2],[],first.Witness);
        Assert.Throws<InvalidDataException>(()=>Create(g.Frame,[first,duplicate]));
    }
    [Fact]
    public void AProposedAlignmentCollapsingAnotherBranchIsBlockedWithoutLosingItsSource()
    {
        var g=GraphOf([[0,1],[-100,0],[100,0],[0,0]],[[0,1],[0,2],[0,3]]);
        var at=g.Sites[0];var c=Align(g,at,g.Edges[0],g.Edges[1],Neighborhood(g,XY(at),g.Edges))!;
        Assert.False(c.Admissible);Assert.Contains(c.Issues,s=>s.Contains("zero geometry"));
        using var bundle=ConstantModels();var opinion=NeighborhoodRun.Evaluate(c,bundle.RootElement,"test",true,true);
        Assert.True(double.IsNegativeInfinity(opinion.Utility));Assert.Equal(3,g.Edges.Length);
    }
    [Fact]
    public void CoarseCellsCarryTheirActualCutsAndRejectAChangedGradeOnTheSameAxis()
    {
        var g=GraphOf([[0,0],[100,0]],[[0,1]]);var cells=NeighborhoodFeatures.Line(g.Edges[0]);
        Assert.Equal(cells.Bands,NeighborhoodFeatures.ReadCells(cells.Surface));
        var rows=Rows(cells.Surface);var cell=(Relation)rows[0];
        Gojo.Structures.ReadWitness.TryRead(cell,out var source,out var basis,out var result,out var residue,out var provenance);
        var grade=Assert.IsType<CarrierLocation>(result);
        rows[0]=Gojo.Structures.ReadWitness.Create(new Carrier(),source,basis,new CarrierLocation(grade.Carrier,(grade.Ticks+1)%6),residue,provenance);
        Assert.Throws<InvalidDataException>(()=>NeighborhoodFeatures.ReadCells(PatchEvidence.G(rows)));
    }
}
