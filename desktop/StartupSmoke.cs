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
            await LoadCatalog();
            Require(_loadedDataset == _dataset.Text?.Trim(), "Cold-start folder was not loaded.");
            Require(_drawings.Items.Count > 0, "Cold start left the first page empty.");
            _tabs.SelectedIndex = 1; await LoadPreview();
            total = _catalogTotal; first = _drawings.Items.Count;
            var size = (int)_reviewLimit.Value!;
            var offset = (total - 1) / size * size;
            await ChangePage(offset); last = _drawings.Items.Count;
            Require(last == total - offset, "Final page omitted drawings.");
            await Capture(Path.Combine(output, "last-page.png"));
            await ChangePage(0);
            Require(_drawings.Items.Count == first, "First page did not restore.");
            Require(_raw.Profiles.Length > 0, "Cold-start preview is empty.");
            var inspector = Path.Combine(Workspace.InspectorFolder(_dataset.Text!), "evidence.html");
            if (File.Exists(inspector)) Require(_openResult.IsEnabled, "Existing inspector was not available on cold start.");
            await Capture(Path.Combine(output, "first-page.png"));
        }
        catch (Exception error) { errors.Add(error.ToString()); Environment.ExitCode = 1; }
        finally
        {
            File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(new
            {
                success = errors.Count == 0, errors, dataset = _dataset.Text, total, firstPageRecords = first,
                lastPageRecords = last, pageText = _pageLabel.Text, existingInspectorAvailable = _openResult.IsEnabled,
                startupRequiredManualLoad = false
            }, new JsonSerializerOptions { WriteIndented = true }));
            _allowClose = true; Close();
        }
    }
}
