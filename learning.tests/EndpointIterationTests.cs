using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Structures;
using GojoQuickDraw.Learning;
using static GojoQuickDraw.Learning.NeighborhoodGraph;
using static GojoQuickDraw.Learning.NeighborhoodProposals;

namespace GojoQuickDraw.Learning.Tests;

public sealed class EndpointIterationTests
{
    private static Graph GraphOf(double[][] sites,int[][] edges)
    {
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(new{sites,edges=edges.Select(e=>new{a=e[0],b=e[1]})}));return FromJson(json.RootElement);
    }
    private static JsonDocument ConstantModels()
    {
        object Head(int count)=>new{columns=Enumerable.Range(0,count).SelectMany(i=>Enumerable.Range(0,6).Select(b=>$"{i}:{b}")).Prepend("bias").ToArray(),
            weights=Enumerable.Repeat(0.0,count*6).Prepend(Math.Log(.98/.02)).ToArray()};
        return JsonDocument.Parse(JsonSerializer.Serialize(new{line=Head(8),repair=Head(24)}));
    }
    [Fact]
    public void TwoEndpointsCanBecomeACornerAndACornerPlusEndpointCanBecomeATee()
    {
        var corner=GraphOf([[-100,0],[0,0],[0,4],[0,100]],[[0,1],[2,3]]);
        Assert.Contains(Generate(corner),c=>c.Admissible&&c.After.Sites.Any(s=>Kind(c.After,s)=="L"));
        var tee=GraphOf([[-100,0],[0,0],[0,100],[4,0],[100,0]],[[0,1],[1,2],[3,4]]);
        var proposal=Assert.Single(Generate(tee).Where(c=>c.Observation is not null&&c.Admissible&&c.After.Sites.Any(s=>Kind(c.After,s)=="T")).Take(1));
        var at=Assert.Single(proposal.After.Sites,s=>Kind(proposal.After,s)=="T");Assert.Equal(3,Degree(proposal.After,at));
        Assert.Contains(proposal.Bindings,b=>ReferenceEquals(b.Left,tee.Sites[1]));
    }
    [Fact]
    public void AnEndpointCanProjectOntoOtherSegmentEvenWhenTheirFarEndsAlreadyMeet()
    {
        using var json=JsonDocument.Parse("""
            {"sites":[[-100,0],[100,0],[0,4]],"edges":[{"a":0,"b":1},{"a":1,"b":2,"handles":[[100,100],[-100,100]]}]}
            """);
        var graph=FromJson(json.RootElement);var tip=graph.Edges[1].B;
        var contact=NeighborhoodContacts.Discover(graph).Single(c=>ReferenceEquals(c.Endpoint,tip)&&ReferenceEquals(c.Other.Witness,graph.Edges[0].Witness));
        Assert.InRange(contact.T,.49999,.50001);Assert.NotSame(contact.Point,tip);
        Assert.Equal(1,Degree(graph,tip));Assert.DoesNotContain(graph.Sites,s=>ReferenceEquals(s,contact.Point));
        var candidate=Attach(graph,contact);Assert.True(candidate.Admissible);Assert.Equal(3,Degree(candidate.After,contact.Point));
        Assert.Equal("T",Kind(candidate.After,contact.Point));
        var split=candidate.After.Edges.Where(e=>ReferenceEquals(e.Source,graph.Edges[0].Source)).ToArray();
        Assert.Same(contact.Parameter,split[0].Span.Right);Assert.Same(contact.Parameter,split[1].Span.Left);
        Assert.Contains(Generate(graph),c=>c.Observation is not null&&c.Admissible&&c.After.Sites.Any(s=>Kind(c.After,s)=="T"));
        Assert.Throws<InvalidDataException>(()=>NeighborhoodContacts.Read(contact.Witness,candidate.After));
    }
    [Fact]
    public void AProjectionIsASeparateObservationAndCannotRetargetItselfByChangingItsParameter()
    {
        var graph=GraphOf([[-100,0],[100,0],[0,4],[0,100]],[[0,1],[2,3]]);
        var contact=NeighborhoodContacts.Discover(graph).First(c=>ReferenceEquals(c.Other.Witness,graph.Edges[0].Witness));
        ReadWitness.TryRead(contact.Witness,out var selected,out var basis,out var result,out var residue,out var source);
        var fields=Fields(result,4);contact.Other.Span.TryReadSegment(out var axis,out _,out _);fields[0]=Relation.Point(axis,10000);
        var falseRead=ReadWitness.Create(new Carrier(),selected,basis,PatchEvidence.G(fields),residue,source);
        Assert.Throws<InvalidDataException>(()=>NeighborhoodContacts.Read(falseRead,graph));
        Assert.All(graph.Sites,s=>Assert.Equal(1,Degree(graph,s)));
    }
    [Fact]
    public void StraighteningCreatesNewConnectionEvidenceInTheNextPassWithTheSameModels()
    {
        using var json=JsonDocument.Parse("""
            {"sites":[[-100,0],[100,0],[0,-5],[0,-100]],"edges":[{"a":0,"b":1,"handles":[[-33,20],[33,20]]},{"a":2,"b":3}]}
            """);
        var graph=FromJson(json.RootElement);using var models=ConstantModels();
        var one=NeighborhoodRun.Execute(graph.Surface,graph,models.RootElement,"fixed-model",true,true,1);
        var oneFinal=Read((Relation)NeighborhoodRun.RootFields(one.Surface)[3]);Assert.DoesNotContain(oneFinal.Sites,s=>Kind(oneFinal,s)=="T");
        var repeated=NeighborhoodRun.Execute(graph.Surface,graph,models.RootElement,"fixed-model",true,true,4);
        var f=NeighborhoodRun.RootFields(repeated.Surface);var final=Read((Relation)f[3]);Assert.Single(final.Sites,s=>Kind(final,s)=="T");
        Assert.True(Rows(f[2]).Length>=2);var rounds=Rows(f[2]).Select(Rows).ToArray();Assert.Same(rounds[0][2],rounds[1][0]);
        var doc=GojoStructureCompactJson.Deserialize(PatchEvidence.Capture("endpoint-feedback",repeated.Surface));
        Assert.True(GojoStructureCompactJson.TryToElement(doc,out var restored));NeighborhoodRun.Validate((Relation)restored.Root);
    }
    [Fact]
    public void AnUnchangedInputStopsWithoutAccumulatingRepeatedOpinions()
    {
        var graph=GraphOf([[-100,0],[100,0],[0,4],[0,100]],[[0,1],[2,3]]);using var model=ConstantModels();
        var run=NeighborhoodRun.Execute(graph.Surface,graph,model.RootElement,"unselected",false,false,8);var fields=NeighborhoodRun.RootFields(run.Surface);
        Assert.Same(graph.Surface,fields[3]);Assert.Single(Rows(fields[2]));
        var controller=Fields(fields[5],4);Assert.Equal(1,Assert.IsType<CarrierLocation>(controller[1]).Ticks);
        var transition=Assert.IsType<Relation>(controller[2]);Assert.Same(transition.Left,transition.Right);
    }
}
