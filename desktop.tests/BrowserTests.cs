using System.IO.Compression;
using System.Text;
using System.Text.Json;
using GojoQuickDraw.Desktop;

namespace GojoQuickDraw.Desktop.Tests;

public sealed class BrowserTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "QuickDraw browser " + Guid.NewGuid().ToString("N"));
    public BrowserTests() { Directory.CreateDirectory(_root); }
    public void Dispose() { Directory.Delete(_root, true); }
    private string Folder(string name)
    {
        var folder = Path.Combine(_root, name); Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "manifest.json"), "{\"schema\":\"gojo.quickdraw-dataset.v1\"}");
        return folder;
    }
    private static string Row(string id, string category, string status = "ok", int second = 0) => JsonSerializer.Serialize(new
    {
        itemId = id, keyId = "source-" + id, category, status, counts = new { strokes = 1, samples = 3, spans = 1 },
        evidencePath = "records/" + id + ".gz", convertedUtc = $"2026-10-26T01:00:{second:00}+00:00"
    }) + "\n";

    [Fact]
    public void FolderViewerPagesAllCategoriesAndLatestCommittedStatusWithoutPythonOrHtml()
    {
        var angel = Folder("angel"); var church = Folder("church");
        File.WriteAllText(Path.Combine(angel, "index.ndjson"), Row("a", "angel") + Row("b", "angel", second: 1));
        File.WriteAllText(Path.Combine(church, "index.ndjson"), Row("c", "church", second: 2) + Row("bad", "church") + Row("bad", "church", "error"));
        var browser = new DatasetBrowser();
        var first = browser.Read(_root, 2); var last = browser.Read(_root, 2, 2);
        Assert.Equal(3, first.Stored); Assert.Equal(1, first.Errors);
        Assert.Equal(new[] { "a", "b" }, first.Drawings.Select(r => r.ItemId));
        Assert.Equal("c", Assert.Single(last.Drawings).ItemId);
        Assert.Equal(church, last.Drawings[0].DatasetPath);
        Assert.Equal("c", browser.Read(_root, 1, order: "recent").Drawings[0].ItemId);
        Assert.Equal(browser.Read(_root, 3, order: "random", seed: 42).Drawings, browser.Read(_root, 3, order: "random", seed: 42).Drawings);
        Assert.Empty(Directory.GetFiles(_root, "*.html", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(_root, "*.sqlite3", SearchOption.AllDirectories));
    }

    [Fact]
    public void StoppedRunCanBeViewedWithoutRepairingOrDiscardingItsInterruptedTail()
    {
        var folder = Folder("church"); var journal = Path.Combine(folder, "index.ndjson");
        var content = Row("done", "church") + "{\"itemId\":\"interrupted\"";
        File.WriteAllText(journal, content);
        var browser = new DatasetBrowser(); var page = browser.Read(_root);
        Assert.Equal("done", Assert.Single(page.Drawings).ItemId); Assert.Single(page.Warnings!);
        Assert.Equal(content, File.ReadAllText(journal));
        File.WriteAllText(journal, Row("done", "church") + Row("second", "church"));
        Assert.Equal(2, browser.Read(_root).Stored);
    }

    [Fact]
    public void MalformedCommittedEntryRemainsAnErrorInsteadOfBeingSilentlySkipped()
    {
        var folder = Folder("church"); File.WriteAllText(Path.Combine(folder, "index.ndjson"), "{\"broken\":\n");
        Assert.ThrowsAny<JsonException>(() => new DatasetBrowser().Read(_root));
    }

    [Fact]
    public void SavedShapePreviewHonorsDisplaySegmentsAndKeepsAddedStatusAndIdentity()
    {
        var folder = Folder("church"); Directory.CreateDirectory(Path.Combine(folder, "records"));
        var packet = new
        {
            itemKey = "a", source = new { key_id = "source-a" },
            observation = new { strokes = new[] { new { points = new[] { new[] { 0.0, 0.0 }, new[] { 5.0, 10.0 } } } },
                spans = new[] { new { controls = new[] { new[] { 0.0, 0.0 }, new[] { 5.0, 10.0 } } } } },
            cleanup = new { parts = new[] { new { role = "window", shape = "rectangle", status = "prior-only", color = "#8c68aa",
                geometry = new { points = new[] { new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 }, new[] { 5.0, 6.0 } },
                    segments = new[] { new[] { 0, 1 }, new[] { 1, 2 } }, displaySegments = new[] { 1 } } } } }
        };
        using (var stream = new GZipStream(File.Create(Path.Combine(folder, "records/a.gz")), CompressionMode.Compress))
            stream.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet)));
        var row = new DrawingRow("a", "source-a", "church", 1, 2, 1, "records/a.gz", "ok", folder);
        var preview = Workspace.ReadPreview(folder, row);
        var shape = Assert.Single(preview.Cleanup);
        Assert.Equal(new[] { 1, 2 }, Assert.Single(shape.Segments)); Assert.Equal("prior-only", shape.Status);
        Assert.Empty(preview.Areas);
        Assert.Throws<InvalidDataException>(() => Workspace.ReadPreview(folder, row with { KeyId = "other" }));
    }
}
