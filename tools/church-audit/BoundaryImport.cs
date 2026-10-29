using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gojo.Core;
using Gojo.Encoding;
using Gojo.Encoding.Structures;
using Gojo.Structures;
using GojoQuickDraw.ChurchStudy;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;

/// <summary>A separate, lossless structural import. It adds declared scaffold
/// boundary relations between the actual output roles of retained native point
/// generators. It neither changes a numeric world nor asserts observed ink.</summary>
public static class BoundaryImport
{
    public static Relation Unwrap(Relation root,out Relation? import)
    {
        var f=F(root);import=null;
        if(f.Length==2&&f[0] is Relation before&&f[1] is Relation witness&&
            ReadWitness.TryRead(witness,out var source,out _,out _,out _,out _)&&ReferenceEquals(source,before))
        {Validate(root);import=witness;return before;}
        return root;
    }

    public static void Validate(Relation wrapper,bool validateWorld=true)
    {
        var f=F(wrapper);if(f.Length!=2||f[0] is not Relation original||f[1] is not Relation import||
            !ReadWitness.TryRead(import,out var source,out _,out var result,out _,out _)||!ReferenceEquals(source,original))
            throw new InvalidDataException("Boundary import lost its original world.");
        if(validateWorld)ChurchExperiment.Validate(original);
        var rounds=F(F(original)[2]);
        foreach(var stage in F(result).Cast<Relation>())
        {
            if(!rounds.Any(r=>ReferenceEquals(r,stage.Left)))throw new InvalidDataException("Topology was imported into a foreign pass.");
            var pass=F(stage.Left);if(pass.Length!=5)throw new InvalidDataException("A local-only pass has no scaffold producer.");
            var parts=F(F(pass[1])[3]);var boundaries=F(stage.Right);
            if(parts.Length!=boundaries.Length)throw new InvalidDataException("Boundary import lost a scaffold.");
            foreach(var (part,i) in parts.Select((p,i)=>(F(p),i)))
            {
                var geometry=(Relation)part[1];var roles=F(F(geometry)[2]);
                if(boundaries[i] is not Relation read||!ReadWitness.TryRead(read,out var generator,out var basis,out var edges,out _,out _)||
                    !ReferenceEquals(generator,geometry)||!ReferenceEquals(basis,F(geometry)[2]))
                    throw new InvalidDataException("Boundary endpoints are not the actual public output roles.");
                foreach(var edge in F(edges).Cast<Relation>())
                    if(!roles.Any(p=>ReferenceEquals(p,edge.Left))||!roles.Any(p=>ReferenceEquals(p,edge.Right)))
                        throw new InvalidDataException("A scaffold edge bypasses its declared producer.");
            }
        }
        if(F(result).Length!=rounds.Count(r=>F(r).Length==5))throw new InvalidDataException("An executed scaffold pass was omitted from the import.");
    }

    public static void Seal(string folder,int maximum=10000,int workers=4)
    {
        if(maximum is <1 or >10000||workers is <1 or >4)throw new ArgumentOutOfRangeException(nameof(maximum));
        var started=System.Diagnostics.Stopwatch.StartNew();
        var directory=Path.GetFullPath(folder);var receipts=new System.Collections.Concurrent.ConcurrentBag<object>();var completed=0;
        var files=Directory.EnumerateFiles(Path.Combine(directory,"drawings"),"view.json",SearchOption.AllDirectories).Order().Take(maximum).ToArray();
        Parallel.ForEach(files,new ParallelOptions{MaxDegreeOfParallelism=workers},file=>
        {
            using var view=JsonDocument.Parse(File.ReadAllText(file));var identity=view.RootElement.GetProperty("id").GetString()!;
            if(identity.Length!=40||identity.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("A drawing identity must name its retained source hash.");
            var input=Path.Combine(directory,"inputs",identity+".json.gz");
            using var inputStream=new GZipStream(File.OpenRead(input),CompressionMode.Decompress);
            using var inputJson=JsonDocument.Parse(inputStream);
            var declared=inputJson.RootElement.GetProperty("scaffold").GetProperty("parts").EnumerateArray().ToDictionary(p=>p.GetProperty("id").GetString()!,p=>p.Clone());
            foreach(var run in view.RootElement.GetProperty("results").EnumerateArray())
            {
                var arm=run.GetProperty("arm").GetString()!;var path=Path.Combine(Path.GetDirectoryName(file)!,arm+".gojo.bin.gz");
                if(!ChurchExperiment.Arms.Contains(arm))throw new InvalidDataException("An archive path must belong to a declared experiment version.");
                var bytes=File.ReadAllBytes(path);var beforeHash=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                Relation root;
                using(var gzip=new GZipStream(new MemoryStream(bytes),CompressionMode.Decompress))
                {using var plain=new MemoryStream();gzip.CopyTo(plain);root=(Relation)GojoNumericFormat.Decode(plain.ToArray())[0];}
                var bare=Unwrap(root,out var already);
                if(already is not null){receipts.Add(new{identity,arm,retained=true,sha256=beforeHash});continue;}
                ChurchExperiment.Validate(bare);var original=bare;
                var preview=run.GetProperty("stages").EnumerateArray().ToArray();var additions=new List<IElement>();var edgeCount=0;
                foreach(var (pass,index) in F(F(original)[2]).Select((s,i)=>(s,i+1)))
                {
                    var fields=F(pass);if(fields.Length!=5)continue;
                    var parts=F(F(fields[1])[3]);var expected=preview[index].GetProperty("parts").EnumerateArray().ToArray();
                    if(parts.Length!=expected.Length)throw new InvalidDataException("Review and native scaffold counts differ.");
                    var imported=new List<IElement>();
                    foreach(var (part,i) in parts.Select((p,i)=>(F(p),i)))
                    {
                        var producer=(Relation)part[1];var outputSurface=(Relation)F(producer)[2];var roles=F(outputSurface);
                        var seed=declared[expected[i].GetProperty("id").GetString()!];var topology=seed.GetProperty("geometry").GetProperty("segments").Deserialize<int[][]>()!;
                        if(seed.GetProperty("geometry").GetProperty("points").GetArrayLength()!=roles.Length||
                            topology.Any(e=>e.Length!=2||e.Any(v=>v<0||v>=roles.Length)))throw new InvalidDataException("Declared scaffold ports do not match their producer.");
                        var cached=expected[i].GetProperty("segments").Deserialize<int[][]>()!;
                        if(!topology.SelectMany(e=>e).SequenceEqual(cached.SelectMany(e=>e))||topology.Length!=cached.Length)
                            throw new InvalidDataException("Review topology differs from its declared source scaffold.");
                        var edges=topology.Select(e=>(IElement)new Relation(roles[e[0]],roles[e[1]])).ToArray();edgeCount+=edges.Length;
                        var provenance=EncodedTextSurface.Create(new Carrier(),"Authored source-scaffold boundary import; not acquired ink. Input SHA256 "+
                            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed.GetRawText()))).ToLowerInvariant());
                        imported.Add(ReadWitness.Create(new Carrier(),producer,outputSurface,G(edges),G(),provenance));
                    }
                    additions.Add(new Relation(pass,G(imported.ToArray())));
                }
                var sourceReceipt=EncodedTextSurface.Create(new Carrier(),"Lossless structural import. Original compressed native SHA256 "+beforeHash);
                var witness=ReadWitness.Create(new Carrier(),original,G(),G(additions.ToArray()),G(),sourceReceipt);
                root=G(original,witness);Validate(root,validateWorld:false);
                var temporary=path+".structure.tmp";
                using(var gzip=new GZipStream(File.Create(temporary),CompressionLevel.Fastest))gzip.Write(GojoNumericFormat.Encode(root));
                using(var gzip=new GZipStream(File.OpenRead(temporary),CompressionMode.Decompress))
                {using var plain=new MemoryStream();gzip.CopyTo(plain);Validate((Relation)GojoNumericFormat.Decode(plain.ToArray())[0],validateWorld:false);}
                File.Move(temporary,path,true);
                receipts.Add(new{identity,arm,retained=false,beforeSha256=beforeHash,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),boundaryEdges=edgeCount});
            }
            var n=Interlocked.Increment(ref completed);if(n%8==0||n==files.Length)Console.WriteLine($"Boundary import: {n}/{files.Length} drawings; geometry unchanged");
        });
        var report=new{drawings=files.Length,worlds=receipts.Count,receipts,elapsedSeconds=started.Elapsed.TotalSeconds,
            encoderAssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(BoundaryImport).Assembly.Location))).ToLowerInvariant(),
            qualification="Additional directed boundary relations bind the actual retained native generator output roles. The original numerical run remains intact inside each wrapper. No simulation result, source sample or model score is changed; this authored structural import did not retroactively influence settlement."};
        File.WriteAllText(Path.Combine(directory,"boundary-import.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
    }
}
