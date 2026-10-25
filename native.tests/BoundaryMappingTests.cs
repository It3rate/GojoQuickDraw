using System.Text.Json.Nodes;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Structures;
using GojoQuickDraw.Native;

namespace GojoQuickDraw.Native.Tests;

public sealed partial class NativeRecordTests
{
    [Fact]
    public void PartialSharedInkEndpointsAndPriorOnlyBoundarySurviveWithoutLabels()
    {
        var fields = Entries(Import(MappingFixture()));
        var context = Entries(fields[2]); context[5] = EncodedTextSurface.Create(new Carrier(), "{}"); fields[2] = G(context);
        var opinions = Entries(fields[3]); opinions[3] = EncodedTextSurface.Create(new Carrier(), "{}"); fields[3] = G(opinions);
        var mapping = Entries(fields[4]); mapping[4] = EncodedTextSurface.Create(new Carrier(), "{}"); fields[4] = G(mapping);
        var read = NativeRecord.Restore(NativeRecord.Capture("boundary-correspondence", G(fields)));
        var map = Assert.IsType<BoundaryMappingEvidence.Read>(read.BoundaryMapping);
        Assert.Equal(3, map.Targets.Length);
        var wall = map.Targets[0]; var roof = map.Targets[1]; var missing = map.Targets[2];
        Assert.Same(read.Context.Parts[0].Witness, wall.Part);
        Assert.Same(read.Context.Parts[0].Segments[0], Assert.Single(wall.Edges));
        var ink = Assert.Single(wall.Matches);
        Assert.Same(read.Annotation!.Profiles[0].Witness, ink.Source.Profile);
        Assert.Same(ink.Source.Profile, Assert.Single(roof.Matches).Source.Profile);
        Assert.Equal((2000L, 8000L), (ink.Source.From, ink.Source.To));
        Assert.Equal((9000L, 1000L), (roof.Matches[0].TargetFrom, roof.Matches[0].TargetTo));
        Assert.Empty(missing.Matches);
        Assert.Same(read.Annotation.Profiles[1].Curve.To, Assert.Single(missing.Endpoints).Site);
        Assert.Equal([(0L, 10000L)], missing.Remainder);
        Assert.Equal([(0L, 2000L), (8000L, 10000L), (0L, 10000L)], map.Unresolved.Select(r => (r.From, r.To)).ToArray());
        Assert.Empty(map.Unprofiled); Assert.Equal("{}", map.AdapterJson);
        Assert.True(ReadWitness.TryRead(wall.Witness, out var selected, out _, out _, out var residue, out _));
        Assert.Same(selected, residue); // Non-draining observation, never two copies of consumed ink.
    }

    [Theory]
    [InlineData("missing-profile")]
    [InlineData("foreign-part")]
    [InlineData("absent-edge")]
    [InlineData("non-endpoint")]
    [InlineData("reversed-source")]
    [InlineData("uncovered-source")]
    [InlineData("false-unresolved")]
    [InlineData("uncovered-target")]
    [InlineData("duplicate-support")]
    [InlineData("false-unprofiled")]
    public void InvalidCorrespondenceCannotRestoreAsCompleteEvidence(string damage)
    {
        var p = MappingFixture(); var map = p["boundaryMapping"]!; var target = map["targets"]![0]!;
        switch (damage)
        {
            case "missing-profile": target["ink"]![0]!["span"] = "absent"; break;
            case "foreign-part": target["part"] = "absent"; break;
            case "absent-edge": target["segments"]![0] = 99; break;
            case "non-endpoint": map["targets"]![2]!["endpointSupport"]![0]!["at"] = 2; break;
            case "reversed-source": target["ink"]![0]!["from"] = .9; break;
            case "uncovered-source": map["sourceCoverage"]!.AsArray().RemoveAt(0); break;
            case "false-unresolved": map["sourceCoverage"]![0]!["to"] = .3; break;
            case "uncovered-target": target["targetRemainder"]!.AsArray().RemoveAt(0); break;
            case "duplicate-support": target["ink"]!.AsArray().Add(target["ink"]![0]!.DeepClone()); break;
            case "false-unprofiled": map["unprofiledSamples"]!.AsArray().Add(new JsonObject { ["stroke"] = 0, ["sample"] = 1 }); break;
        }
        Assert.Throws<InvalidDataException>(() => Import(p));
    }

    [Fact]
    public void EqualCoordinateEndpointFromAnotherAcquisitionIsNotSupport()
    {
        var root = Import(MappingFixture()); Assert.True(NativeRecord.TryRead(root, out var read));
        var other = NativeRecord.Restore(NativeRecord.Capture("other", root));
        var fields = Entries(root); var map = Entries(fields[4]); var targets = Entries(map[1]);
        var witness = Assert.IsType<Relation>(targets[2]);
        Assert.True(ReadWitness.TryRead(witness, out var selected, out var basis, out var result, out var residue, out var provenance));
        var selection = Entries(selected);
        selection[1] = G(new Relation(read.Annotation!.Profiles[1].Witness, other.Annotation!.Profiles[1].Curve.To));
        var replacement = G(selection);
        targets[2] = ReadWitness.Create(new Carrier(), replacement, basis, result, replacement, provenance);
        map[1] = G(targets); fields[4] = G(map);
        Assert.False(NativeRecord.TryRead(G(fields), out _));
    }

    [Fact]
    public void EqualScaffoldSegmentCopyCannotReplaceTheActualSelectedEdge()
    {
        var root = Import(MappingFixture()); var fields = Entries(root); var map = Entries(fields[4]); var targets = Entries(map[1]);
        Assert.True(ReadWitness.TryRead(Assert.IsType<Relation>(targets[0]), out var selected, out var basis, out var result, out var residue, out var provenance));
        var body = Entries(result); var choice = Assert.IsType<Relation>(body[0]); var edges = Entries(choice.Right); var edge = Assert.IsType<Relation>(edges[0]);
        edges[0] = new Relation(edge.Left, edge.Right); Assert.True(edge.Equals(edges[0]));
        body[0] = new Relation(choice.Left, G(edges));
        targets[0] = ReadWitness.Create(new Carrier(), selected, basis, G(body), residue, provenance);
        map[1] = G(targets); fields[4] = G(map);
        Assert.False(NativeRecord.TryRead(G(fields), out _));
    }

    private static JsonObject MappingFixture()
    {
        var packet = ObjectFixture(); packet["opinions"] = JsonNode.Parse("""{"sourceId":"fixture","claims":[]}""");
        packet["boundaryMapping"] = JsonNode.Parse("""
        {"sourceId":"fixture","targets":[
          {"part":"wall","segments":[0],"relatedParts":["wall"],"ink":[
            {"span":"s0","from":0.2,"to":0.8,"targetFrom":0.1,"targetTo":0.9}],
            "endpointSupport":[],"targetRemainder":[[0,0.1],[0.9,1]]},
          {"part":"roof","segments":[2],"relatedParts":["roof","wall"],"ink":[
            {"span":"s0","from":0.4,"to":0.6,"targetFrom":0.9,"targetTo":0.1}],
            "endpointSupport":[],"targetRemainder":[[0,0.1],[0.9,1]]},
          {"part":"wall","segments":[2],"relatedParts":["wall"],"ink":[],
            "endpointSupport":[{"span":"s1","at":1}],"targetRemainder":[[0,1]]}
        ],"sourceCoverage":[
          {"span":"s0","from":0,"to":0.2,"status":"unresolved"},
          {"span":"s0","from":0.8,"to":1,"status":"unresolved"},
          {"span":"s1","from":0,"to":1,"status":"unresolved"}
        ],"unprofiledSamples":[]}
        """);
        return packet;
    }
}
