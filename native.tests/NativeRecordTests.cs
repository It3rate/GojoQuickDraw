using System.Text.Json;
using System.Text.Json.Nodes;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Structures;
using GojoQuickDraw.Native;

namespace GojoQuickDraw.Native.Tests;

public sealed class NativeRecordTests
{
    [Fact]
    public void StandaloneRestorePreservesActualSourceAliasesAndOrderedSamples()
    {
        var packet = Fixture(); var root = Import(packet);
        var restored = NativeRecord.Restore(NativeRecord.Capture("fixture", root));
        Assert.Equal(5, restored.Acquisition.Samples.Count);
        Assert.Equal([3, 2, 1, 2, 1], restored.Acquisition.Samples.Select(sample => (int)sample.TimeNumerator!).ToArray());
        var profiles = restored.Annotation!.Profiles;
        Assert.Equal(2, profiles.Length);
        foreach (var profile in profiles)
        {
            Assert.True(ReadWitness.TryRead(profile.Witness, out var selection, out _, out _, out var residue, out _));
            Assert.Same(profile.Source.Selection, selection); Assert.Same(selection, residue);
            var samples = Entries(profile.Source.Selection.Right).Cast<Relation>().ToArray();
            Assert.Same(samples[0].Left, profile.Curve.From); Assert.Same(samples[^1].Left, profile.Curve.To);
        }
        Assert.Equal(profiles[0].Curve.Controls[^1], profiles[1].Curve.Controls[0]);
        Assert.NotSame(profiles[0].Curve.To, profiles[1].Curve.From);
        Assert.Equal(4, restored.Annotation.Interpretation.Construction.Rungs.Length);
    }

    [Fact]
    public void OriginalSourceLexemesSurviveWithoutSidecars()
    {
        var packet = Fixture();
        const string source = "{ \"key_id\":\"fixture\", \"drawing\":[[[0,5,1e1],[0,2,0],[3,2,1]],[[10,10],[0,20],[2,1]]] }";
        packet["sourceOriginalJson"] = source;
        var restored = NativeRecord.Restore(NativeRecord.Capture("fixture", Import(packet)));
        Assert.Equal(source, restored.SourceOriginalJson);
        Assert.Equal("1e1", restored.Source.Strokes[0].Samples[2].X.Lexeme);
    }

    [Fact]
    public void EmptyStrokeInventsNeitherProfilesNorZeroSamples()
    {
        var packet = JsonNode.Parse("{\"itemKey\":\"empty\",\"source\":{\"key_id\":\"empty\",\"drawing\":[[[],[]], [[],[]]]}}")!.AsObject();
        var read = NativeRecord.Restore(NativeRecord.Capture("empty", Import(packet)));
        Assert.Null(read.Annotation); Assert.Equal(2, read.Acquisition.StrokeCount); Assert.Empty(read.Acquisition.Samples);
        Assert.Empty(read.Context.Parts);
    }

    [Fact]
    public void ContextUsesActualProfileWitnessesButFreshOpenScaffoldSites()
    {
        var packet = Fixture(context: true); var restored = NativeRecord.Restore(NativeRecord.Capture("fixture", Import(packet)));
        var context = restored.Context;
        Assert.Equal(2, context.Parts.Length);
        Assert.Same(restored.Annotation!.Profiles[0].Witness, context.Parts[0].Profiles[0]);
        var first = context.Parts[0]; Assert.Single(first.OpenSlots); Assert.Equal(4, first.Points.Length);
        var segment = first.Segments[0]; Assert.Same(first.Points[0], segment.Left); Assert.Same(first.Points[1], segment.Right);
        Assert.DoesNotContain(first.Points[0].Left, restored.Acquisition.Samples.Select(sample => (IElement)sample.Event));
        var comparison = Assert.Single(context.Relations[0].Comparisons);
        Assert.True(ReadWitness.TryRead(comparison, out var selected, out _, out var measured, out _, out _));
        Assert.Same(first.Bounds, Assert.IsType<Relation>(selected).Left);
        Assert.Equal([2000, 3000, 6000, 12000], Entries(measured).Cast<CarrierLocation>().Select(location => location.Ticks).ToArray());
    }

    [Fact]
    public void ScaffoldCannotStealAnAcquiredIdentity()
    {
        var root = Import(Fixture(context: true)); Assert.True(NativeRecord.TryRead(root, out var read));
        var outer = Entries(root); var context = Entries(outer[2]); var candidates = Entries(context[1]);
        var candidate = Assert.IsType<Relation>(candidates[0]);
        Assert.True(ReadWitness.TryRead(candidate, out var selected, out var basis, out var result, out var residue, out var history));
        var body = Entries(result); var scaffold = Entries(body[0]); var points = Entries(scaffold[0]);
        points[0] = new Relation(read.Acquisition.Samples[0].Event, Assert.IsType<Relation>(points[0]).Right);
        scaffold[0] = G(points); scaffold[1] = G(); body[0] = G(scaffold);
        candidates[0] = ReadWitness.Create(new Carrier(), selected, basis, G(body), residue, history);
        context[1] = G(candidates); outer[2] = G(context);
        Assert.False(NativeRecord.TryRead(G(outer), out _));
    }

    [Fact]
    public void ContextCannotPointToAnAbsentProfileOrFuturePart()
    {
        var missing = Fixture(context: true); missing["context"]!["parts"]![0]!["spans"]![0] = "missing";
        Assert.Throws<InvalidDataException>(() => Import(missing));
        var forward = Fixture(context: true); forward["context"]!["parts"]![0]!["participants"] = new JsonArray("window");
        Assert.Throws<InvalidDataException>(() => Import(forward));
    }

    [Fact]
    public void CorruptArchiveReferenceNeverBecomesPartialSuccess()
    {
        var json = JsonNode.Parse(NativeRecord.Capture("fixture", Import(Fixture())))!.AsObject();
        var nodes = json["nodes"]!.AsArray();
        var relation = nodes.First(node => node!["kind"]!.GetValue<string>() == "relation")!;
        relation["left"] = "absent-reference";
        Assert.Throws<InvalidDataException>(() => NativeRecord.Restore(json.ToJsonString()));
    }

    [Fact]
    public void PerturbingClearanceWithoutUpdatingWitnessInvalidatesRead()
    {
        var root = Import(Fixture(context: true)); Assert.True(NativeRecord.TryRead(root, out var read));
        var comparison = read.Context.Relations[0].Comparisons[0];
        Assert.True(ReadWitness.TryRead(comparison, out _, out _, out var measured, out _, out _));
        // Explicit structural retargeting changes the published measured amount;
        // it does not move the compared boundary and therefore must fail replay.
        var location = Assert.IsType<CarrierLocation>(Entries(measured)[0]); location.Tick.Move(1);
        Assert.False(NativeRecord.TryRead(root, out _));
    }

    [Fact]
    public void WorkerContinuesAfterMalformedRecordAndRejectsUnsafePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gojo-quickdraw-native-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var bad = Fixture(); bad["itemKey"] = "../escape";
            var input = new StringReader("{broken\n" + bad.ToJsonString() + "\n" + Fixture().ToJsonString() + "\n");
            var output = new StringWriter(); var errors = new StringWriter();
            Assert.Equal(0, Worker.Run(["encode", "--output", directory], input, output, errors));
            var rows = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonNode.Parse(line)!).ToArray();
            Assert.Equal(3, rows.Length); Assert.False(rows[0]["ok"]!.GetValue<bool>()); Assert.False(rows[1]["ok"]!.GetValue<bool>()); Assert.True(rows[2]["ok"]!.GetValue<bool>());
            Assert.Single(Directory.GetFiles(directory));
            var inspect = new StringWriter();
            Assert.Equal(0, Worker.Run(["inspect", "--input", Path.Combine(directory, "fixture.gojo.json.gz"), "--source"], TextReader.Null, inspect, errors));
            Assert.NotNull(JsonNode.Parse(inspect.ToString())!["sourceOriginalJson"]);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void OpinionTransportConnectsProfilesExpectedTopologyAndEarlierClaims()
    {
        var packet = OpinionFixture();
        // Type/provider names describe the adapter and do not supply geometry.
        foreach (var row in packet["opinions"]!["claims"]!.AsArray()) { row!.AsObject().Remove("kind"); row.AsObject().Remove("provider"); }
        var restored = NativeRecord.Restore(NativeRecord.Capture("opinions", Import(packet)));
        var opinions = Assert.IsType<OpinionEvidence.Read>(restored.Opinions);
        Assert.Equal(2, opinions.Claims.Length);
        var first = opinions.Claims[0]; var second = opinions.Claims[1];
        Assert.Same(restored.Annotation!.Profiles[0].Witness, first.Profiles[0]);
        Assert.Same(first.Witness, Assert.Single(second.Dependencies));
        Assert.Same(first.Edges[0].Left, first.Edges[1].Left);
        Assert.DoesNotContain(first.Sites[0].Left, restored.Acquisition.Samples.Select(s => (IElement)s.Event));
        var hypothesis = Assert.Single(opinions.Bundle.Read(opinions.Bundle.Hypotheses));
        Assert.Same(opinions.Bundle.Ladder.Medium, hypothesis.Support);
        Assert.Same(opinions.Bundle.Ladder.Weak, hypothesis.Opposition);
        Assert.Single(opinions.Bundle.Read(opinions.Bundle.Defaults));
        Assert.NotSame(first.Sites[0], second.Sites[0]);
    }

    [Fact]
    public void OpinionsRejectForwardDependenciesDuplicateSupportAndUnknownGrades()
    {
        var forward = OpinionFixture(); forward["opinions"]!["claims"]![0]!["dependencies"] = new JsonArray("second");
        Assert.Throws<InvalidDataException>(() => Import(forward));
        var missing = OpinionFixture(); missing["opinions"]!["claims"]![0]!["spans"]![0] = "absent";
        Assert.Throws<InvalidDataException>(() => Import(missing));
        var duplicate = OpinionFixture(); duplicate["opinions"]!["claims"]![0]!["spans"] = new JsonArray("s0", "s0");
        Assert.Throws<InvalidDataException>(() => Import(duplicate));
        var grade = OpinionFixture(); grade["opinions"]!["claims"]![0]!["support"] = "certain";
        Assert.Throws<InvalidDataException>(() => Import(grade));
    }

    [Fact]
    public void OpinionExpectedEdgeMustConsumeItsActualDeclaredSites()
    {
        var bad = OpinionFixture(); bad["opinions"]!["claims"]![0]!["geometry"]!["segments"]![0] = new JsonArray(0, 12);
        Assert.Throws<InvalidDataException>(() => Import(bad));
        var unchanged = Import(OpinionFixture()); Assert.True(NativeRecord.TryRead(unchanged, out var read));
        var root = Entries(unchanged); var opinion = Entries(root[3]); var claims = Entries(opinion[2]);
        Assert.True(ReadWitness.TryRead(Assert.IsType<Relation>(claims[0]), out var selected, out var basis, out var result, out var residue, out var history));
        var expected = Entries(result); var sites = Entries(expected[0]);
        sites[0] = new Relation(read.Acquisition.Samples[0].Event, Assert.IsType<Relation>(sites[0]).Right);
        expected[0] = G(sites); expected[1] = G();
        claims[0] = ReadWitness.Create(new Carrier(), selected, basis, G(expected), residue, history);
        opinion[2] = G(claims); root[3] = G(opinion);
        Assert.False(NativeRecord.TryRead(G(root), out _));
    }

    [Fact]
    public void CornerExpectationCanConsumeActualEarlierShapeSitesWithoutCoordinateAliasing()
    {
        var packet = OpinionFixture();
        packet["opinions"]!["claims"]![1]!["geometry"]!["pointSources"] = JsonNode.Parse("""[{"claim":"first","index":0},{"claim":"first","index":2}]""");
        var read = NativeRecord.Restore(NativeRecord.Capture("shared-expectations", Import(packet)));
        Assert.Same(read.Opinions!.Claims[0].Sites[0], read.Opinions.Claims[1].Sites[0]);
        Assert.Same(read.Opinions.Claims[0].Sites[2], read.Opinions.Claims[1].Sites[1]);
        packet["opinions"]!["claims"]![1]!["geometry"]!["pointSources"]![0]!["index"] = 1;
        Assert.Throws<InvalidDataException>(() => Import(packet));
    }

    private static JsonObject OpinionFixture()
    {
        var packet = Fixture();
        packet["opinions"] = JsonNode.Parse("""
            {"sourceId":"fixture","claims":[
              {"id":"first","kind":"corner","provider":"fixture","context":"hypotheses","spans":["s0","s1"],"dependencies":[],
               "geometry":{"points":[[10,0],[0,0],[10,20]],"segments":[[0,1],[0,2]]},
               "comparisons":[{"actual":0,"expected":0,"allowance":4}],"support":"medium","opposition":"weak"},
              {"id":"second","kind":"container-carry","provider":"fixture","context":"defaults","spans":["s1"],"dependencies":["first"],
               "geometry":{"points":[[10,0],[10,20]],"segments":[[0,1]]},"comparisons":[],"support":"weak","opposition":"none"}
            ]}
            """);
        return packet;
    }

    private static Relation Import(JsonObject packet)
    { using var document = JsonDocument.Parse(packet.ToJsonString()); return NativeRecord.Import(document.RootElement); }
    private static IElement[] Entries(IElement element)
    { Assert.True(Assert.IsType<Relation>(element).TryReadOrderedGroup(out var fields)); return fields; }
    private static Relation G(params IElement[] elements) => Relation.OrderedGroup(new Carrier(), elements);

    private static JsonObject Fixture(bool context = false)
    {
        double[][][] profiles = [[[0, 0], [5, 2], [10, 0]], [[10, 0], [10, 20]]];
        object Coordinate(double value) => new { origin = "envelope-a", from = "envelope-a", to = "envelope-b", numerator = (long)Math.Round(value * 1000), denominator = 512000 };
        var sites = new List<object> { new { id = "envelope-a", at = 0, x = 0, y = 0 }, new { id = "envelope-b", at = 0, x = 512, y = 512 } };
        var edges = new List<object> { new { id = "rough", at = 0, controls = new[] { "envelope-a", "envelope-b" } } };
        var rungs = new List<object> {
            new { id = "rough", visible = new[] { "rough" }, guides = Array.Empty<string>(), selected = Array.Empty<string>(), introduced = new[] { "rough" } },
            new { id = "outline", visible = new[] { "rough" }, guides = Array.Empty<string>(), selected = Array.Empty<string>(), introduced = Array.Empty<string>() }
        };
        for (var i = 0; i < profiles.Length; i++)
        {
            sites.Add(new { id = $"a{i}", at = i + 2, x = Coordinate(profiles[i][0][0]), y = Coordinate(profiles[i][0][1]) });
            sites.Add(new { id = $"b{i}", at = i + 2, x = Coordinate(profiles[i][^1][0]), y = Coordinate(profiles[i][^1][1]) });
            edges.Add(new { id = $"edge{i}", at = i + 2, controls = new[] { $"a{i}", $"b{i}" } });
            rungs.Add(new { id = $"stroke{i}", visible = Enumerable.Range(0, i + 1).Select(k => $"edge{k}").ToArray(), guides = new[] { "rough" }, selected = i == 0 ? new[] { "rough" } : [], introduced = new[] { $"edge{i}" } });
        }
        var packet = new {
            itemKey = "fixture",
            source = new { key_id = "fixture", drawing = profiles.Select(p => new[] { p.Select(v => v[0]).ToArray(), p.Select(v => v[1]).ToArray(), p.Select((_, i) => (double)(p.Length - i)).ToArray() }).ToArray() },
            observation = new { sourceId = "fixture", bounds = new[] { 0, 0, 10, 20 }, spans = profiles.Select((p, i) => new { id = $"s{i}", strokeIndex = i, startIndex = 0, endIndex = p.Length - 1, controls = p }).ToArray() },
            program = new { id = "fixture", family = "fixture", viewBox = new[] { 0, 0, 512, 512 }, parameters = Array.Empty<object>(), sites, edges, rungs,
                constraints = Array.Empty<object>(), repeats = Array.Empty<object>(), circular = Array.Empty<object>(),
                sourceMapping = new { sourceId = "fixture", envelopeSiteIds = new[] { "envelope-a", "envelope-b" }, spanMap = Enumerable.Range(0, profiles.Length).ToDictionary(i => $"s{i}", i => $"edge{i}") } },
            annotations = new { policy = new { angleDegrees = 10, proximity = 4, linearity = 4, reflectionDistance = 6, repetitionDistance = 5, minSpanLength = 12, coincidentDegrees = 1 },
                lines = profiles.Select((_, i) => new { spans = new[] { $"s{i}" } }).ToArray(), joints = Array.Empty<object>(), symmetries = Array.Empty<object>(), relations = Array.Empty<object>() }
        };
        var result = JsonSerializer.SerializeToNode(packet)!.AsObject();
        if (context) result["context"] = JsonNode.Parse("""
            {"sourceId":"fixture","parts":[
              {"id":"wall","kind":"main-body","spans":["s0","s1"],"bounds":[0,0,10,20],"geometry":{"points":[[0,0],[10,0],[10,20],[0,20]],"segments":[[0,1],[1,2],[2,3],[3,0]]}},
              {"id":"window","kind":"window","spans":["s1"],"bounds":[2,3,4,8]}
            ],"relations":[{"id":"contains","kind":"containment","participants":["wall","window"],"spans":[]}],
            "assumptions":[{"id":"physical-scale","kind":"scale","openSlots":["metres-per-page-unit"]}],
            "views":[{"id":"front","kind":"front","spans":["s0","s1"]}]}
            """);
        return result;
    }
}
