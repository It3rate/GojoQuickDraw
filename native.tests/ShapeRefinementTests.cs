using System.Text.Json.Nodes;
using Gojo.Core;
using Gojo.Encoding.Structures;
using GojoQuickDraw.Native;

namespace GojoQuickDraw.Native.Tests;

public sealed partial class NativeRecordTests
{
    [Fact]
    public void RefinedPeakContactAndBeforeAfterCorrespondenceSurviveWithoutLabels()
    {
        var fields=Entries(Import(RefinementFixture()));
        var context=Entries(fields[2]);context[5]=EncodedTextSurface.Create(new Carrier(),"{}");fields[2]=G(context);
        var refinement=Entries(fields[5]);refinement[5]=EncodedTextSurface.Create(new Carrier(),"{}");fields[5]=G(refinement);
        var read=NativeRecord.Restore(NativeRecord.Capture("shape-refinement",G(fields)));
        var result=Assert.IsType<ShapeRefinementEvidence.Read>(read.Refinement);
        Assert.Equal(3,result.Parts.Length);Assert.Equal(2,result.Correspondences.Length);Assert.Equal(3,result.Contacts.Length);
        Assert.Equal("{}",result.AdapterJson);
        var before=read.Context.Parts[1];var roof=read.Context.Parts[8];var cross=read.Context.Parts[9];
        Assert.Same(roof.Points[1],cross.Points[1]);
        Assert.NotSame(before.Points[1],roof.Points[1]);
        Assert.Contains(result.Correspondences[1].SitePairs,p=>ReferenceEquals(p.Left,before.Points[1])&&ReferenceEquals(p.Right,roof.Points[1]));
        Assert.Empty(cross.Profiles);Assert.Same(roof.Witness,Assert.Single(cross.Parts));
    }

    [Theory]
    [InlineData("equal-is-not-shared")]
    [InlineData("missing-correspondence")]
    [InlineData("wrong-site")]
    [InlineData("claimed-ink")]
    public void RefinementRejectsMissingOrInventedSupport(string damage)
    {
        var p=RefinementFixture();
        switch(damage)
        {
            case "equal-is-not-shared":p["context"]!["parts"]![9]!["geometry"]!["siteSources"]![1]=null;break;
            case "missing-correspondence":p["cleanup"]!["correspondences"]!.AsArray().RemoveAt(1);break;
            case "wrong-site":p["cleanup"]!["correspondences"]![1]!["points"]![1]![1]=99;break;
            case "claimed-ink":p["context"]!["parts"]![9]!["spans"]=new JsonArray("s0");break;
        }
        Assert.Throws<InvalidDataException>(()=>Import(p));
    }

    [Fact]
    public void SplitWallsRetainOneSourceAndSharedTowerEdgesWithoutLabels()
    {
        var packet=MappingFixture();var parts=packet["context"]!["parts"]!.AsArray();
        foreach(var row in JsonNode.Parse("""
        [
          {"id":"frame","spans":[],"participants":["wall"],"bounds":[0,0,10,20],"geometry":{"points":[[0,0],[10,0],[10,20],[0,20]],"segments":[]}},
          {"id":"left","spans":[],"participants":["wall","frame"],"bounds":[0,0,4,20],"geometry":{"points":[[0,0],[4,0],[4,20],[0,20]],"segments":[[0,1],[1,2],[2,3],[3,0]],"siteSources":[{"part":"frame","index":0},null,null,{"part":"frame","index":3}]}},
          {"id":"right","spans":[],"participants":["wall","frame"],"bounds":[6,0,10,20],"geometry":{"points":[[6,0],[10,0],[10,20],[6,20]],"segments":[[0,1],[1,2],[2,3],[3,0]],"siteSources":[null,{"part":"frame","index":1},{"part":"frame","index":2},null]}},
          {"id":"tower","spans":[],"participants":["wall","frame","left","right"],"bounds":[4,-10,6,20],"geometry":{"points":[[4,-10],[6,-10],[6,20],[4,20],[4,0],[6,0]],"segments":[[0,1],[1,5],[5,2],[2,3],[3,4],[4,0]],"siteSources":[null,null,{"part":"right","index":3},{"part":"left","index":2},{"part":"left","index":1},{"part":"right","index":0}]}}
        ]
        """)!.AsArray())parts.Add(row!.DeepClone());
        packet["cleanup"]=JsonNode.Parse("""
        {"sourceId":"fixture","parts":[{"id":"frame"},{"id":"left"},{"id":"right"},{"id":"tower"}],
         "correspondences":[{"source":"wall","target":"frame","points":[[0,0],[1,1],[2,2],[3,3]]},
                            {"source":"wall","target":"left","points":[[0,0],[1,1],[2,2],[3,3]]},
                            {"source":"wall","target":"right","points":[[0,0],[1,1],[2,2],[3,3]]},
                            {"source":"wall","target":"tower","points":[[0,0],[1,1],[2,2],[3,3],[0,4],[1,5]]}],
         "attachments":[{"parent":"left","child":"tower","parentPoint":1,"childPoint":4},
                        {"parent":"left","child":"tower","parentPoint":2,"childPoint":3},
                        {"parent":"right","child":"tower","parentPoint":0,"childPoint":5},
                        {"parent":"right","child":"tower","parentPoint":3,"childPoint":2}],"symmetries":[]}
        """);
        var fields=Entries(Import(packet));
        var context=Entries(fields[2]);context[5]=EncodedTextSurface.Create(new Carrier(),"{}");fields[2]=G(context);
        var refinement=Entries(fields[5]);refinement[5]=EncodedTextSurface.Create(new Carrier(),"{}");fields[5]=G(refinement);
        var restored=NativeRecord.Restore(NativeRecord.Capture("split-wall",G(fields)));
        var read=Assert.IsType<ShapeRefinementEvidence.Read>(restored.Refinement);
        Assert.Equal(4,read.Parts.Length);Assert.Equal(4,read.Correspondences.Length);Assert.Equal(4,read.Contacts.Length);
        Assert.All(read.Correspondences,c=>Assert.Same(restored.Context.Parts[0].Witness,c.Source));
        var frame=restored.Context.Parts[7];var left=restored.Context.Parts[8];var right=restored.Context.Parts[9];var tower=restored.Context.Parts[10];
        Assert.Empty(frame.Segments);Assert.Equal(4,left.Segments.Length);Assert.Equal(4,right.Segments.Length);
        Assert.Same(left.Points[1],tower.Points[4]);Assert.Same(left.Points[2],tower.Points[3]);
        Assert.Same(right.Points[0],tower.Points[5]);Assert.Same(right.Points[3],tower.Points[2]);
        Assert.NotSame(left.Points[1],right.Points[0]);
        Assert.Contains(frame.Witness,left.Parts);Assert.Contains(frame.Witness,right.Parts);
        Assert.All(new[]{frame,left,right,tower},part=>Assert.Empty(part.Profiles));
    }

    private static JsonObject RefinementFixture()
    {
        var packet=MappingFixture();var parts=packet["context"]!["parts"]!.AsArray();
        foreach(var row in JsonNode.Parse("""
        [
          {"id":"cw","spans":[],"participants":["wall"],"bounds":[0,0,10,20],"geometry":{"points":[[0,0],[10,0],[10,20],[0,20]],"segments":[[0,1],[1,2],[2,3],[3,0]]}},
          {"id":"cr","spans":[],"participants":["roof","cw"],"bounds":[0,-8,10,0],"geometry":{"points":[[0,0],[5,-8],[10,0]],"segments":[[0,1],[1,2],[2,0]],"siteSources":[{"part":"cw","index":0},null,{"part":"cw","index":1}]}},
          {"id":"cc","spans":[],"participants":["cr"],"bounds":[5,-12,5,-8],"geometry":{"points":[[5,-12],[5,-8]],"segments":[[0,1]],"siteSources":[null,{"part":"cr","index":1}]}}
        ]
        """)!.AsArray())parts.Add(row!.DeepClone());
        packet["cleanup"]=JsonNode.Parse("""
        {"sourceId":"fixture","parts":[{"id":"cw"},{"id":"cr"},{"id":"cc"}],
         "correspondences":[{"source":"wall","target":"cw","points":[[0,0],[1,1],[2,2],[3,3]]},
                            {"source":"roof","target":"cr","points":[[0,0],[1,1],[2,2]]}],
         "attachments":[{"parent":"cw","child":"cr","parentPoint":0,"childPoint":0},
                        {"parent":"cw","child":"cr","parentPoint":1,"childPoint":2},
                        {"parent":"cr","child":"cc","parentPoint":1,"childPoint":1}],"symmetries":[]}
        """);
        return packet;
    }
}
