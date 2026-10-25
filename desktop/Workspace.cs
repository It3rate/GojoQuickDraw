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

    public static string Repository()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "gojo_quickdraw", "cli.py"))) return directory.FullName;
        throw new DirectoryNotFoundException("Keep the desktop app in the GojoQuickDraw checkout.");
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
    string EvidencePath, string Status, string DatasetPath = "")
{
    public string QualifiedId => DatasetPath + "::" + ItemId;
    public override string ToString() => $"{Category} · {KeyId} · {Intervals} intervals";
}
public sealed record FolderOption(string Label, string Path) { public override string ToString() => Label; }

public sealed record Catalog(IReadOnlyList<DrawingRow> Drawings, int Stored, int Errors);
public sealed record Preview(double[][][] Strokes, double[][][] Profiles);

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
        return new(rows, data.GetProperty("storedRecords").GetInt32(), data.GetProperty("storedErrors").GetInt32());
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
        return new(observed.GetProperty("strokes").EnumerateArray().Select(stroke => Points(stroke.GetProperty("points"))).ToArray(),
            observed.GetProperty("spans").EnumerateArray().Select(span => Points(span.GetProperty("controls"))).ToArray());
    }

    public static string RunFolder(string dataset)
    {
        var path = Path.GetFullPath(dataset);
        return Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "-runs", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
    }
}
