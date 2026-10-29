using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Gojo.Core;
using Gojo.Experiments.ConstructionMachines;
using GojoQuickDraw.ChurchStudy;
using static Gojo.Experiments.ConstructionMachines.MachineExpressions;
using static GojoQuickDraw.Learning.NeighborhoodGraph;

if(args.Length!=2)throw new ArgumentException("Usage: INPUT.json OUTPUT_FOLDER");
using var input=JsonDocument.Parse(File.ReadAllText(args[0]));var r=input.RootElement;
using var graphJson=JsonDocument.Parse("""{"sites":[[0,400],[500,400]],"edges":[{"a":0,"b":1}]}""");
var source=FromJson(graphJson.RootElement);var original=GojoNumericFormat.Encode(source.Surface);
object Read(ChurchScene s,int step)=>new{step,parts=s.Parts.Select(p=>new{p.Id,bounds=s.Bounds(p)}).ToArray(),errors=ChurchSettlement.Errors(s)};
using var empty=JsonDocument.Parse("""{"rules":[]}""");
var baseline=new ChurchScene(source,source,r.GetProperty("scaffold"),true,true,hypotheses:empty.RootElement);
var scene=new ChurchScene(source,source,r.GetProperty("scaffold"),true,true,hypotheses:r.GetProperty("plan"));
var snapshots=new List<object>{Read(scene,0)};var memory=new Dictionary<Relation,IElement>(ReferenceEqualityComparer.Instance);
var rounds=new List<IElement>();var timer=Stopwatch.StartNew();
for(var i=1;i<=16;i++)
{
    rounds.Add(ChurchSettlement.Step(scene,memory).Surface);snapshots.Add(Read(scene,i));
    ChurchSettlement.Step(baseline);
}
timer.Stop();
if(!original.SequenceEqual(GojoNumericFormat.Encode(source.Surface)))throw new InvalidDataException("Control changed its source");
var native=GojoNumericFormat.Encode(G(scene.Root,G(rounds.ToArray())));
var restored=F((Relation)GojoNumericFormat.Decode(native)[0]);var world=F(restored[0]);
var rows=F(world[3]);var maxDifference=0d;
for(var i=0;i<rows.Length;i++)
{
    var machine=NegotiatingMachine.Restore((Relation)F(rows[i])[0]);
    var ports=new[]{machine.Port(0,0),machine.Port(0,1),machine.Port(1,0),machine.Port(3,1)};
    var actual=ports.Select(p=>(ChurchScene.Read(F(machine.Publish(p))[2])-ChurchScene.Offset)/ChurchScene.Scale).ToArray();
    maxDifference=Math.Max(maxDifference,actual.Zip(scene.Bounds(scene.Parts[i]),(a,b)=>Math.Abs(a-b)).Max());
}
if(maxDifference!=0)throw new InvalidDataException("Restored native frames differ from the reported control");
Directory.CreateDirectory(args[1]);
using(var zip=new GZipStream(File.Create(Path.Combine(args[1],"world.gojo.bin.gz")),CompressionLevel.Optimal))zip.Write(native);
var report=new{schema="gojo.cad-native-control.v1",snapshots,baseline=Read(baseline,16),elapsedIncludingSnapshotsAndBaselineMs=timer.Elapsed.TotalMilliseconds,
    sourceUnchanged=true,restoredMaxDifference=maxDifference,nativeBytes=native.Length,
    qualification="Authored native frame control, not QuickDraw or a learned target. One persistent connected scene is reused for 16 rounds. Time includes snapshots, baseline steps and native witnesses; it does not isolate arithmetic. Source remains unchanged."};
File.WriteAllText(Path.Combine(args[1],"result.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions(GojoQuickDraw.ChurchStudy.Program.Json){WriteIndented=true}));
