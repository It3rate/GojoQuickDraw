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
            ? "D:/_quickDraw/quickdraw/church.ndjson" : Path.Combine(repository, "examples", "churches.ndjson");
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

    public static string NewDataset(string parent) => Path.Combine(parent, "church-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
}

public sealed record DrawingRow(string ItemId, string KeyId, string Category, int Strokes, int Samples, int Intervals,
    string EvidencePath, string Status)
{
    public override string ToString() => $"{KeyId}   ·   {Strokes} strokes   ·   {Intervals} intervals";
}

public sealed record Catalog(IReadOnlyList<DrawingRow> Drawings, int Stored, int Errors);
public sealed record Preview(double[][][] Strokes, double[][][] Profiles);

public static class Workspace
{
    public static Catalog ReadCatalog(string dataset)
    {
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataset, "manifest.json")));
        using (manifest)
        {
            if (manifest.RootElement.GetProperty("schema").GetString() != "gojo.quickdraw-dataset.v1")
                throw new InvalidDataException("Choose a Gojo QuickDraw dataset folder containing manifest.json.");
            var rows = new Dictionary<string, DrawingRow>();
            using var stream = new FileStream(Path.Combine(dataset, "index.ndjson"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length > 0)
            {
                stream.Seek(-1, SeekOrigin.End);
                if (stream.ReadByte() != '\n') throw new InvalidDataException("The dataset journal has an unfinished entry. Finish or resume conversion before loading it.");
                stream.Position = 0;
            }
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                JsonDocument document;
                try { document = JsonDocument.Parse(line); }
                catch (JsonException) { throw new InvalidDataException("The dataset journal is incomplete. Finish or resume conversion before loading it."); }
                using (document)
                {
                    var row = document.RootElement;
                    var id = row.GetProperty("itemId").GetString()!;
                    // Keep the first 1,000 identities, updating their latest status throughout the journal.
                    if (rows.Count >= 1000 && !rows.ContainsKey(id)) continue;
                    var status = row.GetProperty("status").GetString()!;
                    var counts = row.TryGetProperty("counts", out var count) ? count : default;
                    int Count(string key) => counts.ValueKind == JsonValueKind.Object && counts.TryGetProperty(key, out var value) ? value.GetInt32() : 0;
                    rows[id] = new(id, row.GetProperty("keyId").GetString() ?? "Unparsed record",
                        row.GetProperty("category").GetString() ?? "unknown", Count("strokes"), Count("samples"), Count("spans"),
                        row.TryGetProperty("evidencePath", out var path) ? path.GetString()! : "", status);
                }
            }
            var stored = rows.Values.Count(row => row.Status == "ok");
            var errors = rows.Values.Count(row => row.Status == "error");
            if (manifest.RootElement.TryGetProperty("lastRun", out var last))
            {
                stored = last.GetProperty("storedRecords").GetInt32();
                errors = last.GetProperty("storedErrors").GetInt32();
            }
            return new(rows.Values.Where(row => row.Status == "ok").ToArray(), stored, errors);
        }
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
