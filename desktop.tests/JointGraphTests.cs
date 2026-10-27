using System.Text.Json;
using GojoQuickDraw.Reads;
using GojoQuickDraw.Desktop;

namespace GojoQuickDraw.Desktop.Tests;

public sealed class JointGraphTests
{
    [Fact]
    public void DirectionalTopologyReadsAllJunctionTypes()
    {
        foreach(var (expected,angles) in new (string,double[])[]{("L",[0,90]),("I",[0,180]),("T",[0,90,180]),
            ("Y",[0,120,240]),("X",[0,90,180,270]),("K",[0,40,100,180])})
            Assert.Equal(expected,JointGraphRead.Classify(angles));
    }

    [Fact]
    public void CrossIntersectionIsVisibleButDoesNotInventSharedSiteIdentity()
    {
        JointEdge[] edges=[new("h","cross",[-10,0],[10,0],"a","b",["p0"]),new("v","cross",[0,-10],[0,10],"c","d",["p1"])];
        var cross=Assert.Single(JointGraphRead.Final(edges).Nodes,n=>n.Kind=="X");
        Assert.False(cross.Shared);Assert.Equal(2,cross.On.Length);Assert.Equal(["p0","p1"],cross.Sources);
        Assert.Equal("a",edges[0].SiteA);
    }

    [Fact]
    public void ClosedFrameWithBarsKeepsTContactsCenterAndClosedWalks()
    {
        double[][] points=[[0,10],[0,0],[10,0],[10,10],[0,5],[5,0],[10,5],[5,10],[5,5]];
        int[][] segments=[[0,4],[4,1],[1,5],[5,2],[2,6],[6,3],[3,7],[7,0],[5,8],[8,7],[4,8],[8,6]];
        var graph=JointGraphRead.Final(segments.Select((s,i)=>new JointEdge("e"+i,"window",points[s[0]],points[s[1]],"s"+s[0],"s"+s[1],["p"+i])).ToArray());
        Assert.Equal(4,graph.Nodes.Count(n=>n.Kind=="T"));Assert.Equal(4,graph.Nodes.Count(n=>n.Kind=="L"));
        Assert.True(Assert.Single(graph.Nodes,n=>n.Kind=="X").Shared);
        Assert.Equal(4,graph.Nodes.Count(n=>n.Kind=="O"));
        Assert.NotEmpty(graph.Links);Assert.NotEmpty(graph.Orders);
        var export=JsonSerializer.SerializeToElement(JointGraphRead.TrainingView(JointGraphRead.Compare(JointGraphRead.Graph([]),graph,"test")));
        var cycles=export.GetProperty("final").GetProperty("nodes").EnumerateArray().Where(n=>n.GetProperty("kind").GetString()=="O").ToArray();
        Assert.All(cycles,n=>Assert.True(n.GetProperty("on").GetArrayLength()>=3));
    }

    private static JointNode Node(string id,string kind,double x,double y,string[] sources)=>new(id,kind,[x,y],[0,90],[],sources,false,0,"fixture");
    [Fact]
    public void MatchingUsesSourceLinksInsteadOfPositionOrDesiredTypeAndKeepsTiesOpen()
    {
        var source=JointGraphRead.Graph([Node("a","L",0,0,["p0","p1"]),Node("b","T",30,50,["p2","p3"])]);
        var final=JointGraphRead.Graph([Node("fa","T",300,100,["p0","p1"]),Node("fb","T",400,200,["p2","p3"]),Node("new","L",0,0,[])]);
        var read=JointGraphRead.Compare(source,final,"test");
        Assert.Equal("changed type",read.Matches[0].Status);Assert.Equal("a",Assert.Single(read.Matches[0].Candidates));
        Assert.Equal("unmapped",read.Matches[2].Status);Assert.True(Assert.Single(read.OrderChecks).Same);
        var tied=JointGraphRead.Compare(JointGraphRead.Graph([source.Nodes[0],source.Nodes[0] with {Id="other"}]),final,"test");
        Assert.Equal("ambiguous",tied.Matches[0].Status);Assert.Equal(2,tied.Matches[0].Candidates.Length);
        var json=JsonSerializer.Serialize(JointGraphRead.TrainingView(read),JointPreview.Options);
        Assert.DoesNotContain("\"point\"",json);Assert.DoesNotContain("\"gap\"",json);Assert.Contains("relativeOrder",json);
    }
}
