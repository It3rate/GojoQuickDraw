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
var currentCheckpoints=0;var currentEdges=0;
var recoveryWitnesses=0;var recoveryIntervals=0;
foreach(var file in Directory.EnumerateFiles(Path.Combine(args[0],"drawings"),"view.json",SearchOption.AllDirectories).Order().Take(limit))
{
    using var view=JsonDocument.Parse(File.ReadAllText(file));
    foreach(var result in view.RootElement.GetProperty("results").EnumerateArray())
    {
        var arm=result.GetProperty("arm").GetString()!;
        using var stream=new GZipStream(File.OpenRead(Path.Combine(Path.GetDirectoryName(file)!,arm+".gojo.bin.gz")),CompressionMode.Decompress);
        using var bytes=new MemoryStream();stream.CopyTo(bytes);
        var restored=(Relation)GojoNumericFormat.Decode(bytes.ToArray())[0];
        if(F(restored).Length==11)
        {
            AuditCurrent(restored,view.RootElement,result);currentCheckpoints++;worlds++;continue;
        }
        var root=BoundaryImport.Unwrap(restored,out var boundaryImport);ChurchExperiment.Validate(root);
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
                if(part.Length==3)
                {
                    var host=NegotiatingMachine.Restore((Relation)part[0]);
                    var bounds=stages[index].GetProperty("parts")[i].GetProperty("bounds").Deserialize<double[]>()!;
                    foreach(var (port,k) in F(part[2]).Select((p,k)=>(p,k)))
                        Check((ChurchScene.Read(F(host.Publish((Relation)port))[2])-ChurchScene.Offset)/ChurchScene.Scale,bounds[k]);
                }
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
            foreach(var import in F(F(world[6])[1]).Cast<Relation>())
            {
                if(!ReadWitness.TryRead(import,out var beforeRecovery,out _,out var recovered,out _,out _)||
                    beforeRecovery is not Relation beforeRelation||!beforeRelation.TryReadOrderedGroup(out var beforeFields)||
                    beforeFields.Length!=2||beforeFields[0] is not Relation firstRelation||!firstRelation.TryReadOrderedGroup(out var firstFields)||
                    firstFields.Length!=7||!ReferenceEquals(firstFields[0],original.Surface))continue;
                foreach(var interval in F(beforeFields[1]))
                {
                    var pair=F(interval);var source=original.Edges.Single(e=>ReferenceEquals(e.Source,pair[0]));
                    source.Span.TryReadSegment(out var actualAxis,out _,out _);
                    if(pair[1] is not Relation span||!span.TryReadSegment(out var axis,out var a,out var b)||
                        !ReferenceEquals(axis,actualAxis)||a.Ticks<0||b.Ticks>PatchEvidence.Q||a.Ticks>=b.Ticks)
                        throw new InvalidDataException("Recovery lost its actual unexplained source interval");
                    recoveryIntervals++;
                }
                if(F(recovered).Any(g=>!F(world[3]).Any(p=>ReferenceEquals(F(p)[1],g))))
                    throw new InvalidDataException("Recovery witness does not select its live target constructions");
                foreach(var p in F(firstFields[3]))MachineRunner.Run((Relation)F(p)[1]);
                recoveryWitnesses++;
            }
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
            AuditReceipts(machines);
        }
        worlds++;
    }
}
var report=new{worlds,currentCheckpoints,currentEdges,shapePoints=points,errorReads=reads,actuatorOutcomes=outcomes,recoveryWitnesses,recoveryIntervals,maxDifference,
    qualification="Read-only comparison of restored native graphs, executable shape outputs and error intervals against review projections. Retained receiver accounting is checked exactly, including latest responses in live checkpoints. Omitted earlier provenance and receipts cannot be audited. No accuracy judgment."};
File.WriteAllText(args[2],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(report));
void AuditCurrent(Relation root,JsonElement drawing,JsonElement result)
{
    ChurchCheckpoint.Validate(root);var state=ChurchCheckpoint.Restore(root);
    if(!result.TryGetProperty("archiveMode",out var mode)||mode.GetString()!="current")throw new InvalidDataException("Review does not identify its qualified current checkpoint.");
    if(drawing.TryGetProperty("sourceProfiles",out var count)&&count.GetInt32()!=state.SourceProfiles.Length)
        throw new InvalidDataException("Current checkpoint lost imported source profiles.");
    var final=result.GetProperty("stages").EnumerateArray().Last();var graph=final.GetProperty("graph");
    var expectedSites=graph.GetProperty("sites").EnumerateArray().ToArray();var expectedEdges=graph.GetProperty("edges").EnumerateArray().ToArray();
    if(expectedSites.Length!=state.Current.Sites.Length||expectedEdges.Length!=state.Current.Edges.Length)
        throw new InvalidDataException("Current graph cardinality changed during restoration.");
    var siteIds=new Dictionary<Relation,string>(ReferenceEqualityComparer.Instance);
    foreach(var (site,i) in state.Current.Sites.Select((s,i)=>(s,i)))
    {
        var expected=expectedSites[i];var xy=XY(site);for(var k=0;k<2;k++)Check(xy[k],expected.GetProperty("point")[k].GetDouble());
        siteIds.Add(site,expected.GetProperty("id").GetString()!);
        if(Degree(state.Current,site)!=expected.GetProperty("degree").GetInt32()||Kind(state.Current,site)!=expected.GetProperty("kind").GetString())
            throw new InvalidDataException("Current joint topology changed during restoration.");
    }
    foreach(var (edge,i) in state.Current.Edges.Select((e,i)=>(e,i)))
    {
        var expected=expectedEdges[i];var controls=expected.GetProperty("controls").Deserialize<double[][]>()!;
        if(controls.Length!=edge.Points.Length||siteIds[edge.A]!=expected.GetProperty("start").GetString()||siteIds[edge.B]!=expected.GetProperty("end").GetString())
            throw new InvalidDataException("Current edge lost its ordered endpoint binding.");
        for(var n=0;n<controls.Length;n++)for(var k=0;k<2;k++)Check(edge.Points[n][k],controls[n][k]);
        edge.Span.TryReadSegment(out _,out var a,out var b);Check(a.Ticks/(double)PatchEvidence.Q,expected.GetProperty("interval")[0].GetDouble());Check(b.Ticks/(double)PatchEvidence.Q,expected.GetProperty("interval")[1].GetDouble());
        var inferred=state.UnobservedSources.Any(s=>ReferenceEquals(s,edge.Source));
        if(inferred!=expected.GetProperty("inferred").GetBoolean())throw new InvalidDataException("An unobserved passage changed into acquired ink.");
        if(!inferred&&expected.GetProperty("source").GetString()!="p"+Array.FindIndex(state.Source.Edges,e=>ReferenceEquals(e.Source,edge.Source)))
            throw new InvalidDataException("Current source interval changed its native source identity.");
        currentEdges++;
    }
    var samples=state.Machines.SelectMany(m=>F(m.CurrentPublication).Cast<Relation>()).ToDictionary(p=>(Relation)F(p)[0],p=>p,(IEqualityComparer<Relation>)ReferenceEqualityComparer.Instance);
    AuditReceipts(state.Machines);
    var parts=final.GetProperty("parts");
    if(parts.GetArrayLength()!=state.Parts.Length+state.Derived.Length)throw new InvalidDataException("Current shape count differs from the saved review.");
    foreach(var (part,i) in state.Parts.Select((p,i)=>(F(p),i)))
    {
        foreach(var (port,k) in F(part[1]).Select((p,k)=>(p,k)))Check((ChurchScene.Read(F(samples[(Relation)port])[2])-ChurchScene.Offset)/ChurchScene.Scale,parts[i].GetProperty("bounds")[k].GetDouble());
        Geometry((Relation)part[0],parts[i]);
    }
    foreach(var (geometry,i) in state.Derived.Select((g,i)=>(g,i)))Geometry(geometry,parts[state.Parts.Length+i]);
    var errors=final.GetProperty("errors");if(errors.GetArrayLength()!=state.Obligations.Length)throw new InvalidDataException("Current error law count differs from the saved review.");
    foreach(var (error,i) in state.Obligations.Select((e,i)=>(e,i)))
    {
        var read=error.Assess(samples);var resolved=EvidenceObligation.Resolved(read);
        if(resolved!=errors[i].GetProperty("resolved").GetBoolean())throw new InvalidDataException("Current error changed its unresolved status.");
        var norm=Math.Sqrt(error.Terms.Sum(t=>{var map=(Relation)F(t)[1];((Relation)map.Right).TryReadSegment(out _,out var a,out var b);return Math.Pow(b.Ticks-a.Ticks,2);}));
        var demand=resolved?ChurchScene.Read(EvidenceObligation.Demand(read))/Math.Max(1,norm)/ChurchScene.Scale:0;
        Check(demand,errors[i].GetProperty("demand").GetDouble());reads++;
    }
    void Geometry(Relation geometry,JsonElement expectedPart)
    {
        var output=F(F(MachineRunner.Run(geometry))[2]);var expected=expectedPart.GetProperty("points").Deserialize<double[][]>()!;
        if(output.Length!=expected.Length)throw new InvalidDataException("Current native geometry changed point count.");
        for(var n=0;n<output.Length;n++)
        {
            var xy=Gojo.Experiments.ConstructionMachines.Program.XY(output[n]);for(var k=0;k<2;k++)Check((xy[k]-ChurchScene.Offset)/ChurchScene.Scale,expected[n][k]);points++;
        }
    }
}
void AuditReceipts(IEnumerable<NegotiatingMachine> machines)
{
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
void Check(double actual,double expected)
{
    var diff=Math.Abs(actual-expected);maxDifference=Math.Max(maxDifference,diff);
    if(diff>1e-8||!double.IsFinite(actual))throw new InvalidDataException("Restored native output differs from saved review.");
}
