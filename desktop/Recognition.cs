using System.IO.Compression;
using System.Text.Json;

namespace GojoQuickDraw.Desktop;

// A read of external acquisition metadata, not a drawing quality judgment.
public static class Recognition
{
    public static readonly string[] Modes = ["all", "recognized", "unrecognized", "unknown"];
    public static readonly string[] Labels = ["All recognition outcomes", "Recognized by game", "Not recognized by game", "Outcome unavailable"];
    public static bool Accepts(bool? outcome, string mode) => mode switch
    {
        "all" => true, "recognized" => outcome is true, "unrecognized" => outcome is false,
        "unknown" => outcome is null, _ => throw new ArgumentException("Unknown game recognition filter.")
    };
    public static bool? Outcome(JsonElement source) => source.TryGetProperty("recognized", out var flag) && flag.ValueKind is JsonValueKind.True or JsonValueKind.False ? flag.GetBoolean() : null;
    public static bool? ReadLegacy(DrawingRow row)
    {
        var folder = Path.GetFullPath(row.DatasetPath);
        var path = Path.GetFullPath(Path.Combine(folder, row.EvidencePath));
        if (!path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Recognition source is outside the dataset.");
        using var stream = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
        using var document = JsonDocument.Parse(stream);
        var packet = document.RootElement;
        if (packet.GetProperty("itemKey").GetString() != row.ItemId || packet.GetProperty("source").GetProperty("key_id").GetString() != row.KeyId)
            throw new InvalidDataException("Recognition source differs from the selected drawing.");
        return Outcome(packet.GetProperty("source"));
    }
}
