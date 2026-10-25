using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Media.Imaging;

namespace GojoQuickDraw.Desktop;

internal sealed partial class MainWindow
{
    // Explicit local integration harness: real controls, worker commands, and rendered screenshots.
    // It writes only to a new requested QA directory and never persists desktop settings.
    private async Task RunSmoke()
    {
        string Argument(string key) => Program.Arguments.SkipWhile(value => value != key).Skip(1).FirstOrDefault()
            ?? throw new ArgumentException("Missing " + key);
        var output = Path.GetFullPath(Argument("--qa-output"));
        var input = Path.GetFullPath(Argument("--qa-input"));
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        {
            Environment.ExitCode = 1; _allowClose = true; Close(); return;
        }
        var errors = new List<string>();
        try
        {
            Directory.CreateDirectory(output);
            _autoOpen.IsChecked = false; _dataset.Text = Path.Combine(output, "dataset");
            _input.Text = input; _category.Text = "church"; _limit.Value = 3; _reviewLimit.Value = 3;
            await Execute(PipelineAction.Convert, PipelineAction.Verify, PipelineAction.Review);
            Require(Results.Count == 3 && Results.All(result => result.Successful), "Convert / verify / review did not all succeed.");
            Require(_drawings.Items.Count == 3, "Drawing catalog did not load all three records.");
            Require(_configuration.IsEnabled && !_stop.IsEnabled && !_progress.IsVisible, "Idle controls did not restore.");
            await Capture(Path.Combine(output, "convert.png"));
            _tabs.SelectedIndex = 1; await LoadPreview();
            Require(_raw.Profiles.Length > 0 && _fitted.Profiles.Length > 0, "Selected drawing preview is empty.");
            await Capture(Path.Combine(output, "evidence.png"));
            _tabs.SelectedIndex = 2; await Execute(PipelineAction.Stages);
            Require(Results[^1].Successful && Results[^1].Data!.Value.GetProperty("passes").GetInt32() == 7, "Seven stages were not generated.");
            await Capture(Path.Combine(output, "steps.png"));
            _tabs.SelectedIndex = 3; await Execute(PipelineAction.Probe); await Execute(PipelineAction.ExampleProbe);
            Require(Results.TakeLast(2).All(result => result.Successful), "Machine probes failed.");
            Require(Results[^1].Data!.Value.GetProperty("probes").GetArrayLength() == 4, "Example did not compare all four groups.");
            await Capture(Path.Combine(output, "probes.png"));
            var before = ArchiveHashes(_dataset.Text!);
            var cancel = Execute(PipelineAction.Verify);
            Require(!_configuration.IsEnabled && _stop.IsEnabled, "Running controls were not disabled.");
            await Task.Delay(200); _cancellation?.Cancel(); await cancel;
            Require(Results[^1].Cancelled, "Stop did not cancel the worker.");
            Require(ArchiveHashes(_dataset.Text!) == before, "Stopping verification changed baseline archives.");
            _tabs.SelectedIndex = 4; await Execute(PipelineAction.Setup);
            Require(Results[^1].Successful, "Worker could not restart after cancellation.");
            await Capture(Path.Combine(output, "setup.png"));
            _tabs.SelectedIndex = 1; Width = 920; Height = 760;
            await Capture(Path.Combine(output, "compact.png"));
        }
        catch (Exception error) { errors.Add(error.ToString()); Environment.ExitCode = 1; }
        finally
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(new
            {
                success = errors.Count == 0, errors, dataset = _dataset.Text,
                jobs = Results.Select(result => new { result.ExitCode, result.Cancelled, result.Successful }),
                status = _status.Text, detail = _statusDetail.Text,
                catalogDrawings = _drawings.Items.Count, acquiredStrokesInPreview = _raw.Profiles.Length,
                fittedIntervalsInPreview = _fitted.Profiles.Length,
                cancellationPreservedArchives = errors.Count == 0
            }, new JsonSerializerOptions { WriteIndented = true }));
            _allowClose = true; Close();
        }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static string ArchiveHashes(string dataset) => string.Join("\n", Directory.EnumerateFiles(dataset, "*.gz", SearchOption.AllDirectories)
        .Order().Select(file => Path.GetRelativePath(dataset, file) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))));
    private async Task Capture(string path)
    {
        await Task.Delay(350);
        var size = new PixelSize((int)Math.Ceiling(ClientSize.Width), (int)Math.Ceiling(ClientSize.Height));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        bitmap.Render(this); bitmap.Save(path);
    }
}
