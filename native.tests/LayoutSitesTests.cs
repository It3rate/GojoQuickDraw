using System.Text.Json.Nodes;
using Gojo.Core;
using Gojo.Encoding.Structures;
using GojoQuickDraw.Native;

namespace GojoQuickDraw.Native.Tests;

public sealed partial class NativeRecordTests
{
    [Fact]
    public void ProposedRoofSharesActualWallSitesWithoutAliasingAcquisition()
    {
        var packet = ObjectFixture();
        packet["context"]!["parts"]![1]!["participants"] = new JsonArray("wall");
        packet["context"]!["parts"]![1]!["geometry"]!["siteSources"] = JsonNode.Parse("""
            [{"part":"wall","index":0},null,{"part":"wall","index":1}]
            """);
        var fields = Entries(Import(packet));
        var context = Entries(fields[2]); context[5] = EncodedTextSurface.Create(new Carrier(), "{}");
        fields[2] = G(context);
        var restored = NativeRecord.Restore(NativeRecord.Capture("shared-layout", G(fields)));
        var wall = restored.Context.Parts[0]; var roof = restored.Context.Parts[1];
        Assert.Same(wall.Points[0], roof.Points[0]);
        Assert.Same(wall.Points[1], roof.Points[2]);
        Assert.NotSame(wall.Points[0], roof.Points[1]);
        Assert.Same(roof.Points[0], roof.Segments[0].Left);
        Assert.Same(roof.Points[2], roof.Segments[2].Left);
        Assert.DoesNotContain(roof.Points[0].Left, restored.Acquisition.Samples.Select(p => (IElement)p.Event));
        var separate = NativeRecord.Restore(NativeRecord.Capture("equal-not-shared", Import(ObjectFixture())));
        Assert.NotSame(separate.Context.Parts[0].Points[0], separate.Context.Parts[1].Points[0]);
    }

    [Theory]
    [InlineData("missing", 0, true)]
    [InlineData("window-a", 0, true)]
    [InlineData("wall", 99, true)]
    [InlineData("wall", 1, true)]
    [InlineData("wall", 0, false)]
    public void LayoutSiteImportsRejectUnselectedForwardMissingOrUnequalSites(string part, int index, bool selected)
    {
        var packet = ObjectFixture(); var roof = packet["context"]!["parts"]![1]!;
        roof["participants"] = selected ? new JsonArray("wall") : new JsonArray();
        roof["geometry"]!["siteSources"] = new JsonArray(new JsonObject { ["part"] = part, ["index"] = index }, null, null);
        Assert.Throws<InvalidDataException>(() => Import(packet));
    }
}
