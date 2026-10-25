using System.Text.Json.Nodes;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Structures;
using GojoQuickDraw.Native;

namespace GojoQuickDraw.Native.Tests;

public sealed partial class NativeRecordTests
{
    [Fact]
    public void ObjectMembershipNestingAndAlternativesSurviveWithoutAdapterLabels()
    {
        var root = Import(ObjectFixture());
        var fields = Entries(root); var contextFields = Entries(fields[2]);
        contextFields[5] = EncodedTextSurface.Create(new Carrier(), "{}");
        fields[2] = G(contextFields);
        var read = NativeRecord.Restore(NativeRecord.Capture("objects-without-labels", G(fields)));
        var parts = read.Context.Parts;
        Assert.Equal(7, parts.Length); Assert.Equal("{}", read.Context.AdapterJson);
        var wall = parts[0]; var roof = parts[1]; var firstWindow = parts[2];
        var secondWindow = parts[3]; var alternative = parts[4]; var building = parts[5]; var assembly = parts[6];

        Assert.Same(read.Annotation!.Profiles[0].Witness, Assert.Single(firstWindow.Profiles));
        Assert.Same(read.Annotation.Profiles[1].Witness, Assert.Single(secondWindow.Profiles));
        Assert.NotSame(firstWindow.Witness, secondWindow.Witness);
        Assert.NotSame(firstWindow.Points[0], secondWindow.Points[0]);
        Assert.Same(firstWindow.Profiles[0], Assert.Single(alternative.Profiles));
        Assert.NotSame(firstWindow.Witness, alternative.Witness);
        Assert.NotSame(firstWindow.Points[0].Left, alternative.Points[0].Left);
        Assert.Equal(PositionTicks(firstWindow.Points[0]), PositionTicks(alternative.Points[0]));

        Assert.Equal(wall.Profiles, building.Profiles);
        Assert.NotSame(wall.Witness, building.Witness);
        Assert.Same(wall.Witness, Assert.Single(building.Parts));
        Assert.Equal([building.Witness, roof.Witness, firstWindow.Witness, secondWindow.Witness], assembly.Parts);
        Assert.Same(wall.Witness, read.Context.Relations[0].Parts[0]);
        Assert.Same(firstWindow.Witness, read.Context.Relations[0].Parts[1]);
        Assert.Same(firstWindow.Witness, read.Context.Relations[2].Parts[0]);
        Assert.Same(secondWindow.Witness, read.Context.Relations[2].Parts[1]);

        foreach (var point in parts.SelectMany(part => part.Points))
            Assert.DoesNotContain(point.Left, read.Acquisition.Samples.Select(sample => (IElement)sample.Event));
        Assert.Equal(5, read.Acquisition.Samples.Count);
    }

    [Fact]
    public void ObjectBoundaryReadsPreserveOrderAndDoNotInferMissingParentBounds()
    {
        var packet = ObjectFixture();
        var read = NativeRecord.Restore(NativeRecord.Capture("object-bounds", Import(packet)));
        Assert.Equal([2000, 4000, 6000, 12000], Margins(read.Context.Relations[0]));
        packet["context"]!["relations"]![0]!["participants"] = new JsonArray("window-a", "wall");
        var reversed = NativeRecord.Restore(NativeRecord.Capture("reversed-object-bounds", Import(packet)));
        Assert.Equal([-2000, -4000, -6000, -12000], Margins(reversed.Context.Relations[0]));

        // Membership alone does not promise that the parent's visible bounds
        // are the union, intersection, or any other derived read of its members.
        packet["context"]!["parts"]![5]!.AsObject().Remove("bounds");
        var open = NativeRecord.Restore(NativeRecord.Capture("open-parent-bounds", Import(packet)));
        Assert.Null(open.Context.Parts[5].Bounds);
        Assert.Same(open.Context.Parts[0].Witness, Assert.Single(open.Context.Parts[5].Parts));
        Assert.Empty(open.Context.Relations[3].Comparisons);
    }

    [Fact]
    public void ObjectAssembliesRejectMissingForwardAndRepeatedMembers()
    {
        foreach (var references in new[] { new[] { "missing" }, new[] { "assembly" }, new[] { "wall", "wall" } })
        {
            var packet = ObjectFixture();
            packet["context"]!["parts"]![5]!["participants"] = new JsonArray(references.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
            Assert.Throws<InvalidDataException>(() => Import(packet));
        }
    }

    [Fact]
    public void ObjectOpinionsSelectActualCandidatesAcrossRoundTripAndWithoutLabels()
    {
        var packet = ObjectOpinionFixture(); var root = Import(packet);
        var fields = Entries(root); var contextFields = Entries(fields[2]); var opinionFields = Entries(fields[3]);
        contextFields[5] = EncodedTextSurface.Create(new Carrier(), "{}");
        opinionFields[3] = EncodedTextSurface.Create(new Carrier(), "{}");
        fields[2] = G(contextFields); fields[3] = G(opinionFields);
        var read = NativeRecord.Restore(NativeRecord.Capture("connected-object-opinions", G(fields)));
        var claims = read.Opinions!.Claims;
        Assert.Same(read.Context.Parts[0].Witness, Assert.Single(claims[0].ContextParts));
        Assert.Equal([read.Context.Parts[5].Witness, read.Context.Parts[0].Witness], claims[1].ContextParts);
        Assert.Same(claims[0].Witness, Assert.Single(claims[1].Dependencies));
        Assert.Same(read.Context.Parts[0].Profiles[0], claims[0].Profiles[0]);
        Assert.Same(read.Opinions.Bundle.Ladder.Medium, Assert.Single(read.Opinions.Bundle.Read(read.Opinions.Bundle.Hypotheses)).Support);
        Assert.Equal("{}", read.Opinions.AdapterJson);

        // Legacy two-group opinion selections remain readable with no object link.
        var legacy = NativeRecord.Restore(NativeRecord.Capture("legacy-opinions", Import(OpinionFixture())));
        Assert.All(legacy.Opinions!.Claims, claim => Assert.Empty(claim.ContextParts));
    }

    [Fact]
    public void ObjectOpinionsRejectAbsentRepeatedAndForeignCandidateIdentity()
    {
        foreach (var references in new[] { new[] { "missing" }, new[] { "wall", "wall" } })
        {
            var packet = ObjectOpinionFixture();
            packet["opinions"]!["claims"]![0]!["contextParts"] = new JsonArray(references.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
            Assert.Throws<InvalidDataException>(() => Import(packet));
        }
        var root = Import(ObjectOpinionFixture()); Assert.True(NativeRecord.TryRead(root, out var read));
        var other = NativeRecord.Restore(NativeRecord.Capture("same-shape-different-identity", root));
        var fields = Entries(root);
        Assert.False(OpinionEvidence.TryRead(Assert.IsType<Relation>(fields[3]), Assert.IsType<Relation>(fields[1]), read.Annotation, out _, other.Context));
        Assert.False(OpinionEvidence.TryRead(Assert.IsType<Relation>(fields[3]), Assert.IsType<Relation>(fields[1]), read.Annotation, out _));
        Assert.True(OpinionEvidence.TryRead(Assert.IsType<Relation>(fields[3]), Assert.IsType<Relation>(fields[1]), read.Annotation, out _, read.Context));
    }

    private static long[] PositionTicks(Relation site) =>
        [Assert.IsType<CarrierLocation>(Assert.IsType<Relation>(site.Right).Left).Ticks,
         Assert.IsType<CarrierLocation>(Assert.IsType<Relation>(site.Right).Right).Ticks];

    private static long[] Margins(ContextEvidence.Candidate candidate)
    {
        Assert.True(ReadWitness.TryRead(Assert.Single(candidate.Comparisons), out _, out _, out var result, out _, out _));
        return Entries(result).Cast<CarrierLocation>().Select(location => location.Ticks).ToArray();
    }

    private static JsonObject ObjectOpinionFixture()
    {
        var packet = ObjectFixture(); packet["opinions"] = OpinionFixture()["opinions"]!.DeepClone();
        packet["opinions"]!["claims"]![0]!["contextParts"] = new JsonArray("wall");
        packet["opinions"]!["claims"]![1]!["contextParts"] = new JsonArray("building", "wall");
        return packet;
    }

    private static JsonObject ObjectFixture()
    {
        var packet = Fixture();
        // These are authored overlapping interpretations of a small acquisition,
        // not a test claiming that the source actually depicts a church.
        packet["context"] = JsonNode.Parse("""
            {"sourceId":"fixture","parts":[
              {"id":"wall","spans":["s0","s1"],"bounds":[0,0,10,20],"geometry":{"points":[[0,0],[10,0],[10,20],[0,20]],"segments":[[0,1],[1,2],[2,3],[3,0]]}},
              {"id":"roof","spans":["s0"],"bounds":[0,-6,10,0],"geometry":{"points":[[0,0],[5,-6],[10,0]],"segments":[[0,1],[1,2],[2,0]]}},
              {"id":"window-a","spans":["s0"],"bounds":[2,4,4,8],"geometry":{"points":[[2,4],[4,4],[4,8],[2,8]],"segments":[[0,1],[1,2],[2,3],[3,0]]}},
              {"id":"window-b","spans":["s1"],"bounds":[6,4,8,8],"geometry":{"points":[[6,4],[8,4],[8,8],[6,8]],"segments":[[0,1],[1,2],[2,3],[3,0]]}},
              {"id":"opening-alternative","spans":["s0"],"bounds":[2,4,4,8],"geometry":{"points":[[2,4],[4,4],[4,8],[2,8]],"segments":[[0,1],[1,2],[2,3]]}},
              {"id":"building","spans":["s0","s1"],"participants":["wall"],"bounds":[0,0,10,20]},
              {"id":"assembly","participants":["building","roof","window-a","window-b"],"bounds":[0,-6,10,20]}
            ],"relations":[
              {"id":"contains-a","participants":["wall","window-a"]},
              {"id":"contains-b","participants":["wall","window-b"]},
              {"id":"repeated-openings","participants":["window-a","window-b"]},
              {"id":"same-region","participants":["wall","building"]}
            ],"assumptions":[],"views":[]}
            """);
        return packet;
    }
}
