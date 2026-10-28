using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using GojoQuickDraw.Native;

namespace GojoQuickDraw.Learning;

public static class Program
{
    public static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    public static int Main(string[] args)
    {
        try
        {
            if(args.Length!=3)throw new ArgumentException("Usage: export INPUT.ndjson OUTPUT_DIR | profile PACKET.json OUTPUT.json | inspect ARCHIVE.gz OUTPUT.json");
            if(args[0]=="neighborhood-export")
            {
                Directory.CreateDirectory(args[2]);var count=0;
                foreach(var line in File.ReadLines(args[1],Encoding.UTF8))
                {
                    using var request=JsonDocument.Parse(line);Gojo.Core.Relation root;object result;
                    var closureBatch=request.RootElement.TryGetProperty("closureCases",out var closureCases);
                    if(closureBatch)
                    {
                        var batch=NeighborhoodClosure.Training(closureCases);root=batch.Surface;
                        result=new{archive=$"batch-{count:D5}.gojo.json.gz",lines=Array.Empty<NeighborhoodLearning.Example>(),repairs=Array.Empty<NeighborhoodLearning.Example>(),
                            closures=batch.Examples,goals=0,unsupported=0,ambiguous=0};
                    }
                    else if(request.RootElement.TryGetProperty("packet",out _))
                    {
                        var batch=NeighborhoodWeak.Build(request.RootElement);root=batch.Surface;
                        result=new{archive=$"batch-{count:D5}.gojo.json.gz",lines=Array.Empty<NeighborhoodLearning.Example>(),repairs=batch.Repairs,goals=batch.Goals,unsupported=batch.Unsupported,ambiguous=batch.Ambiguous};
                    }
                    else
                    {
                        var batch=NeighborhoodLearning.Build(request.RootElement);root=batch.Surface;
                        result=new{archive=$"batch-{count:D5}.gojo.json.gz",lines=batch.Lines,repairs=batch.Repairs,goals=0,unsupported=0,ambiguous=0};
                    }
                    var path=Path.Combine(args[2],$"batch-{count:D5}.gojo.json.gz");
                    using(var output=new GZipStream(File.Create(path),CompressionLevel.Fastest))output.Write(Encoding.UTF8.GetBytes(PatchEvidence.Capture("neighborhood-training-"+count,root)));
                    using var stored=new StreamReader(new GZipStream(File.OpenRead(path),CompressionMode.Decompress));
                    var doc=Gojo.Encoding.Structures.GojoStructureCompactJson.Deserialize(stored.ReadToEnd());
                    if(!Gojo.Encoding.Structures.GojoStructureCompactJson.TryToElement(doc,out var graph)||graph.Root is not Gojo.Core.Relation restored)throw new InvalidDataException("Neighborhood examples did not restore.");
                    if(closureBatch)NeighborhoodClosure.ValidateTraining(restored);else NeighborhoodLearning.Validate(restored);
                    var published=JsonSerializer.SerializeToNode(result,Json)!;
                    published["lineNames"]=JsonSerializer.SerializeToNode(NeighborhoodFeatures.LineNames);
                    published["repairNames"]=JsonSerializer.SerializeToNode(NeighborhoodFeatures.RepairNames);
                    published["closureNames"]=JsonSerializer.SerializeToNode(NeighborhoodClosure.Names);
                    Console.WriteLine(published.ToJsonString());
                    count++;if(count<3||count%10==0)Console.Error.WriteLine($"Neighborhood examples: {count} batches independently restored");
                }
            }
            else if(args[0]=="neighborhood-run")
            {
                Directory.CreateDirectory(args[2]);var count=0;
                foreach(var line in File.ReadLines(args[1],Encoding.UTF8))
                {
                    using var request=JsonDocument.Parse(line);var r=request.RootElement;
                    var modelBytes=File.ReadAllBytes(r.GetProperty("modelPath").GetString()!);using var model=JsonDocument.Parse(modelBytes);
                    NeighborhoodFeatures.ValidateBundle(model.RootElement);
                    var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(modelBytes)).ToLowerInvariant();
                    Gojo.Core.IElement observation;NeighborhoodGraph.Graph initial;
                    if(r.TryGetProperty("packet",out var packet))
                    {
                        var source=Gojo.Experiments.QuickDraw.QuickDrawAnnotationEvidence.Import(packet);
                        if(!Gojo.Experiments.QuickDraw.QuickDrawAnnotationEvidence.TryRead(source,out var read))throw new InvalidDataException("Unreadable source annotation.");
                        observation=source;initial=NeighborhoodGraph.FromAnnotation(read);
                    }
                    else {initial=NeighborhoodGraph.FromJson(r.GetProperty("source"));observation=initial.Surface;}
                    var result=NeighborhoodRun.Execute(observation,initial,model.RootElement,hash,r.GetProperty("useRepair").GetBoolean(),r.GetProperty("useLine").GetBoolean(),
                        r.GetProperty("passes").GetInt32(),r.GetProperty("maxCandidates").GetInt32(),!r.TryGetProperty("useClosure",out var useClosure)||useClosure.GetBoolean());
                    var path=Path.Combine(args[2],$"drawing-{count:D5}.gojo.json.gz");
                    using(var output=new GZipStream(File.Create(path),CompressionLevel.Fastest))output.Write(Encoding.UTF8.GetBytes(PatchEvidence.Capture("neighborhood-result-"+count,result.Surface)));
                    using var stored=new StreamReader(new GZipStream(File.OpenRead(path),CompressionMode.Decompress));
                    var doc=Gojo.Encoding.Structures.GojoStructureCompactJson.Deserialize(stored.ReadToEnd());
                    if(!Gojo.Encoding.Structures.GojoStructureCompactJson.TryToElement(doc,out var graph)||graph.Root is not Gojo.Core.Relation restored)throw new InvalidDataException("Neighborhood result did not restore.");
                    NeighborhoodRun.Validate(restored);
                    var readback=JsonSerializer.SerializeToNode(result.Readback,Json)!;
                    var stages=JsonSerializer.SerializeToNode(NeighborhoodRun.Stages(restored),Json)!.AsArray();
                    readback["initial"]=stages[0]!["graph"]!.DeepClone();readback["final"]=stages[^1]!["graph"]!.DeepClone();readback["stages"]=stages;
                    if(r.TryGetProperty("target",out var targetJson))
                    {
                        var target=NeighborhoodGraph.FromJson(targetJson);var fields=NeighborhoodRun.RootFields(restored);
                        readback["evaluation"]=JsonSerializer.SerializeToNode(new{before=NeighborhoodLearning.TargetCost(NeighborhoodGraph.Read((Gojo.Core.Relation)fields[1]),target),
                            after=NeighborhoodLearning.TargetCost(NeighborhoodGraph.Read((Gojo.Core.Relation)fields[3]),target)},Json);
                    }
                    Console.WriteLine(readback.ToJsonString());
                    count++;if(count<3||count%10==0)Console.Error.WriteLine($"Neighborhood results: {count} drawings independently restored");
                }
            }
            else if(args[0]=="templates")File.WriteAllText(args[2],JsonSerializer.Serialize(PatchEvidence.Views(),Json));
            else if(args[0]=="export")
            {
                Directory.CreateDirectory(args[2]);var count=0;
                foreach(var line in File.ReadLines(args[1],Encoding.UTF8))
                {
                    if(string.IsNullOrWhiteSpace(line))continue;
                    using var document=JsonDocument.Parse(line);var key="patches-"+(count++).ToString("D5");var path=Path.Combine(args[2],key+".gojo.json.gz");
                    var (root,rows)=PatchEvidence.Build(document.RootElement,Path.GetFileName(path));
                    var capture=PatchEvidence.Capture(key,root);
                    using(var stream=new GZipStream(File.Create(path),CompressionLevel.Fastest))stream.Write(Encoding.UTF8.GetBytes(capture));
                    using(var stream=new StreamReader(new GZipStream(File.OpenRead(path),CompressionMode.Decompress)))
                    {
                        var read=PatchEvidence.Restore(stream.ReadToEnd());var vectors=PatchEvidence.Vectors(read);
                        if(vectors.Length!=rows.Count||vectors.Where((v,i)=>!v.SequenceEqual(rows[i].Bands)).Any())throw new InvalidDataException("Stored patches changed their published cells.");
                    }
                    foreach(var row in rows)Console.WriteLine(JsonSerializer.Serialize(row,Json));
                    if(count<3||count%10==0)Console.Error.WriteLine($"Native patches: {count} source batches restored ({rows.Count} patches in this batch)");
                }
            }
            else if(args[0]=="encode")
            {
                Directory.CreateDirectory(args[2]);var count=0;
                foreach(var line in File.ReadLines(args[1],Encoding.UTF8))
                {
                    using var request=JsonDocument.Parse(line);var inputPath=request.RootElement.GetProperty("sourceArchive").GetString()!;
                    using var input=new StreamReader(new GZipStream(File.OpenRead(inputPath),CompressionMode.Decompress));
                    var source=PatchEvidence.Restore(input.ReadToEnd());var root=EncodingEvidence.Build(source,request.RootElement);
                    var text=PatchEvidence.Capture("paired-encoding-"+count,root);var path=Path.Combine(args[2],Path.GetFileName(inputPath));
                    using(var gzip=new GZipStream(File.Create(path),CompressionLevel.Fastest))gzip.Write(Encoding.UTF8.GetBytes(text));
                    using var stored=new StreamReader(new GZipStream(File.OpenRead(path),CompressionMode.Decompress));
                    var document=Gojo.Encoding.Structures.GojoStructureCompactJson.Deserialize(stored.ReadToEnd());
                    if(!Gojo.Encoding.Structures.GojoStructureCompactJson.TryToElement(document,out var graph)||graph.Root is not Gojo.Core.Relation restored)
                        throw new InvalidDataException("Paired native encoding did not independently restore.");
                    Console.WriteLine(JsonSerializer.Serialize(EncodingEvidence.Read(restored),Json));
                    count++;if(count<3||count%10==0)Console.Error.WriteLine($"Paired encoding: {count} drawings independently restored");
                }
            }
            else if(args[0]=="opinions")
            {
                Directory.CreateDirectory(args[2]);var count=0;
                foreach(var line in File.ReadLines(args[1],Encoding.UTF8))
                {
                    using var request=JsonDocument.Parse(line);var inputPath=request.RootElement.GetProperty("sourceArchive").GetString()!;
                    using var input=new StreamReader(new GZipStream(File.OpenRead(inputPath),CompressionMode.Decompress));
                    var source=PatchEvidence.Restore(input.ReadToEnd());var root=PatchEvidence.Scores(source,request.RootElement);
                    var text=PatchEvidence.Capture("model-opinions-"+count,root);
                    var path=Path.Combine(args[2],Path.GetFileName(inputPath));
                    using(var gzip=new GZipStream(File.Create(path),CompressionLevel.Fastest))gzip.Write(Encoding.UTF8.GetBytes(text));
                    using var stored=new StreamReader(new GZipStream(File.OpenRead(path),CompressionMode.Decompress));
                    var doc=Gojo.Encoding.Structures.GojoStructureCompactJson.Deserialize(stored.ReadToEnd());
                    if(!Gojo.Encoding.Structures.GojoStructureCompactJson.TryToElement(doc,out var graph)||graph.Root is not Gojo.Core.Relation restored||PatchEvidence.ValidateScores(restored)!=PatchEvidence.Validate(source))throw new InvalidDataException("Learned opinion did not restore.");
                    count++;
                    if(count<3||count%10==0)Console.Error.WriteLine($"Native model opinions: {count} batches restored");
                }
                Console.WriteLine(JsonSerializer.Serialize(new{nativeOpinionBatches=count,independentRestoresVerified=true},Json));
            }
            else if(args[0]=="inspect")
            {
                using var stream=new StreamReader(new GZipStream(File.OpenRead(args[1]),CompressionMode.Decompress));
                var root=PatchEvidence.Restore(stream.ReadToEnd());File.WriteAllText(args[2],JsonSerializer.Serialize(new{nativeRestoreVerified=true,patches=PatchEvidence.Validate(root)},Json));
            }
            else if(args[0]=="profile")
            {
                using var document=JsonDocument.Parse(File.ReadAllText(args[1]));var time=Stopwatch.StartNew();var timings=new Dictionary<string,double>();
                T Measure<T>(string name,Func<T> run){var before=time.Elapsed.TotalSeconds;var result=run();timings[name]=time.Elapsed.TotalSeconds-before;return result;}
                var root=Measure("import",()=>NativeRecord.Import(document.RootElement));
                var text=Measure("capture",()=>NativeRecord.Capture("profile",root));
                var read=Measure("firstRestore",()=>NativeRecord.Restore(text));
                var bytes=Measure("compress",()=>{using var output=new MemoryStream();using(var gzip=new GZipStream(output,CompressionLevel.SmallestSize,leaveOpen:true))gzip.Write(Encoding.UTF8.GetBytes(text));return output.ToArray();});
                _=Measure("storedRestore",()=>{using var input=new StreamReader(new GZipStream(new MemoryStream(bytes),CompressionMode.Decompress));return NativeRecord.Restore(input.ReadToEnd());});
                _=Measure("jointReadback",()=>JointReadback.Read(read));
                File.WriteAllText(args[2],JsonSerializer.Serialize(new{seconds=timings,totalSeconds=time.Elapsed.TotalSeconds,uncompressedBytes=Encoding.UTF8.GetByteCount(text),compressedBytes=bytes.Length,summary=NativeRecord.Summarize(read)},Json));
            }
            else throw new ArgumentException("Unknown learning command.");
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error.ToString());return 1;}
    }
}
