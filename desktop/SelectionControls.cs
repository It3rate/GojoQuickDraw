using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace GojoQuickDraw.Desktop;

internal sealed partial class MainWindow
{
    private readonly HashSet<string> _sourceCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextBox _samplingSeed = new() { Watermark = "New random selection" };
    private readonly ComboBox _folderScope = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _inspectionOrder = new() { ItemsSource = new[] { "Category / source order", "Most recent", "Random" }, SelectedIndex = 0,
        HorizontalAlignment = HorizontalAlignment.Stretch };
    private bool _updatingFolders;
    private long _inspectionSeed, _catalogRevision;
    private CancellationTokenSource? _catalogCancellation;
    private string InspectionOrder => _inspectionOrder.SelectedIndex switch { 1 => "recent", 2 => "random", _ => "category" };
    private string InspectionPath => (_folderScope.SelectedItem as FolderOption)?.Path ?? _dataset.Text?.Trim() ?? "";

    private void InitializeSelectionControls()
    {
        _samplingSeed.Text = _settings.SamplingSeed; _inspectionSeed = _settings.InspectionSeed;
        _inspectionOrder.SelectedIndex = _settings.InspectionOrder switch { "recent" => 1, "random" => 2, _ => 0 };
        _folderScope.SelectionChanged += async (_, _) => { if (!_updatingFolders && _folderScope.SelectedItem is not null) await LoadCatalog(refreshFolders: false); };
        _inspectionOrder.SelectionChanged += async (_, _) => { if (!_updatingFolders && _loadedDataset is not null) await LoadCatalog(refreshFolders: false); };
    }

    private void SetSourceCategories(IEnumerable<string> selected)
    {
        var wanted = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
        _sourceCategories.Clear();
        foreach (var name in CategoryCatalog.Names.Where(wanted.Contains)) _sourceCategories.Add(name);
        _category.Content = _sourceCategories.Count switch
        {
            0 => "Choose categories…", 1 => _sourceCategories.Single() + "  ·  Change…",
            345 => "All 345 categories  ·  Change…", _ => $"{_sourceCategories.Count} categories selected  ·  Change…"
        };
        ToolTip.SetTip(_category, string.Join(", ", _sourceCategories.Order(StringComparer.OrdinalIgnoreCase).Take(40)));
    }

    private async Task ChooseCategories()
    {
        var picker = new CategoryPicker(_sourceCategories);
        var selected = await picker.ShowDialog<string[]?>(this);
        if (selected is not null) SetSourceCategories(selected);
    }

    private Control InspectionControls()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,240,Auto"), ColumnSpacing = 12 };
        grid.Children.Add(Field("Generated folder", _folderScope));
        var order = Field("Inspection order", _inspectionOrder); Grid.SetColumn(order, 1); grid.Children.Add(order);
        var shuffle = Button("New random sample", async () =>
        {
            _inspectionSeed = Random.Shared.NextInt64(long.MaxValue);
            _updatingFolders = true; _inspectionOrder.SelectedIndex = 2; _updatingFolders = false;
            await LoadCatalog(refreshFolders: false);
        });
        shuffle.VerticalAlignment = VerticalAlignment.Bottom; Grid.SetColumn(shuffle, 2); grid.Children.Add(shuffle);
        return grid;
    }

    private async Task LoadCatalog(bool announce = true, bool refreshFolders = true)
    {
        var root = _dataset.Text?.Trim() ?? "";
        var revision = ++_catalogRevision;
        _catalogCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _catalogCancellation = cancellation;
        try
        {
            if (announce) SetStatus("Loading drawings…", "Discovering generated folders and selecting drawings from their saved journals.");
            if (refreshFolders || _folderScope.SelectedItem is null)
            {
                var folders = await Task.Run(() => Workspace.GeneratedFolders(root));
                if (revision != _catalogRevision || _dataset.Text?.Trim() != root) return;
                var previous = (_folderScope.SelectedItem as FolderOption)?.Path;
                var choices = new[] { new FolderOption($"All categories ({folders.Count} generated folders)", root) }.Concat(folders).ToArray();
                _updatingFolders = true;
                _folderScope.ItemsSource = choices;
                _folderScope.SelectedItem = choices.FirstOrDefault(folder => folder.Path == previous) ?? choices[0];
                _updatingFolders = false;
                if (folders.Count == 0)
                {
                    _loadedDataset = null; _drawings.ItemsSource = Array.Empty<DrawingRow>();
                    _datasetSummary.Text = "No generated category folders yet. Choose categories and run a sample.";
                    if (announce) SetStatus("No drawings generated yet", "Use Convert to create category datasets in this collection.");
                    return;
                }
            }
            var plan = Pipeline.Plan(PipelineAction.Catalog, Options(PipelineAction.Catalog, Path.GetTempPath()));
            var result = await Pipeline.Run(plan, _ => { }, cancellation.Token);
            if (revision != _catalogRevision || _dataset.Text?.Trim() != root || result.Cancelled) return;
            if (!result.Successful || result.Data is null) throw new InvalidDataException(result.Error);
            var catalog = Workspace.ReadCatalogData(result.Data.Value);
            var key = (_drawings.SelectedItem as DrawingRow)?.QualifiedId;
            _loadedDataset = root; _drawings.ItemsSource = catalog.Drawings;
            _drawings.SelectedItem = catalog.Drawings.FirstOrDefault(row => row.QualifiedId == key) ?? catalog.Drawings.FirstOrDefault();
            _datasetSummary.Text = $"{catalog.Stored:N0} stored drawings in scope · {catalog.Errors:N0} retained errors · {catalog.Drawings.Count:N0} shown";
            var folder = catalog.Drawings.FirstOrDefault()?.DatasetPath ?? InspectionPath;
            if (File.Exists(Path.Combine(folder, "manifest.json")))
            {
                using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "manifest.json")));
                if (manifest.RootElement.TryGetProperty("config", out var config))
                {
                    _maxPoints.Value = config.GetProperty("maxPoints").GetInt32(); _maxSpans.Value = config.GetProperty("maxSpans").GetInt32();
                    if (config.GetProperty("fit").TryGetProperty("polylineTolerance", out var fit)) _fit.Value = fit.GetDecimal();
                }
            }
            if (announce) SetStatus("Drawings loaded", "Folder scope and order also apply to visual reviews and limited verification. Subfolders are always included.");
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (revision != _catalogRevision) return;
            if (announce) SetStatus("Could not load generated drawings", error.Message, true); else AppendLog("Could not reload drawings: " + error.Message);
        }
        finally { if (ReferenceEquals(_catalogCancellation, cancellation)) _catalogCancellation = null; }
    }
}
