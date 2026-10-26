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
public sealed record Preview(double[][][] Strokes, double[][][] Profiles, PreviewPart[] Areas, PreviewPart[] Cleanup, string[] Warnings);

public static class Workspace
{
    public static IReadOnlyList<FolderOption> GeneratedFolders(string root)
    {
        if (!Directory.Exists(root)) return [];
        var found = new List<FolderOption>();
        var pending = new Stack<string>(); pending.Push(System.IO.Path.GetFullPath(root));
        var ignored = new HashSet<string>(["records", "native", "errors", "_sampling", "_source-index", "review", "qa"], StringComparer.OrdinalIgnoreCase);
        while (pending.TryPop(out var folder))
        {
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
        return new(observed.GetProperty("strokes").EnumerateArray().Select(stroke => Points(stroke.GetProperty("points"))).ToArray(),
            observed.GetProperty("spans").EnumerateArray().Select(span => Points(span.GetProperty("controls"))).ToArray(), Parts("layout"), Parts("cleanup"), warnings);
    }

    public static string RunFolder(string dataset)
    {
        var path = Path.GetFullPath(dataset);
        return Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "-runs", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
    }

    public static string InspectorFolder(string dataset) => Path.Combine(Path.GetFullPath(dataset).TrimEnd(Path.DirectorySeparatorChar) + "-runs", "inspector");
    public static string AnalysisFolder(string dataset) => Path.GetFullPath(dataset).TrimEnd(Path.DirectorySeparatorChar) + "-analysis";

    public static (string Source, string Output) AnalysisPaths(string selected, string? output = null)
    {
        selected = Path.GetFullPath(selected);
        if (!Directory.Exists(selected)) throw new ArgumentException("Choose an existing generated collection to analyze.");
        var source = selected;
        var destination = AnalysisFolder(selected);
        var collection = Path.Combine(selected, "collection.json");
        if (File.Exists(collection))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(collection));
            var root = document.RootElement;
            if (root.TryGetProperty("kind", out var kind) && kind.GetString() == "derived-analysis" && root.TryGetProperty("baseline", out var baseline))
            { source = baseline.GetString()!; destination = selected; }
        }
        return (Path.GetFullPath(source), Path.GetFullPath(output ?? destination));
    }
}
