using System.Globalization;
using System.IO.Compression;
using System.Text.Json;

namespace GojoQuickDraw.Desktop;

public sealed class WorkspaceSettings
{
    public string Python { get; set; } = File.Exists("C:/Program Files/Python310/python.exe")
        ? "C:/Program Files/Python310/python.exe" : "python";
    public string Gojo { get; set; } = "";
    public string Input { get; set; } = "";
    public string Dataset { get; set; } = "";
    public string Category { get; set; } = "church";
    public int Limit { get; set; } = 25;
    public string InspectionOrder { get; set; } = "category";
    public long InspectionSeed { get; set; }
    public string SamplingSeed { get; set; } = "";
    public int ResultView { get; set; }
    public string ResultStage { get; set; } = "";

    public static string Repository()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "gojo_quickdraw", "cli.py"))) return directory.FullName;
        // The saved-dataset viewer also works from a copied application folder.
        // Conversion tools still require a checkout, selected in Tool settings.
        return AppContext.BaseDirectory;
    }

    public static string SettingsFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GojoQuickDraw", "desktop-settings.json");

    public static WorkspaceSettings Load(string repository)
    {
        WorkspaceSettings? settings = null;
        try { if (File.Exists(SettingsFile)) settings = JsonSerializer.Deserialize<WorkspaceSettings>(File.ReadAllText(SettingsFile)); }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { }
        settings ??= new();
        if (string.IsNullOrWhiteSpace(settings.Gojo)) settings.Gojo = Path.GetFullPath(Path.Combine(repository, "..", "Gojo"));
        if (string.IsNullOrWhiteSpace(settings.Input)) settings.Input = File.Exists("D:/_quickDraw/quickdraw/church.ndjson")
            ? "D:/_quickDraw/quickdraw/church.ndjson" : Path.Combine(repository, "examples", "church.ndjson");
        if (string.IsNullOrWhiteSpace(settings.Dataset)) settings.Dataset = Directory.Exists("D:/_quickDraw/gojo-quickdraw-evidence/church-pilot")
            ? "D:/_quickDraw/gojo-quickdraw-evidence/church-pilot"
            : NewDataset(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GojoQuickDraw"));
        settings.Dataset = settings.Dataset.Trim();
        return settings;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        var temporary = SettingsFile + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, SettingsFile, true);
    }

    public static string NewDataset(string parent) => Path.Combine(parent, "samples-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
}

public sealed record DrawingRow(string ItemId, string KeyId, string Category, int Strokes, int Samples, int Intervals,
    string EvidencePath, string Status, string DatasetPath = "", DateTimeOffset ConvertedUtc = default)
{
    public string QualifiedId => DatasetPath + "::" + ItemId;
    public override string ToString() => $"{Category} · {KeyId} · {Intervals} intervals";
}
public sealed record FolderOption(string Label, string Path) { public override string ToString() => Label; }

public sealed record Catalog(IReadOnlyList<DrawingRow> Drawings, int Stored, int Errors, int Offset = 0, int PageSize = 12,
    IReadOnlyList<string>? Warnings = null);
public sealed record PreviewPart(string Role, string Shape, string Status, string Color, double[][] Points, int[][] Segments, int[] Fill);
public sealed record PreviewStage(string Id, string Title, double[][][] Profiles, PreviewPart[] Parts, bool Curves = true,
    double[][][]? Residue = null, string Note = "", string[]? Dependencies = null);
public sealed record Preview(double[][][] Strokes, double[][][] Profiles, PreviewPart[] Areas, PreviewPart[] Cleanup, string[] Warnings,
    IReadOnlyList<PreviewStage>? Stages = null, GojoQuickDraw.Reads.JointComparison? Joints = null);

public static class Workspace
{
    public static IReadOnlyList<FolderOption> GeneratedFolders(string root)
    {
        if (!Directory.Exists(root)) return [];
        var found = new List<FolderOption>();
        var pending = new Stack<string>(); pending.Push(System.IO.Path.GetFullPath(root));
        var ignored = new HashSet<string>(["records", "native", "errors", "stages", "_sampling", "_source-index", "review", "qa", "_work"], StringComparer.OrdinalIgnoreCase);
        while (pending.TryPop(out var folder))
        {
            var pipeline = Path.Combine(folder, "pipeline.json");
            if (File.Exists(pipeline))
            {
                using var workspace = JsonDocument.Parse(File.ReadAllText(pipeline));
                if (workspace.RootElement.GetProperty("schema").GetString() == "gojo.quickdraw-pipeline.v1")
                {
                    var child = Path.Combine(folder, File.Exists(Path.Combine(folder, "results", "collection.json")) ? "results" : "acquired");
                    if (Directory.Exists(child)) pending.Push(child);
                    continue;
                }
            }
            var manifest = System.IO.Path.Combine(folder, "manifest.json");
            if (File.Exists(manifest))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(manifest));
                if (document.RootElement.TryGetProperty("schema", out var schema) && schema.GetString() == "gojo.quickdraw-dataset.v1")
                    found.Add(new(System.IO.Path.GetRelativePath(root, folder) == "." ? System.IO.Path.GetFileName(folder) : System.IO.Path.GetRelativePath(root, folder), folder));
            }
            foreach (var child in Directory.EnumerateDirectories(folder))
            {
                var name = System.IO.Path.GetFileName(child);
                if (!ignored.Contains(name) && !name.StartsWith('.') && !name.EndsWith("-runs", StringComparison.OrdinalIgnoreCase)
                    && (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
            }
        }
        return found.OrderBy(folder => folder.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static Catalog ReadCatalogData(JsonElement data)
    {
        var rows = data.GetProperty("records").EnumerateArray().Select(row =>
        {
            var counts = row.GetProperty("counts");
            int Count(string key) => counts.TryGetProperty(key, out var count) ? count.GetInt32() : 0;
            return new DrawingRow(row.GetProperty("itemId").GetString()!, row.GetProperty("keyId").GetString()!,
                row.GetProperty("category").GetString()!, Count("strokes"), Count("samples"), Count("spans"),
                row.GetProperty("evidencePath").GetString()!, "ok", row.GetProperty("datasetPath").GetString()!);
        }).ToArray();
        return new(rows, data.GetProperty("storedRecords").GetInt32(), data.GetProperty("storedErrors").GetInt32(),
            data.TryGetProperty("offset", out var offset) ? offset.GetInt32() : 0,
            data.TryGetProperty("pageSize", out var size) ? size.GetInt32() : 50);
    }

    public static Preview ReadPreview(string dataset, DrawingRow row)
    {
        var root = Path.GetFullPath(dataset) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(dataset, row.EvidencePath));
        if (!path.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Evidence path is outside the dataset.");
        using var input = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
        using var document = JsonDocument.Parse(input);
        var packet = document.RootElement;
        if (packet.GetProperty("itemKey").GetString() != row.ItemId || packet.GetProperty("source").GetProperty("key_id").GetString() != row.KeyId)
            throw new InvalidDataException("Preview identity differs from the selected drawing.");
        var observed = packet.GetProperty("observation");
        double[][] Points(JsonElement points) => points.EnumerateArray().Select(point => point.EnumerateArray().Select(n => n.GetDouble()).ToArray()).ToArray();
        PreviewPart[] Parts(string section)
        {
            if (!packet.TryGetProperty(section, out var layer) || layer.ValueKind != JsonValueKind.Object || !layer.TryGetProperty("parts", out var parts)) return [];
            var result = new List<PreviewPart>();
            foreach (var part in parts.EnumerateArray())
            {
                if (!part.TryGetProperty("geometry", out var geometry) || !geometry.TryGetProperty("points", out var p)) continue;
                var segments = geometry.GetProperty("segments").EnumerateArray().Select(s => s.EnumerateArray().Select(n => n.GetInt32()).ToArray()).ToArray();
                var shown = geometry.TryGetProperty("displaySegments", out var display) ? display.EnumerateArray().Select(n => n.GetInt32()) : Enumerable.Range(0, segments.Length);
                result.Add(new(part.GetProperty("role").GetString()!, part.TryGetProperty("shape", out var shape) ? shape.GetString()! : "area", part.GetProperty("status").GetString()!,
                    part.TryGetProperty("color", out var color) ? color.GetString()! : "#7C8784", Points(p), shown.Select(i => segments[i]).ToArray(),
                    geometry.TryGetProperty("fillIndices", out var fill) ? fill.EnumerateArray().Select(n => n.GetInt32()).ToArray() : []));
            }
            return result.ToArray();
        }
        var warnings = packet.TryGetProperty("cleanup", out var cleanup) && cleanup.TryGetProperty("checks", out var checks)
            ? checks.EnumerateArray().Where(c => c.GetProperty("status").GetString() == "needs-review").Select(c => c.GetProperty("label").GetString()!).ToArray() : [];
        var strokes = observed.GetProperty("strokes").EnumerateArray().Select(stroke => Points(stroke.GetProperty("points"))).ToArray();
        var profiles = observed.GetProperty("spans").EnumerateArray().Select(span => Points(span.GetProperty("controls"))).ToArray();
        var areas = Parts("layout"); var final = Parts("cleanup");
        var stages = new List<PreviewStage> { new("original", "Original drawing", strokes, [], false), new("fitted", "Close fitted outlines", profiles, []) };
        if (packet.TryGetProperty("pipeline", out var pipeline))
        {
            if (pipeline.GetProperty("itemId").GetString() != row.ItemId || pipeline.GetProperty("sourceId").GetString() != row.KeyId)
                throw new InvalidDataException("Saved stages differ from the selected acquisition.");
            foreach (var stage in pipeline.GetProperty("stages").EnumerateArray())
            {
                var id = stage.GetProperty("id").GetString()!;
                var curves = stage.GetProperty("kind").GetString() != "strokes";
                var paths = stage.TryGetProperty("profiles", out var pathsInput) ? pathsInput.EnumerateArray().Select(Points).ToArray() : [];
                var residue = stage.TryGetProperty("residue", out var residueInput) ? residueInput.EnumerateArray().Select(Points).ToArray() : [];
                stages.RemoveAll(s => s.Id == id);
                stages.Add(new(id, stage.GetProperty("title").GetString()!, paths, id == "areas" ? areas : id == "cleanup" ? final : [], curves, residue,
                    stage.TryGetProperty("nativeRestored", out var native) && native.GetBoolean() ? "Saved native construction / evidence; independently restored." : "Saved acquired source evidence.",
                    stage.GetProperty("dependsOn").EnumerateArray().Select(d => d.GetString()!).ToArray()));
            }
        }
        else
        {
            // Older analysis records contain useful proposal caches. Keep their
            // unexecuted status explicit rather than claiming native execution.
            if (packet.TryGetProperty("analysis", out var analysis) && analysis.TryGetProperty("proposal", out var proposal) && proposal.ValueKind == JsonValueKind.Object)
                foreach (var (step, i) in proposal.GetProperty("steps").EnumerateArray().Select((s, i) => (s, i)).Skip(1))
                    stages.Add(new($"outline-{i}", step.GetProperty("title").GetString()!, step.GetProperty("edges").EnumerateArray().Select(e => Points(e.GetProperty("controls"))).ToArray(), [],
                        Note: "Older saved proposal preview; native drawing-stage execution was not saved. Use Complete pipeline to retain executed stages."));
            if (areas.Length > 0) stages.Add(new("areas", "Shape areas and attachments", [], areas));
            if (final.Length > 0) stages.Add(new("cleanup", "Refined church shapes", [], final));
        }
        return new(strokes, profiles, areas, final, warnings, stages, JointPreview.Read(packet));
    }

    public static string RunFolder(string dataset)
    {
        var path = Path.GetFullPath(dataset);
        var work = Directory.Exists("D:/_quickDraw") ? "D:/_quickDraw/_work" : Path.Combine(Path.GetTempPath(), "GojoQuickDraw");
        return Path.Combine(work, "runs", Path.GetFileName(path), DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
    }

    public static string InspectorFolder(string dataset)
    {
        var path = Path.GetFullPath(dataset).TrimEnd(Path.DirectorySeparatorChar);
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path.ToLowerInvariant())))[..12];
        var work = Directory.Exists("D:/_quickDraw") ? "D:/_quickDraw/_work" : Path.Combine(Path.GetTempPath(), "GojoQuickDraw");
        return Path.GetFullPath(Path.Combine(work, "reviews", Path.GetFileName(path) + "-" + key, "inspector"));
    }
    public static string AnalysisFolder(string dataset) => Path.GetFullPath(dataset).TrimEnd(Path.DirectorySeparatorChar) + "-processed";

    public static JsonElement? PipelineManifest(string dataset)
    {
        var path = Path.Combine(dataset, "pipeline.json");
        if (!File.Exists(path)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("schema").GetString() == "gojo.quickdraw-pipeline.v1" ? document.RootElement.Clone() : null;
    }

    public static (string Source, string Output) AnalysisPaths(string selected, string? output = null)
    {
        selected = Path.GetFullPath(selected);
        if (!Directory.Exists(selected)) throw new ArgumentException("Choose an existing generated collection to analyze.");
        var source = selected;
        var destination = AnalysisFolder(selected);
        if (PipelineManifest(selected) is { } workspace)
        {
            var request = workspace.GetProperty("request");
            if (request.GetProperty("mode").GetString() == "existing")
            { source = request.GetProperty("source").GetString()!; destination = selected; }
            else source = Path.Combine(selected, "acquired");
        }
        if (File.Exists(Path.Combine(selected, "manifest.json")))
        {
            // A selected category may sit inside a larger collection. Keep its
            // derived dataset outside that collection so browsing does not count
            // both source and result as independent drawings.
            for (var parent = Directory.GetParent(selected); parent is not null; parent = parent.Parent)
                if (File.Exists(Path.Combine(parent.FullName, "pipeline.json")) || File.Exists(Path.Combine(parent.FullName, "collection.json")))
                {
                    destination = parent.FullName + "-" + Path.GetFileName(selected) + "-processed";
                    if (File.Exists(Path.Combine(parent.FullName, "pipeline.json"))) break;
                }
        }
        var collection = Path.Combine(selected, "collection.json");
        if (File.Exists(collection))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(collection));
            var root = document.RootElement;
            if (root.TryGetProperty("kind", out var kind) && kind.GetString() == "derived-analysis" && root.TryGetProperty("baseline", out var baseline))
            { source = baseline.GetString()!; }
        }
        return (Path.GetFullPath(source), Path.GetFullPath(output ?? destination));
    }
}
