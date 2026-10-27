using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gojo.Experiments.QuickDraw;
using GojoQuickDraw.Native;

return Worker.Run(args, Console.In, Console.Out, Console.Error);

namespace GojoQuickDraw.Native
{
    public static class Worker
    {
        public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
        {
            try
            {
                if (args.Length == 5 && args[0] is "construct" or "gestalt-evidence")
                {
                    var forwarded = args.ToArray(); forwarded[0] = "--" + args[0];
                    return args[0] == "construct" ? ConstructionLadderRunner.Run(forwarded) : QuickDrawGestaltEvidenceRunner.Run(forwarded);
                }
                if (args.Length >= 3 && args[0] == "inspect" && args[1] == "--input")
                {
                    var read = NativeRecord.Restore(ReadCompressed(args[2]));
                    output.WriteLine(JsonSerializer.Serialize(new { ok = true, summary = NativeRecord.Summarize(read),
                        jointComparison = args.Contains("--joints") ? JointReadback.Json(read) : (JsonElement?)null,
                        sourceOriginalJson = args.Contains("--source") ? read.SourceOriginalJson : null }));
                    return 0;
                }
                if (args.Length != 3 || args[0] != "encode" || args[1] != "--output")
                { error.WriteLine("Usage: encode --output DIR (NDJSON stdin); inspect --input ARCHIVE [--source] [--joints]; construct|gestalt-evidence --input FILE --output DIR"); return 2; }
                var destination = Path.GetFullPath(args[2]); Directory.CreateDirectory(destination);
                string? line;
                while ((line = input.ReadLine()) is not null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string? key = null;
                    try
                    {
                        using var packet = JsonDocument.Parse(line);
                        key = packet.RootElement.GetProperty("itemKey").GetString();
                        if (key is null || !Regex.IsMatch(key, "\\A[A-Za-z0-9_-]{1,128}\\z", RegexOptions.CultureInvariant))
                            throw new InvalidDataException("itemKey must contain 1..128 ASCII letters, digits, '-' or '_'.");
                        var root = NativeRecord.Import(packet.RootElement);
                        var json = NativeRecord.Capture(key, root);
                        NativeRecord.Read restored;
                        var filename = key + ".gojo.json.gz";
                        var path = Path.Combine(destination, filename); var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                        try
                        {
                            using (var file = File.Create(temporary))
                            using (var gzip = new GZipStream(file, CompressionLevel.SmallestSize))
                            using (var writer = new StreamWriter(gzip, new UTF8Encoding(false))) writer.Write(json);
                            // Validate bytes actually stored, including gzip integrity, before publishing.
                            restored = NativeRecord.Restore(ReadCompressed(temporary));
                            File.Move(temporary, path, overwrite: true);
                        }
                        finally { if (File.Exists(temporary)) File.Delete(temporary); }
                        var stageViews = restored.DrawingSteps?.Construction.Rungs.Select((rung, i) => new
                        {
                            index = i,
                            profiles = rung.Visible.Select(edge => edge.Controls.Select(site => new[] { site.Position.X, site.Position.Y }).ToArray()).ToArray(),
                            residue = restored.DrawingSteps.Passes[i].Residue.Select(edge => edge.Controls.Select(site => new[] { site.Position.X, site.Position.Y }).ToArray()).ToArray()
                        }).ToArray();
                        output.WriteLine(JsonSerializer.Serialize(new { ok = true, itemKey = key, archive = filename,
                            summary = NativeRecord.Summarize(restored), stageViews, jointComparison = JointReadback.Json(restored) }));
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
                    { output.WriteLine(JsonSerializer.Serialize(new { ok = false, itemKey = key, error = exception.Message, errorType = exception.GetType().Name })); }
                    output.Flush();
                }
                return 0;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            { error.WriteLine(exception.Message); return 1; }
        }
        private static string ReadCompressed(string path)
        {
            using var file = File.OpenRead(path); using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, new UTF8Encoding(false, true)); return reader.ReadToEnd();
        }
    }
}
