using System.Text.Json;
using GojoQuickDraw.Reads;

namespace GojoQuickDraw.Desktop;

public sealed record EncodingDrawing(string SourceKey,string Category,double[][][] Strokes,JointNode[] Baseline,JointNode[] Selected,
    EncodingPatch[] Patches,string[] Uncovered,string BaselineArchive,string SelectedArchive,bool PatchLimitReached,
    NeighborhoodStage[]? Stages=null,JsonElement[]? Decisions=null,int Accepted=0);
public sealed record NeighborhoodStage(string Title,NeighborhoodGraphView Graph);
public sealed record NeighborhoodGraphView(double[][][] Curves,JointNode[] Joints);
public sealed record EncodingPatch(string Id,string[] Profiles,string[] Baseline,string[] BaselineKinds,EncodingCandidate[] Candidates,
    int[] Ranking,int? Proposed,string Status,double[] Encounter,bool Changed,double[][][]? Runs=null);
public sealed record EncodingCandidate(string Kind,double Score,double[][] Points,int[][] Segments,double[] Directions);
public sealed record EncodingItem(string Path,string SourceKey,string Category,int Patches);
public sealed record EncodingRun(string Folder,string Provider,int SourceDrawings,int LocalPatches,int ChangedPatches,int Proposed,int Unresolved,
    EncodingItem[] Drawings);

public static class EncodingWorkspace
{
    private static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true};
    public static string Artifact(string folder,string relative)
    {
        var root=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var path=Path.GetFullPath(Path.Combine(root,relative));
        if(Path.IsPathRooted(relative)||!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Encoding artifact is outside its run folder.");
        return path;
    }
    public static EncodingRun Open(string folder)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"encoding.json")));var data=document.RootElement;
        if(data.GetProperty("schema").GetString() is not ("gojo.paired-joint-encoding.v1" or "gojo.neighborhood-consolidation.v1") || data.GetProperty("status").GetString()!="completed")
            throw new InvalidDataException("Choose a completed paired encoding run.");
        var items=JsonSerializer.Deserialize<EncodingItem[]>(data.GetProperty("drawings"),Json) ?? [];
        foreach(var item in items)_=Artifact(folder,item.Path);
        return new(Path.GetFullPath(folder),data.GetProperty("provider").GetString()!,data.GetProperty("sourceDrawings").GetInt32(),
            data.GetProperty("localPatches").GetInt32(),data.GetProperty("changedPatches").GetInt32(),data.GetProperty("proposed").GetInt32(),
            data.GetProperty("unresolved").GetInt32(),items);
    }
    public static EncodingDrawing Drawing(EncodingRun run,int index)
    {
        var text=File.ReadAllText(Artifact(run.Folder,run.Drawings[index].Path));
        using var document=JsonDocument.Parse(text);var data=document.RootElement;
        if(data.TryGetProperty("stages",out var stages))
        {
            var parsed=JsonSerializer.Deserialize<NeighborhoodStage[]>(stages,Json)!;
            var archive=data.GetProperty("nativeArchive").GetString()!;_=Artifact(run.Folder,archive);
            return new(data.GetProperty("sourceKey").GetString()!,data.GetProperty("category").GetString()!,JsonSerializer.Deserialize<double[][][]>(data.GetProperty("strokes"),Json)!,
                parsed[0].Graph.Joints,parsed[^1].Graph.Joints,[],[],archive,archive,false,parsed,
                data.GetProperty("decisions").EnumerateArray().Select(e=>e.Clone()).ToArray(),data.GetProperty("accepted").GetInt32());
        }
        var drawing=JsonSerializer.Deserialize<EncodingDrawing>(text,Json)
            ?? throw new InvalidDataException("Unreadable paired drawing projection.");
        _=Artifact(run.Folder,drawing.BaselineArchive);_=Artifact(run.Folder,drawing.SelectedArchive);
        return drawing;
    }
}
