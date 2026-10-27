using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace GojoQuickDraw.Desktop;

internal sealed partial class MainWindow
{
    // Explicit bounded real-data integration check. All generated data goes to
    // a new QA folder, and this mode never saves the user's settings.
    private async Task RunSmoke()
    {
        string Argument(string key) => Program.Arguments.SkipWhile(value => value != key).Skip(1).FirstOrDefault()
            ?? throw new ArgumentException("Missing " + key);
        var output = Path.GetFullPath(Argument("--qa-output")); var input = Path.GetFullPath(Argument("--qa-input"));
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        { Environment.ExitCode = 1; _allowClose = true; Close(); return; }
        var errors = new List<string>();
        try
        {
            Directory.CreateDirectory(output); _autoOpen.IsChecked = false;
            _dataset.Text = Path.Combine(output, "dataset"); _input.Text = input;
            _limit.Value = 9; _samplingSeed.Text = "42"; _tabs.SelectedIndex = 1;
            var picker = new CategoryPicker([]); picker.Show(this); picker.SetAll(true);
            Require(picker.Selected.Count == 345, "Select all failed."); picker.SetAll(false);
            Require(picker.Selected.Count == 0, "Clear all failed.");
            foreach (var category in new[] { "angel", "church", "frog" }) picker.SelectCategory(category, true);
            SetSourceCategories(picker.Selected); picker.Close();
            await Capture(Path.Combine(output, "convert.png"));
            await Execute(PipelineAction.Convert);
            Require(Results.Count == 1 && Results[0].Successful, "Bounded conversion failed.");
            Require(_catalogTotal == 9 && _galleryCards.Count == 9, "The requested total must be nine across all categories.");
            Require(_tabs.SelectedIndex == 0, "Conversion did not open the viewer.");
            Require(_pagePreviews.Values.Count(p => p.Stages?.Count == 10) == 3, "Convert did not retain all ten church views.");
            Require(_pagePreviews.Values.Count(p => p.Stages?.Count == 2) == 6, "Other categories should retain acquisition and fitted evidence without invented church stages.");
            Require(!Directory.EnumerateFiles(output, "*.html", SearchOption.AllDirectories).Any(), "Conversion unexpectedly required an HTML export.");
            var baseline = _dataset.Text!; var originalHashes = ArchiveHashes(baseline);
            await Execute(PipelineAction.Convert);
            Require(Results[^1].Successful && Results[^1].Data!.Value.GetProperty("resumed").GetInt32() == 9, "Seeded repeat did not reuse nine drawings.");
            Require(ArchiveHashes(baseline) == originalHashes, "Repeat changed saved archives.");
            _updatingFolders = true;
            _folderScope.SelectedItem = _folderScope.Items.Cast<FolderOption>().Single(folder => Path.GetFileName(folder.Path) == "church");
            _updatingFolders = false; await LoadCatalog(refreshFolders: false);
            Require(_catalogTotal == 3 && _galleryCards.Count == 3, "Category browsing failed.");
            Require(_raw.Profiles.Length > 0 && _fitted.Profiles.Length > 0, "Saved previews are empty.");
            _resultView.SelectedIndex = 1; _reviewLimit.Value = 2; await LoadCatalog(refreshFolders: false);
            await ChangePage(2); Require(_galleryCards.Count == 1 && _resultView.SelectedIndex == 1, "Page or result view changed unexpectedly.");
            await ChangePage(0); await Capture(Path.Combine(output, "viewer.png"));
            await CheckViewerKeyboard(output);
            _updatingFolders = true; _folderScope.SelectedIndex = 0; _inspectionOrder.SelectedIndex = 2; _inspectionSeed = 123; _updatingFolders = false;
            await LoadCatalog(refreshFolders: false); var random = _drawings.Items.Cast<DrawingRow>().Select(row => row.QualifiedId).ToArray();
            await LoadCatalog(refreshFolders: false);
            Require(random.SequenceEqual(_drawings.Items.Cast<DrawingRow>().Select(row => row.QualifiedId)), "Random order did not retain its seed.");
            _updatingFolders = true;
            _folderScope.SelectedItem = _folderScope.Items.Cast<FolderOption>().Single(folder => Path.GetFileName(folder.Path) == "church");
            _inspectionOrder.SelectedIndex = 0; _updatingFolders = false; await LoadCatalog(refreshFolders: false);
            _analysisCount.Value = 2; _analysisOutput.Text = Path.Combine(output, "analysis"); _analysisWorkers.Value = 2;
            await Execute(PipelineAction.Analyze);
            Require(Results[^1].Successful && _catalogTotal == 2, "Analysis did not honor its separate count of two.");
            Require(_dataset.Text == _analysisOutput.Text, "The analysis dataset did not open in the viewer.");
            Require(_pagePreviews.Count == 2 && _pagePreviews.Values.All(p => p.Cleanup.Length > 0), "Saved final church shapes are missing.");
            Require(!Directory.EnumerateFiles(output, "*.html", SearchOption.AllDirectories).Any(), "Analysis unexpectedly generated HTML.");
            Require(ArchiveHashes(baseline) == originalHashes, "Analysis changed source archives.");
            _resultView.SelectedIndex = StageOptions.Length - 1; await Capture(Path.Combine(output, "final-shapes.png"));
            for (var stage = 1; stage < StageOptions.Length; stage++)
            {
                _resultView.SelectedIndex = stage;
                Require(_pagePreviews.Values.All(p => Result(p, stage).Id == StageOptions[stage].Id && Result(p, stage).Title != "Stage not saved / not applicable"), "A saved stage cannot be selected.");
                await Capture(Path.Combine(output, $"stage-{stage:00}.png"));
            }
            var enlarge = ShowDrawing((DrawingRow)_drawings.Items[0]!); await Task.Delay(250);
            var detail = OwnedWindows.Single(); await CaptureWindow(detail, Path.Combine(output, "enlarged.png")); detail.Close(); await enlarge;
            var before = ArchiveHashes(_dataset.Text!); _verifyAll.IsChecked = true;
            var cancel = Execute(PipelineAction.Verify); await Task.Delay(200); _cancellation?.Cancel(); await cancel;
            Require(Results[^1].Cancelled, "Stop did not terminate verification.");
            Require(ArchiveHashes(_dataset.Text!) == before, "Stopped verification changed saved data.");
            await Execute(PipelineAction.Setup); Require(Results[^1].Successful, "Tools did not recover after Stop.");
            await Execute(PipelineAction.Refresh);
            Require(Results[^1].Successful && File.Exists(Path.Combine(Workspace.InspectorFolder(_dataset.Text!), "comparison.html")), "Explicit optional browser export failed.");
            Width = 920; Height = 760; await Capture(Path.Combine(output, "compact.png"));
            await CheckBottomReachable(output);
        }
        catch (Exception error) { errors.Add(error.ToString()); Environment.ExitCode = 1; }
        finally
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(new
            {
                success = errors.Count == 0, errors, dataset = _dataset.Text,
                jobs = Results.Select(result => new { result.ExitCode, result.Cancelled, result.Successful }),
                nativeGalleryCards = _galleryCards.Count, finalShapePreviews = _pagePreviews.Values.Count(p => p.Cleanup.Length > 0),
                conversionCount = 9, analysisCount = 2, status = _status.Text
            }, new JsonSerializerOptions { WriteIndented = true }));
            _allowClose = true; Close();
        }
    }

    private async Task CheckViewerKeyboard(string output)
    {
        async Task Key(Window window, Key key, KeyModifiers modifiers = KeyModifiers.None)
        {
            window.RaiseEvent(new KeyEventArgs { RoutedEvent = KeyDownEvent, Source = window, Key = key, KeyModifiers = modifiers });
            await _navigation;
        }
        _resultView.SelectedIndex = 10; _drawings.SelectedIndex = 0;
        await Key(this, Avalonia.Input.Key.Right);
        Require(_drawings.SelectedIndex == 1 && _catalogOffset == 0, "Right did not select next drawing.");
        await Key(this, Avalonia.Input.Key.Right);
        Require(_drawings.SelectedIndex == 0 && _catalogOffset == 2, "Right did not cross the page boundary.");
        await Key(this, Avalonia.Input.Key.Left);
        Require(_drawings.SelectedIndex == 1 && _catalogOffset == 0, "Left did not return to previous page's last drawing.");
        await Key(this, Avalonia.Input.Key.Right, KeyModifiers.Shift);
        Require(_catalogOffset == 2 && _drawings.SelectedIndex == 0 && _resultView.SelectedIndex == 10, "Shift+Right did not preserve the stage on the short final page.");
        await Key(this, Avalonia.Input.Key.Left, KeyModifiers.Shift);
        Require(_catalogOffset == 0 && _drawings.SelectedIndex == 0, "Shift+Left did not select previous page.");
        var edit = new KeyEventArgs { RoutedEvent = KeyDownEvent, Source = _dataset, Key = Avalonia.Input.Key.Right };
        _dataset.RaiseEvent(edit); await _navigation;
        Require(_drawings.SelectedIndex == 0, "Viewer shortcut stole text editing.");
        // A closed stage selector also accepts the global drawing shortcuts.
        _resultView.RaiseEvent(new KeyEventArgs { RoutedEvent = KeyDownEvent, Key = Avalonia.Input.Key.Right });
        await _navigation;
        Require(_drawings.SelectedIndex == 1, "Closed stage selector blocked drawing navigation.");
        await Key(this, Avalonia.Input.Key.Left);
        var enlarged = ShowDrawing((DrawingRow)_drawings.Items[0]!); await Task.Delay(150);
        var detail = OwnedWindows.Single(); var firstTitle = detail.Title;
        await Key(detail, Avalonia.Input.Key.Up);
        Require(_resultView.SelectedIndex == 9 && _drawingStage?.SelectedIndex == 9, "Up did not select the previous stage.");
        await Key(detail, Avalonia.Input.Key.Down);
        await Key(detail, Avalonia.Input.Key.Right);
        Require(detail.Title != firstTitle && detail.Title!.Contains(((DrawingRow)_drawings.SelectedItem!).KeyId), "Enlarged drawing did not update.");
        await Key(detail, Avalonia.Input.Key.Right, KeyModifiers.Shift);
        Require(_catalogOffset == 2 && _resultView.SelectedIndex == 10 && detail.Title!.Contains(((DrawingRow)_drawings.SelectedItem!).KeyId), "Enlarged page movement lost drawing or stage.");
        await CaptureWindow(detail, Path.Combine(output,"keyboard-enlarged.png"));
        detail.Close(); await enlarged;
        Require(_updateDrawing is null && _drawingStage is null, "Closed modal left navigation callbacks attached.");
        await ChangePage(0);
    }

    private async Task CheckBottomReachable(string output)
    {
        for (var tab = 0; tab < 2; tab++)
        {
            _tabs.SelectedIndex = tab;
            var scroll = (ScrollViewer)((TabItem)_tabs.Items[tab]!).Content!;
            await Task.Delay(150); scroll.ScrollToEnd(); await Task.Delay(150);
            Require(scroll.Bounds.Height > 0 && scroll.Viewport.Height > 0, "Tab scroll viewport is unbounded or empty.");
            Require(Math.Abs(scroll.Extent.Height - scroll.Viewport.Height - scroll.Offset.Y) < 3 || scroll.Extent.Height <= scroll.Viewport.Height,
                "Bottom controls cannot be reached by scrolling.");
            var bottom = scroll.TranslatePoint(new Point(0, scroll.Bounds.Height), this);
            Require(bottom is { } p && p.Y <= ClientSize.Height, "Tab extends below the visible window.");
            var content = (StackPanel)((Border)scroll.Content!).Child!;
            var last = content.Children[^1];
            var lastBottom = last.TranslatePoint(new Point(0, last.Bounds.Height), scroll);
            Require(lastBottom is { } end && end.Y <= scroll.Bounds.Height - 1, "The final control is clipped at the end of scrolling.");
            await Capture(Path.Combine(output, $"bottom-tab-{tab}.png"));
        }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static string ArchiveHashes(string dataset) => string.Join("\n", Directory.EnumerateFiles(dataset, "*.gz", SearchOption.AllDirectories)
        .Order().Select(file => Path.GetRelativePath(dataset, file) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))));
    private Task Capture(string path) => CaptureWindow(this, path);
    private static async Task CaptureWindow(Window window, string path)
    {
        await Task.Delay(350);
        var size = new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96)); bitmap.Render(window); bitmap.Save(path);
    }
}
