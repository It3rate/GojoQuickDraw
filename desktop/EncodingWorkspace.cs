using System.Text.Json;
using GojoQuickDraw.Reads;

namespace GojoQuickDraw.Desktop;

public sealed record EncodingDrawing(string SourceKey,string Category,double[][][] Strokes,JointNode[] Baseline,JointNode[] Selected,
    EncodingPatch[] Patches,string[] Uncovered,string BaselineArchive,string SelectedArchive,bool PatchLimitReached);
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
        if(data.GetProperty("schema").GetString()!="gojo.paired-joint-encoding.v1" || data.GetProperty("status").GetString()!="completed")
            throw new InvalidDataException("Choose a completed paired encoding run.");
        var items=JsonSerializer.Deserialize<EncodingItem[]>(data.GetProperty("drawings"),Json) ?? [];
        foreach(var item in items)_=Artifact(folder,item.Path);
        return new(Path.GetFullPath(folder),data.GetProperty("provider").GetString()!,data.GetProperty("sourceDrawings").GetInt32(),
            data.GetProperty("localPatches").GetInt32(),data.GetProperty("changedPatches").GetInt32(),data.GetProperty("proposed").GetInt32(),
            data.GetProperty("unresolved").GetInt32(),items);
    }
    public static EncodingDrawing Drawing(EncodingRun run,int index)
    {
        var drawing=JsonSerializer.Deserialize<EncodingDrawing>(File.ReadAllText(Artifact(run.Folder,run.Drawings[index].Path)),Json)
            ?? throw new InvalidDataException("Unreadable paired drawing projection.");
        _=Artifact(run.Folder,drawing.BaselineArchive);_=Artifact(run.Folder,drawing.SelectedArchive);
        return drawing;
    }
}
