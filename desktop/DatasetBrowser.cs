using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GojoQuickDraw.Desktop;

// Read-only UI projection of committed journals. Browsing does not need Python,
// build native evidence, repair journals, or write a generated HTML inspector.
public sealed class DatasetBrowser
{
    private sealed record Snapshot(long Length, DateTime Modified, DrawingRow[] Rows, string[] Warnings);
    private readonly Dictionary<string, Snapshot> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Catalog Read(string root, int size = 12, int offset = 0, string order = "category", long seed = 0,
        CancellationToken cancellation = default)
    {
        lock (_cache) return ReadPage(root, size, offset, order, seed, cancellation);
    }

    private Catalog ReadPage(string root, int size, int offset, string order, long seed, CancellationToken cancellation)
    {
        if (size < 1 || offset < 0) throw new ArgumentException("Page size must be positive and offset nonnegative.");
        if (order is not ("category" or "recent" or "random")) throw new ArgumentException("Unknown drawing order.");
        var rows = new List<DrawingRow>(); var warnings = new List<string>();
        foreach (var folder in Workspace.GeneratedFolders(root))
        {
            cancellation.ThrowIfCancellationRequested();
            var journal = Path.Combine(folder.Path, "index.ndjson");
            var info = new FileInfo(journal);
            if (!info.Exists) { warnings.Add($"{folder.Label}: no drawing index yet."); continue; }
            if (!_cache.TryGetValue(journal, out var snapshot) || snapshot.Length != info.Length || snapshot.Modified != info.LastWriteTimeUtc)
            {
                snapshot = ReadJournal(journal, folder.Path, info.Length, info.LastWriteTimeUtc, cancellation);
                _cache[journal] = snapshot;
            }
            rows.AddRange(snapshot.Rows); warnings.AddRange(snapshot.Warnings);
        }
        var errors = rows.Count(row => row.Status == "error");
        IEnumerable<DrawingRow> successful = rows.Where(row => row.Status == "ok");
        if (order == "recent") successful = successful.OrderByDescending(row => row.ConvertedUtc).ThenBy(row => row.QualifiedId, StringComparer.Ordinal);
        if (order == "random") successful = successful.OrderBy(row => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed.ToString(CultureInfo.InvariantCulture) + ":" + row.QualifiedId))), StringComparer.Ordinal);
        var total = rows.Count(row => row.Status == "ok");
        offset = Math.Min(offset / size * size, Math.Max(0, (total - 1) / size * size));
        return new(successful.Skip(offset).Take(size).ToArray(), total, errors, offset, size, warnings);
    }

    private static Snapshot ReadJournal(string path, string folder, long length, DateTime modified, CancellationToken cancellation)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        // Capture a fixed journal prefix while another process may append to it.
        var committedTail = length == 0;
        if (length > 0) { file.Position = length - 1; committedTail = file.ReadByte() == '\n'; file.Position = 0; }
        using var prefix = new PrefixStream(file, length);
        using var reader = new StreamReader(prefix, Encoding.UTF8);
        var latest = new Dictionary<string, DrawingRow>(); var sequence = new List<string>(); var warnings = new List<string>();
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            cancellation.ThrowIfCancellationRequested(); lineNumber++;
            if (!committedTail && reader.EndOfStream)
            { warnings.Add($"{Path.GetFileName(folder)}: an interrupted final index entry was skipped; completed drawings remain viewable."); break; }
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line); var row = document.RootElement;
            string Text(string key) => row.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";
            var id = Text("itemId");
            if (id.Length == 0) throw new InvalidDataException($"Drawing index entry {lineNumber} has no identity.");
            int Count(string key) => row.TryGetProperty("counts", out var counts) && counts.TryGetProperty(key, out var value) ? value.GetInt32() : 0;
            var time = DateTimeOffset.TryParse(Text("convertedUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var stamp) ? stamp : DateTimeOffset.MinValue;
            var entry = new DrawingRow(id, Text("keyId"), Text("category"), Count("strokes"), Count("samples"), Count("spans"),
                Text("evidencePath"), Text("status"), folder, time);
            if (!latest.ContainsKey(id)) sequence.Add(id);
            latest[id] = entry;
        }
        return new(length, modified, sequence.Select(id => latest[id]).ToArray(), warnings.ToArray());
    }

    private sealed class PrefixStream(Stream source, long remaining) : Stream
    {
        public override int Read(byte[] buffer, int offset, int count)
        { var read = source.Read(buffer, offset, (int)Math.Min(count, remaining)); remaining -= read; return read; }
        public override int Read(Span<byte> buffer)
        { var read = source.Read(buffer[..(int)Math.Min(buffer.Length, remaining)]); remaining -= read; return read; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
