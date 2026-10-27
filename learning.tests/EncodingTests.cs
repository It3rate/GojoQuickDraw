using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Structures;
using GojoQuickDraw.Learning;

namespace GojoQuickDraw.Learning.Tests;

public sealed class EncodingTests
{
    private static Relation Source(bool rotate=false)
    {
        double[][] a=[[-100,0],[100,0]],b=[[0,-100],[0,4]];
        if(rotate){a=a.Select(p=>new[]{-p[1],p[0]}).ToArray();b=b.Select(p=>new[]{-p[1],p[0]}).ToArray();}
        using var input=JsonDocument.Parse(JsonSerializer.Serialize(new{sourceKey="control",patches=new[]{new{id="p",group="g",labelBasis="unlabelled-source-patch",runs=new[]{a,b}}}}));
        return PatchEvidence.Build(input.RootElement,"unused").Root;
    }
    private static Relation Restore(Relation root)
    {
        var document=GojoStructureCompactJson.Deserialize(PatchEvidence.Capture("paired",root));
        Assert.True(GojoStructureCompactJson.TryToElement(document,out var graph));return (Relation)graph.Root;
    }
    private static JsonDocument Read(Relation root)=>JsonDocument.Parse(JsonSerializer.Serialize(EncodingEvidence.Read(root),Program.Json));
    private static JsonDocument Request()=>JsonDocument.Parse("""
        {"enabled":true,"modelSha256":"exact-external-model-bytes","predictions":[{"scores":[0.03,0.02,0.10,0.75,0.10],"proposed":3}]}
        """);

    [Fact]
    public void UncheckedProviderRetainsTheExactSameBaselineSurfaceAfterIndependentRestore()
    {
        var source=Source();using var request=JsonDocument.Parse("{\"enabled\":false}");
        var root=Restore(EncodingEvidence.Build(source,request.RootElement));root.TryReadOrderedGroup(out var fields);
        ReadWitness.TryRead((Relation)fields[1],out var baselineSource,out _,out var baseline,out _,out _);
        ReadWitness.TryRead((Relation)fields[2],out var selectedSource,out var basis,out var selected,out _,out var history);
        Assert.Same(baselineSource,selectedSource);Assert.Same(baseline,selected);Assert.Same(fields[1],basis);Assert.Same(basis,history);
        using var read=Read(root);Assert.False(read.RootElement.GetProperty("enabled").GetBoolean());
        Assert.Equal(read.RootElement.GetProperty("baseline").GetRawText(),read.RootElement.GetProperty("selected").GetRawText());
    }
    [Fact]
    public void AllAlternativesAndTheirSourceOrientationsSurviveWithoutMutatingSourceFeatures()
    {
        var source=Source();var before=PatchEvidence.Vectors(source);using var request=Request();
        var root=Restore(EncodingEvidence.Build(source,request.RootElement));using var read=Read(root);
        var patch=Assert.Single(read.RootElement.GetProperty("patches").EnumerateArray());
        Assert.Equal(5,patch.GetProperty("candidates").GetArrayLength());Assert.Equal(3,patch.GetProperty("proposed").GetInt32());
        var choices=patch.GetProperty("candidates");Assert.Equal(.75,choices[3].GetProperty("score").GetDouble());
        var directions=choices[3].GetProperty("directions").EnumerateArray().Select(v=>Math.Round(v.GetDouble())).Order().ToArray();
        Assert.Equal(new double[]{0,180,270},directions);
        Assert.Equal(before[0],PatchEvidence.Vectors(source)[0]);
        root.TryReadOrderedGroup(out var fields);Assert.NotSame(source,fields[0]);
    }
    [Fact]
    public void RotatedSourceChangesPlacementButNotCandidateScoresOrLearnedType()
    {
        using var request=Request();using var straight=Read(Restore(EncodingEvidence.Build(Source(),request.RootElement)));
        using var rotated=Read(Restore(EncodingEvidence.Build(Source(true),request.RootElement)));
        var a=straight.RootElement.GetProperty("patches")[0];var b=rotated.RootElement.GetProperty("patches")[0];
        Assert.Equal(a.GetProperty("ranking").GetRawText(),b.GetProperty("ranking").GetRawText());
        var directions=b.GetProperty("candidates")[3].GetProperty("directions").EnumerateArray().Select(v=>Math.Round(v.GetDouble())).Order().ToArray();
        Assert.Equal(new double[]{0,90,270},directions);
    }
    [Fact]
    public void AnUnresolvedProviderRetainsAllFiveCandidatesInsteadOfClaimingAContact()
    {
        using var request=JsonDocument.Parse("""{"enabled":true,"modelSha256":"m","predictions":[{"scores":[0.2,0.2,0.2,0.2,0.2],"proposed":null}]}""");
        using var read=Read(Restore(EncodingEvidence.Build(Source(),request.RootElement)));
        var patch=read.RootElement.GetProperty("patches")[0];Assert.Equal(JsonValueKind.Null,patch.GetProperty("proposed").ValueKind);
        Assert.Equal(5,patch.GetProperty("candidates").GetArrayLength());
        Assert.False(read.RootElement.GetProperty("selected")[0].GetProperty("shared").GetBoolean());
        Assert.False(read.RootElement.GetProperty("selected")[0].GetProperty("added").GetBoolean());
    }
    [Fact]
    public void DisconnectedProviderOrBaselineLookalikesCannotReadAsTheSelectedSource()
    {
        var source=Source();using var request=Request();var root=EncodingEvidence.Build(source,request.RootElement);
        root.TryReadOrderedGroup(out var fields);
        var other=EncodingEvidence.Build(Source(),request.RootElement);other.TryReadOrderedGroup(out var foreign);
        Assert.Throws<InvalidDataException>(()=>EncodingEvidence.Read(PatchEvidence.G(fields[0],fields[1],fields[2],foreign[3])));
        Assert.Throws<InvalidDataException>(()=>EncodingEvidence.Read(PatchEvidence.G(fields[0],foreign[1],fields[2],fields[3])));
    }
}
