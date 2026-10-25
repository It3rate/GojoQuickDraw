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
            _input.Text = input; _limit.Value = 9; _reviewLimit.Value = 9; _samplingSeed.Text = "42";
            var picker = new CategoryPicker([]);
            picker.Show(this); picker.SetAll(true);
            Require(picker.Selected.Count == 345, "Select all did not select every static category.");
            picker.SetAll(false); Require(picker.Selected.Count == 0, "Clear all did not clear categories.");
            foreach (var category in new[] { "angel", "church", "frog" }) picker.SelectCategory(category, true);
            Require(picker.Selected.Count == 3, "Category toggle selection failed.");
            await Task.Delay(200);
            using (var image = new RenderTargetBitmap(new PixelSize((int)picker.ClientSize.Width, (int)picker.ClientSize.Height), new Vector(96, 96)))
            { image.Render(picker); image.Save(Path.Combine(output, "category-picker.png")); }
            SetSourceCategories(picker.Selected); picker.Close();
            await Execute(PipelineAction.Convert, PipelineAction.Verify, PipelineAction.Review);
            Require(Results.Count == 3 && Results.All(result => result.Successful), "Convert / verify / review did not all succeed.");
            Require(_drawings.Items.Count == 9, "Drawing catalog did not load all nine records.");
            Require(_folderScope.Items.Count == 4, "Generated folder selector should contain all plus three actual category folders.");
            var originalHashes = ArchiveHashes(_dataset.Text!);
            await Execute(PipelineAction.Convert);
            Require(Results[^1].Successful && Results[^1].Data!.Value.GetProperty("resumed").GetInt32() == 9, "Explicit-seed repeat did not reuse all nine records.");
            Require(ArchiveHashes(_dataset.Text!) == originalHashes, "Repeating a selected sample changed baseline archives.");
            Require(_configuration.IsEnabled && !_stop.IsEnabled && !_progress.IsVisible, "Idle controls did not restore.");
            await Capture(Path.Combine(output, "convert.png"));
            _tabs.SelectedIndex = 1;
            _updatingFolders = true; _folderScope.SelectedItem = _folderScope.Items.Cast<FolderOption>().Single(folder => Path.GetFileName(folder.Path) == "church"); _updatingFolders = false;
            await LoadCatalog(refreshFolders: false); await LoadPreview();
            Require(_drawings.Items.Count == 3 && _drawings.Items.Cast<DrawingRow>().All(row => row.Category == "church"), "Generated church folder filter failed.");
            Require(_raw.Profiles.Length > 0 && _fitted.Profiles.Length > 0, "Selected drawing preview is empty.");
            await Capture(Path.Combine(output, "evidence.png"));
            _updatingFolders = true; _folderScope.SelectedIndex = 0; _inspectionOrder.SelectedIndex = 1; _updatingFolders = false;
            await LoadCatalog(refreshFolders: false);
            Require(_drawings.Items.Count == 9 && ((DrawingRow)_drawings.Items[0]!).Category == "frog", "Most-recent ordering did not select the latest generated category.");
            _updatingFolders = true; _inspectionOrder.SelectedIndex = 2; _inspectionSeed = 123; _updatingFolders = false;
            await LoadCatalog(refreshFolders: false);
            var randomOrder = _drawings.Items.Cast<DrawingRow>().Select(row => row.QualifiedId).ToArray();
            await LoadCatalog(refreshFolders: false);
            Require(randomOrder.SequenceEqual(_drawings.Items.Cast<DrawingRow>().Select(row => row.QualifiedId)), "Random inspection order did not retain its seed.");
            _reviewLimit.Value = 4; await Execute(PipelineAction.Review);
            Require(Results[^1].Successful && Results[^1].Data!.Value.GetProperty("drawings").GetInt32() == 9 && Results[^1].Data!.Value.GetProperty("pages").GetInt32() == 3, "Paged random multi-folder review failed.");
            Require(_drawings.Items.Count == 4, "Catalog page should contain four records.");
            await ChangePage(8); Require(_drawings.Items.Count == 1, "Final catalog page should contain one record.");
            await ChangePage(0); Require(_drawings.Items.Count == 4, "First catalog page did not reload.");
            await Capture(Path.Combine(output, "all-categories.png"));
            _updatingFolders = true; _folderScope.SelectedItem = _folderScope.Items.Cast<FolderOption>().Single(folder => Path.GetFileName(folder.Path) == "church"); _updatingFolders = false;
            await LoadCatalog(refreshFolders: false);
            _tabs.SelectedIndex = 2; await Execute(PipelineAction.Stages);
            Require(Results[^1].Successful && Results[^1].Data!.Value.GetProperty("passes").GetInt32() == 7, "Seven stages were not generated.");
            await Capture(Path.Combine(output, "steps.png"));
            _tabs.SelectedIndex = 3; await Execute(PipelineAction.Probe); await Execute(PipelineAction.ExampleProbe);
            Require(Results.TakeLast(2).All(result => result.Successful), "Machine probes failed.");
            Require(Results[^1].Data!.Value.GetProperty("probes").GetArrayLength() == 4, "Example did not compare all four groups.");
            Require(Results[^1].Data!.Value.GetProperty("containmentMovement").GetArrayLength() == 1, "Example did not expose the container-carry default.");
            await Capture(Path.Combine(output, "probes.png"));
            var before = ArchiveHashes(_dataset.Text!);
            var cancel = Execute(PipelineAction.Verify);
            Require(!_configuration.IsEnabled && _stop.IsEnabled, "Running controls were not disabled.");
            await Task.Delay(200); _cancellation?.Cancel(); await cancel;
            Require(Results[^1].Cancelled, "Stop did not cancel the worker.");
            Require(ArchiveHashes(_dataset.Text!) == before, "Stopping verification changed baseline archives.");
            _tabs.SelectedIndex = 4; await Execute(PipelineAction.Setup);
            Require(Results[^1].Successful, "Setup did not recover after cancellation.");
            _tabs.SelectedIndex = 1;
            _updatingFolders = true; _folderScope.SelectedIndex = 0; _updatingFolders = false;
            await LoadCatalog(refreshFolders: false);
            _analysisOutput.Text = Path.Combine(output, "analysis"); _analysisWorkers.Value = 2;
            await Execute(PipelineAction.Analyze, PipelineAction.Review);
            Require(Results.TakeLast(2).All(result => result.Successful), "Full analysis / paged review failed.");
            Require(Results[^2].Data!.Value.GetProperty("storedRecords").GetInt32() == 9, "Analysis did not include all nine drawings.");
            Require(ArchiveHashes(Path.Combine(output, "dataset")) == before, "Analysis modified baseline archives.");
            Require(_dataset.Text == Path.Combine(output, "analysis"), "Analysis collection was not selected for inspection.");
            await Capture(Path.Combine(output, "analysis.png"));
            await Execute(PipelineAction.Gallery);
            Require(Results[^1].Successful && Results[^1].Data!.Value.GetProperty("drawings").GetInt32() == 9,
                "Input / final gallery did not include all nine analyzed drawings.");
            Require(Results[^1].Data!.Value.GetProperty("linkedDrawings").GetInt32() == 9
                && Results[^1].Data!.Value.GetProperty("finals").GetInt32() == 3,
                "Gallery links or church final interpretations are missing.");
            Require(ArchiveHashes(Path.Combine(output, "dataset")) == before, "Gallery changed baseline archives.");
            await Execute(PipelineAction.Analyze);
            Require(Results[^1].Successful && Results[^1].Data!.Value.GetProperty("resumed").GetInt32() == 9, "Analysis did not resume its original baseline.");
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
                catalogDrawings = _drawings.Items.Count, generatedFolderOptions = _folderScope.Items.Count,
                selectedSourceCategories = _sourceCategories, totalSampleSize = _limit.Value,
                acquiredStrokesInPreview = _raw.Profiles.Length,
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
