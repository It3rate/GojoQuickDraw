using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Gojo.Core;
using Gojo.Encoding.Structures;
using Gojo.Experiments.QuickDraw;
using GojoQuickDraw.Learning;

namespace GojoQuickDraw.ChurchStudy;

public static class Program
{
    public static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    public static int Main(string[] args)
    {
        try
        {
            if(args.Length!=3||args[0]!="run")throw new ArgumentException("Usage: run INPUT.ndjson OUTPUT_FOLDER");
            Directory.CreateDirectory(args[2]);var count=0;
            foreach(var line in File.ReadLines(args[1],Encoding.UTF8))
            {
                using var request=JsonDocument.Parse(line);var r=request.RootElement;var id=r.GetProperty("id").GetString()!;
                if(id.Length!=40||id.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("Use a SHA256-derived forty-character drawing identity.");
                var folder=Path.Combine(args[2],id);Directory.CreateDirectory(folder);
                try
                {
                    var packet=r.GetProperty("packet");
                    foreach(var key in new[]{"cleanup","constructionProgram","boundaryMapping","layout"})if(packet.TryGetProperty(key,out _))throw new InvalidDataException("Final or category interpretations cannot be imported as source features.");
                    if(packet.GetProperty("source").GetProperty("recognized").ValueKind!=JsonValueKind.True)throw new InvalidDataException("This cohort requires an explicitly game-recognized acquisition.");
                    var importWatch=Stopwatch.StartNew();
                    var annotation=QuickDrawAnnotationEvidence.Import(packet);
                    if(!QuickDrawAnnotationEvidence.TryRead(annotation,out var read))throw new InvalidDataException("Source fitted annotation did not restore.");
                    var original=NeighborhoodGraph.FromAnnotation(read);
                    var modelBytes=File.ReadAllBytes(r.GetProperty("modelPath").GetString()!);using var model=JsonDocument.Parse(modelBytes);
                    NeighborhoodFeatures.ValidateBundle(model.RootElement);var hash=Convert.ToHexString(SHA256.HashData(modelBytes)).ToLowerInvariant();
                    var ids=packet.GetProperty("observation").GetProperty("spans").EnumerateArray().Select(p=>p.GetProperty("id").GetString()!).ToArray();
                    var imported=read.Profiles.ToArray();
                    if(ids.Length!=imported.Length)throw new InvalidDataException("Imported profile count differs from the supplied fitted source.");
                    var profiles=ids.Select((id,i)=>(id,source:(IElement)imported[i].Witness)).ToDictionary(p=>p.id,p=>p.source);
                    var importMs=importWatch.Elapsed.TotalMilliseconds;
                    var archiveMode=r.TryGetProperty("archiveMode",out var selectedMode)?selectedMode.GetString()!:"history";
                    var results=new List<object>();
                    foreach(var arm in r.GetProperty("arms").EnumerateArray().Select(v=>v.GetString()!))
                    {
                        var path=Path.Combine(folder,arm+".gojo.bin.gz");
                        var armView=WriteArm(annotation,original,r,profiles,model.RootElement,hash,arm,path+".tmp");
                        // A long history must not keep its live proof graph while
                        // the independent restore allocates a second copy.
                        if(r.GetProperty("passes").GetInt32()>16)
                            GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking:true,compacting:false);
                        var restoreWatch=Stopwatch.StartNew();RestoreArchive(path+".tmp",archiveMode);
                        armView.TimingMs.Restore=restoreWatch.Elapsed.TotalMilliseconds;
                        File.Move(path+".tmp",path,true);results.Add(armView.View);
                        Console.Error.WriteLine($"{id}: {arm} independently restored");
                    }
                    var view=new{id,keyId=packet.GetProperty("source").GetProperty("key_id").GetString(),
                        source=packet.GetProperty("observation").GetProperty("strokes").EnumerateArray().Select(s=>s.GetProperty("points").Deserialize<double[][]>()!).ToArray(),
                        sourceSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(packet.GetProperty("sourceOriginalJson").GetString()!))).ToLowerInvariant(),
                        modelSha256=hash,sourceProfiles=profiles.Count,unrepresentedProfiles=profiles.Count-original.Edges.Length,importMs,results};
                    var json=JsonSerializer.Serialize(view,Json);File.WriteAllText(Path.Combine(folder,"view.json.tmp"),json,Encoding.UTF8);File.Move(Path.Combine(folder,"view.json.tmp"),Path.Combine(folder,"view.json"),true);
                    Console.WriteLine(JsonSerializer.Serialize(new{id,ok=true,drawings=++count},Json));
                }
                catch(Exception ex)
                {
                    File.WriteAllText(Path.Combine(folder,"error.txt"),ex.ToString());Console.WriteLine(JsonSerializer.Serialize(new{id,ok=false,error=ex.Message},Json));
                }
            }
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (object View,ChurchExperiment.Timings TimingMs) WriteArm(Relation annotation,NeighborhoodGraph.Graph original,JsonElement request,
        IReadOnlyDictionary<string,IElement> profiles,JsonElement model,string hash,string arm,string path)
    {
        var mode=request.TryGetProperty("archiveMode",out var selectedMode)?selectedMode.GetString()!:"history";
        var result=ChurchExperiment.Run(annotation,original,request.GetProperty("scaffold"),request.GetProperty("mapping"),profiles,
            model,hash,arm,request.GetProperty("passes").GetInt32(),request.GetProperty("steps").GetInt32(),request.GetProperty("candidates").GetInt32(),
            request.TryGetProperty("heuristics",out var hypotheses)?hypotheses:null,mode);
        var watch=Stopwatch.StartNew();var bytes=GojoNumericFormat.Encode(result.Surface);
        using(var output=new GZipStream(File.Create(path),mode=="current"?CompressionLevel.Fastest:CompressionLevel.Optimal))output.Write(bytes);
        result.TimingMs.Encode=watch.Elapsed.TotalMilliseconds;result.Archive.NativeBytes=bytes.Length;result.Archive.StoredBytes=new FileInfo(path).Length;
        return (result.View,result.TimingMs);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void RestoreArchive(string path,string mode)
    {
        using var stored=new GZipStream(File.OpenRead(path),CompressionMode.Decompress);
        using var bytes=new MemoryStream();stored.CopyTo(bytes);
        var restored=GojoNumericFormat.Decode(bytes.ToArray());
        if(restored.Length!=1||restored[0] is not Relation root)throw new InvalidDataException("Connected church archive did not restore.");
        if(mode=="current")ChurchCheckpoint.Validate(root);else ChurchExperiment.Validate(root);
    }
}
