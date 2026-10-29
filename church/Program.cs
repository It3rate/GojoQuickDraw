using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
                    var annotation=QuickDrawAnnotationEvidence.Import(packet);
                    if(!QuickDrawAnnotationEvidence.TryRead(annotation,out var read))throw new InvalidDataException("Source fitted annotation did not restore.");
                    var original=NeighborhoodGraph.FromAnnotation(read);
                    var modelBytes=File.ReadAllBytes(r.GetProperty("modelPath").GetString()!);using var model=JsonDocument.Parse(modelBytes);
                    NeighborhoodFeatures.ValidateBundle(model.RootElement);var hash=Convert.ToHexString(SHA256.HashData(modelBytes)).ToLowerInvariant();
                    var ids=packet.GetProperty("observation").GetProperty("spans").EnumerateArray().Select(p=>p.GetProperty("id").GetString()!).ToArray();
                    var imported=read.Profiles.ToArray();
                    if(ids.Length!=imported.Length)throw new InvalidDataException("Imported profile count differs from the supplied fitted source.");
                    var profiles=ids.Select((id,i)=>(id,source:(IElement)imported[i].Witness)).ToDictionary(p=>p.id,p=>p.source);
                    var results=new List<object>();
                    foreach(var arm in r.GetProperty("arms").EnumerateArray().Select(v=>v.GetString()!))
                    {
                        var result=ChurchExperiment.Run(annotation,original,r.GetProperty("scaffold"),r.GetProperty("mapping"),profiles,
                            model.RootElement,hash,arm,r.GetProperty("passes").GetInt32(),r.GetProperty("steps").GetInt32(),r.GetProperty("candidates").GetInt32());
                        var path=Path.Combine(folder,arm+".gojo.bin.gz");
                        using(var output=new GZipStream(File.Create(path+".tmp"),CompressionLevel.Optimal))output.Write(GojoNumericFormat.Encode(result.Surface));
                        using(var stored=new GZipStream(File.OpenRead(path+".tmp"),CompressionMode.Decompress))
                        {
                            using var bytes=new MemoryStream();stored.CopyTo(bytes);
                            var restored=GojoNumericFormat.Decode(bytes.ToArray());
                            if(restored.Length!=1||restored[0] is not Relation root)throw new InvalidDataException("Connected church archive did not restore.");
                            ChurchExperiment.Validate(root);
                        }
                        File.Move(path+".tmp",path,true);results.Add(result.View);
                        Console.Error.WriteLine($"{id}: {arm} independently restored");
                    }
                    var view=new{id,keyId=packet.GetProperty("source").GetProperty("key_id").GetString(),
                        source=packet.GetProperty("observation").GetProperty("strokes").EnumerateArray().Select(s=>s.GetProperty("points").Deserialize<double[][]>()!).ToArray(),
                        sourceSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(packet.GetProperty("sourceOriginalJson").GetString()!))).ToLowerInvariant(),
                        modelSha256=hash,sourceProfiles=profiles.Count,unrepresentedProfiles=profiles.Count-original.Edges.Length,results};
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
}
