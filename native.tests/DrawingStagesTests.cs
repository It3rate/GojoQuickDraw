using System.Text.Json.Nodes;
using Gojo.Core;
using GojoQuickDraw.Native;

namespace GojoQuickDraw.Native.Tests;

public sealed partial class NativeRecordTests
{
    private static JsonObject StepsFixture()
    {
        var packet = RefinementFixture();
        var program = packet["program"]!.DeepClone().AsObject();
        // A small four-rung program already used for the actual fitted source.
        // Add non-draining correspondence accounting for every source interval.
        var passes = new JsonArray();
        for (var rung = 0; rung < 4; rung++)
        {
            var links = new JsonArray(); var unmatched = new JsonArray();
            for (var span = 0; span < 2; span++)
                if (rung >= span + 2)
                    links.Add(new JsonObject { ["spanIds"] = new JsonArray($"s{span}"), ["edgeIds"] = new JsonArray($"edge{span}") });
                else unmatched.Add($"s{span}");
            passes.Add(new JsonObject { ["rung"] = rung, ["links"] = links, ["unmatchedSpanIds"] = unmatched,
                ["inferredEdgeIds"] = rung < 2 ? new JsonArray("rough") : new JsonArray(), ["residueEdgeIds"] = new JsonArray() });
        }
        program["sourceMapping"]!["passes"] = passes;
        packet["analysis"] = new JsonObject { ["constructionProgram"] = program };
        return packet;
    }

    [Fact]
    public void AllDrawingRungsAndSourceCorrespondencesRestoreWithoutPreviewSidecars()
    {
        var restored = NativeRecord.Restore(NativeRecord.Capture("stages", Import(StepsFixture())));
        var stages = Assert.IsType<Gojo.Experiments.QuickDraw.QuickDrawGestaltEvidence.Read>(restored.DrawingSteps);
        Assert.Equal(4, stages.Construction.Rungs.Length);
        Assert.Equal(4, stages.Passes.Length);
        Assert.Equal(2, stages.Passes[0].Unmatched.Length);
        Assert.Empty(stages.Passes[3].Unmatched);
        Assert.Equal(2, stages.Passes[3].Links.Length);
        Assert.Equal(restored.Acquisition.Samples.Count, stages.Acquisition.Samples.Count);
        Assert.NotSame(restored.Acquisition.Samples[0].Event, stages.Acquisition.Samples[0].Event);
    }

    [Fact]
    public void MatchingCoordinatesCannotSubstituteForTheOrderedSourceCorrespondence()
    {
        var root = Import(StepsFixture()); var fields = Entries(root); var stages = Entries(fields[6]);
        // Stroke 0 ends at (10,0); stroke 1 starts at exactly (10,0).
        // Their acquired events still must not substitute for one another.
        var pairs = Entries(stages[1]); var first = Assert.IsType<Relation>(pairs[2]); var second = Assert.IsType<Relation>(pairs[3]);
        pairs[2] = new Relation(first.Left, second.Right);
        stages[1] = G(pairs); fields[6] = G(stages);
        Assert.False(NativeRecord.TryRead(G(fields), out _));
    }
}
