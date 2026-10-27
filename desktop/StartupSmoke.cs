using System.Text.Json;

namespace GojoQuickDraw.Desktop;

internal sealed partial class MainWindow
{
    // Explicit read-only cold-start audit. Unlike the main smoke workflow this
    // starts with the requested existing folder and does not press Load drawings.
    private async Task RunStartupSmoke()
    {
        var index = Array.IndexOf(Program.Arguments, "--qa-output");
        var output = index >= 0 && index + 1 < Program.Arguments.Length ? Path.GetFullPath(Program.Arguments[index + 1])
            : throw new ArgumentException("Provide --qa-output for the startup audit.");
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        { Environment.ExitCode = 1; _allowClose = true; Close(); return; }
        Directory.CreateDirectory(output);
        var errors = new List<string>();
        var first = 0; var last = 0; var total = 0;
        try
        {
            // Browsing must remain available independently of conversion tools.
            _python.Text = "not-installed-python"; _gojo.Text = Path.Combine(output, "no-gojo-checkout");
            await LoadCatalog();
            Require(_loadedDataset == _dataset.Text?.Trim(), "Cold-start folder was not loaded.");
            Require(_drawings.Items.Count > 0, "Cold start left the first page empty.");
            _tabs.SelectedIndex = 0; await LoadPreview();
            Require(_tabs.Items.Count == 2, "The main workflows should be View and Convert.");
            Require(_galleryCards.Count == _drawings.Items.Count, "Native comparison cards are missing.");
            total = _catalogTotal; first = _drawings.Items.Count;
            _resultView.SelectedIndex = 1;
            var size = (int)_reviewLimit.Value!;
            var offset = (total - 1) / size * size;
            await ChangePage(offset); last = _drawings.Items.Count;
            Require(_resultView.SelectedIndex == 1, "Paging reset the selected result view.");
            Require(last == total - offset, "Final page omitted drawings.");
            await Capture(Path.Combine(output, "last-page.png"));
            await ChangePage(0);
            Require(_drawings.Items.Count == first, "First page did not restore.");
            Require(_raw.Profiles.Length > 0, "Cold-start preview is empty.");
            var inspector = Path.Combine(Workspace.InspectorFolder(_dataset.Text!), "evidence.html");
            if (File.Exists(inspector)) Require(_openResult.IsEnabled, "Existing inspector was not available on cold start.");
            await Capture(Path.Combine(output, "first-page.png"));
            var keys = Program.Arguments.Select((value,i) => (value,i))
                .Where(p => p.value == "--qa-key" && p.i + 1 < Program.Arguments.Length)
                .Select(p => Program.Arguments[p.i + 1]).ToArray();
            if (keys.Length > 0)
            {
                var catalog = _browser.Read(InspectionPath, _catalogTotal, 0, InspectionOrder, _inspectionSeed, CancellationToken.None);
                for (var i = 0; i < keys.Length; i++)
                {
                    var position = catalog.Drawings.ToList().FindIndex(row => row.KeyId == keys[i]);
                    Require(position >= 0, "Requested visual check drawing is missing: " + keys[i]);
                    await ChangePage(position / size * size); _resultView.SelectedIndex = 10;
                    var row = _drawings.Items.Cast<DrawingRow>().Single(row => row.KeyId == keys[i]);
                    var enlarged = ShowDrawing(row); await Task.Delay(150); var detail = OwnedWindows.Single();
                    await CaptureWindow(detail, Path.Combine(output,$"case-{i+1:00}.png"));
                    detail.Close(); await enlarged;
                }
                _resultView.SelectedIndex = 1; await ChangePage(0);
            }
            _tabs.SelectedIndex = 1; await Capture(Path.Combine(output, "convert.png")); _tabs.SelectedIndex = 0;
            Width = 920; Height = 760; await CheckBottomReachable(output);
        }
        catch (Exception error) { errors.Add(error.ToString()); Environment.ExitCode = 1; }
        finally
        {
            File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(new
            {
                success = errors.Count == 0, errors, dataset = _dataset.Text, total, firstPageRecords = first,
                lastPageRecords = last, pageText = _pageLabel.Text, existingInspectorAvailable = _openResult.IsEnabled,
                startupRequiredManualLoad = false, viewerWorksWithoutPythonOrGojo = true,
                nativeGalleryCards = _galleryCards.Count, resultViewPreserved = _resultView.SelectedIndex == 1
            }, new JsonSerializerOptions { WriteIndented = true }));
            _allowClose = true; Close();
        }
    }
}
