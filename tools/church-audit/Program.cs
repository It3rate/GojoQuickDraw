using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gojo.Core;
using Gojo.Structures;
using Gojo.Experiments.ConstructionMachines;
using Gojo.Experiments.QuickDraw;
using GojoQuickDraw.ChurchStudy;
using GojoQuickDraw.Learning;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

if(args.Length is 2 or 3 or 4&&args[0]=="seal") {BoundaryImport.Seal(args[1],args.Length>=3?int.Parse(args[2]):10000,args.Length==4?int.Parse(args[3]):4);return;}
if(args.Length!=3)throw new ArgumentException("Usage: STUDY_FOLDER MAX_DRAWINGS OUTPUT.json | seal STUDY_FOLDER [MAX_DRAWINGS]");
var limit=int.Parse(args[1]);if(limit is <1 or >1024)throw new ArgumentOutOfRangeException(nameof(limit));
var options=GojoQuickDraw.ChurchStudy.Program.Json;
var worlds=0;var points=0;var reads=0;var outcomes=0;var maxDifference=0d;
foreach(var file in Directory.EnumerateFiles(Path.Combine(args[0],"drawings"),"view.json",SearchOption.AllDirectories).Order().Take(limit))
{
    using var view=JsonDocument.Parse(File.ReadAllText(file));
    foreach(var result in view.RootElement.GetProperty("results").EnumerateArray())
    {
        var arm=result.GetProperty("arm").GetString()!;
        using var stream=new GZipStream(File.OpenRead(Path.Combine(Path.GetDirectoryName(file)!,arm+".gojo.bin.gz")),CompressionMode.Decompress);
        using var bytes=new MemoryStream();stream.CopyTo(bytes);
        var root=BoundaryImport.Unwrap((Relation)GojoNumericFormat.Decode(bytes.ToArray())[0],out var boundaryImport);ChurchExperiment.Validate(root);
        var f=F(root);var original=Read((Relation)f[1]);var final=Read((Relation)f[3]);var stages=result.GetProperty("stages").EnumerateArray().ToArray();
        if(!QuickDrawAnnotationEvidence.TryRead((Relation)f[0],out var acquisition)||
            original.Edges.Any(e=>!acquisition.Profiles.Any(p=>ReferenceEquals(p.Witness,e.Source))))
            throw new InvalidDataException("Restored active profiles no longer refer to the actual native source annotation.");
        if(view.RootElement.TryGetProperty("sourceProfiles",out var profileCount)&&profileCount.GetInt32()!=acquisition.Profiles.Length)
            throw new InvalidDataException("Review source profile count differs from its actual annotation.");
        var actual=JsonSerializer.SerializeToNode(Project(final,original.Edges.Select(e=>e.Source).ToArray()),options);
        if(!JsonNode.DeepEquals(actual,JsonNode.Parse(stages[^1].GetProperty("graph").GetRawText())))throw new InvalidDataException("Saved review graph differs from its restored native final.");
        foreach(var (nativeStage,index) in F(f[2]).Select((s,i)=>(F(s),i+1)))
        {
            if(nativeStage.Length!=5)continue;
            var world=F(nativeStage[1]);var machines=F(world[2]).Cast<Relation>().Select(NegotiatingMachine.Restore).ToArray();
            var samples=new Dictionary<Relation,Relation>(ReferenceEqualityComparer.Instance);
            foreach(var machine in machines)foreach(var sample in F(machine.CurrentPublication).Cast<Relation>())samples[(Relation)F(sample)[0]]=sample;
            foreach(var (part,i) in F(world[3]).Select((p,i)=>(F(p),i)))
            {
                var geometry=MachineRunner.Run((Relation)part[1]);var expected=stages[index].GetProperty("parts")[i].GetProperty("points").Deserialize<double[][]>()!;
                var output=F(F(geometry)[2]);if(expected.Length!=output.Length)throw new InvalidDataException("Shape point count changed during restoration.");
                for(var n=0;n<output.Length;n++)
                {
                    var xy=Gojo.Experiments.ConstructionMachines.Program.XY(output[n]);
                    for(var k=0;k<2;k++)Check((xy[k]-ChurchScene.Offset)/ChurchScene.Scale,expected[n][k]);
                    points++;
                }
            }
            var ordinaryParts=F(world[3]).Length;
            if(stages[index].GetProperty("parts").GetArrayLength()>ordinaryParts)
            {
                // Enhanced studies derive wall cells from captured public
                // bounds. Their native constructions live in the explicit
                // final import read, without independent cell actuators.
                var imports=F(F(world[6])[1]);var derived=F(imports[^1]);
                var geometries=F(derived[1]);
                if(geometries.Length!=stages[index].GetProperty("parts").GetArrayLength()-ordinaryParts)
                    throw new InvalidDataException("Derived wall cells lost their native constructions.");
                foreach(var (geometry,i) in geometries.Select((g,i)=>(g,i)))
                {
                    var output=F(F(MachineRunner.Run((Relation)geometry))[2]);
                    var expected=stages[index].GetProperty("parts")[ordinaryParts+i].GetProperty("points").Deserialize<double[][]>()!;
                    if(output.Length!=expected.Length)throw new InvalidDataException("Derived wall point count changed.");
                    for(var n=0;n<output.Length;n++)
                    {
                        var xy=Gojo.Experiments.ConstructionMachines.Program.XY(output[n]);
                        for(var k=0;k<2;k++)Check((xy[k]-ChurchScene.Offset)/ChurchScene.Scale,expected[n][k]);
                        points++;
                    }
                }
            }
            foreach(var (surface,i) in F(world[4]).Select((s,i)=>(s,i)))
            {
                var error=EvidenceObligation.Restore((Relation)surface);var read=error.Assess(samples);
                if(!EvidenceObligation.Resolved(read))throw new InvalidDataException("A restored error unexpectedly became unresolved.");
                var norm=Math.Sqrt(error.Terms.Sum(t=>
                {
                    var map=(Relation)F(t)[1];((Relation)map.Right).TryReadSegment(out _,out var a,out var b);return Math.Pow(b.Ticks-a.Ticks,2);
                }));
                var demand=ChurchScene.Read(EvidenceObligation.Demand(read))/Math.Max(1,norm)/ChurchScene.Scale;
                Check(demand,stages[index].GetProperty("errors")[i].GetProperty("demand").GetDouble());reads++;
            }
            foreach(var machine in machines)foreach(var receipt in F(F(machine.Surface)[7]).Cast<Relation>())
                foreach(var outcome in NegotiatingMachine.Outcomes(receipt))
                {
                    var total=(N:BigInteger.Zero,D:BigInteger.One);var values=new[]{outcome.Requested,outcome.Accepted,outcome.BoundsBlocked,outcome.CapacityDeferred,outcome.QuantumRemainder,outcome.CandidateBlocked};
                    for(var i=0;i<values.Length;i++)
                    {
                        if(!ProjectionResidue.TryRead(values[i],out _,out var n,out var d))throw new InvalidDataException("Unresolved actuator accounting.");
                        if(i>0)n=-n;total=(total.N*d+n*total.D,total.D*d);
                        var gcd=BigInteger.GreatestCommonDivisor(total.N,total.D);if(gcd!=0)total=(total.N/gcd,total.D/gcd);
                    }
                    if(total.N!=0)throw new InvalidDataException("Restored signed participation did not balance.");outcomes++;
                }
        }
        worlds++;
    }
}
var report=new{worlds,shapePoints=points,errorReads=reads,actuatorOutcomes=outcomes,maxDifference,
    qualification="Read-only comparison of restored native graphs, executable shape outputs, error intervals and exact receiver accounting against review projections. No accuracy judgment."};
File.WriteAllText(args[2],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(report));
void Check(double actual,double expected)
{
    var diff=Math.Abs(actual-expected);maxDifference=Math.Max(maxDifference,diff);
    if(diff>1e-8||!double.IsFinite(actual))throw new InvalidDataException("Restored native output differs from saved review.");
}
