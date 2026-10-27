using Gojo.Core;
using System.Text.Json.Nodes;
using Gojo.Encoding.Structures;
using GojoQuickDraw.Native;

namespace GojoQuickDraw.Native.Tests;

public sealed partial class NativeRecordTests
{
    [Fact]
    public void RefiningAnInferredAreaDoesNotTurnItIntoDrawnInk()
    {
        var packet=RefinementFixture();
        packet["context"]!["parts"]!.AsArray().Single(p=>p!["id"]!.GetValue<string>()=="roof")!["spans"]=new JsonArray();
        foreach(var target in packet["boundaryMapping"]!["targets"]!.AsArray().Where(p=>p!["part"]!.GetValue<string>()=="roof"))
        {
            target!["ink"]=new JsonArray();
            target["targetRemainder"]=JsonNode.Parse("[[0,1]]");
        }
        var restored=NativeRecord.Restore(NativeRecord.Capture("inferred-area",Import(packet)));
        var read=JointReadback.Read(restored);
        Assert.All(read.LineMatches.Where(l=>l.FinalEdge.StartsWith("f1/",StringComparison.Ordinal)),l=>Assert.Equal("added",l.Status));
        Assert.Contains(read.LineMatches,l=>l.FinalEdge.StartsWith("f0/",StringComparison.Ordinal)&&l.Status=="mapped boundary support");
    }

    [Fact]
    public void RecalculatedJointGraphSurvivesLabelRemovalWithActualSiteAndSourceLinks()
    {
        var fields=Entries(Import(RefinementFixture()));
        var context=Entries(fields[2]);context[5]=EncodedTextSurface.Create(new Carrier(),"{}");fields[2]=G(context);
        var mapping=Entries(fields[4]);mapping[4]=EncodedTextSurface.Create(new Carrier(),"{}");fields[4]=G(mapping);
        var refinement=Entries(fields[5]);refinement[5]=EncodedTextSurface.Create(new Carrier(),"{}");fields[5]=G(refinement);
        var restored=NativeRecord.Restore(NativeRecord.Capture("joint-readback",G(fields)));
        var read=JointReadback.Read(restored);
        Assert.Equal(restored.Annotation!.Joints.Length,read.Source.Nodes.Length);
        Assert.Contains(read.Final.Nodes,n=>n.Kind=="Y" && n.Shared);
        Assert.Contains(read.LineMatches,l=>l.SourceProfiles.Contains("p0"));
        Assert.Contains(read.Final.Nodes,n=>n.Added);
        Assert.NotEmpty(read.Final.Orders);
        Assert.Equal("{}",restored.Context.AdapterJson);
        Assert.Equal("{}",restored.Refinement!.AdapterJson);
    }
}
