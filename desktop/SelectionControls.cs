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
    private readonly ComboBox _inspectionRecognition = new() { ItemsSource = Recognition.Labels, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private bool _updatingFolders;
    private long _inspectionSeed, _catalogRevision;
    private readonly DatasetBrowser _browser = new();
    private CancellationTokenSource? _catalogCancellation;
    private int _catalogOffset, _catalogTotal;
    private readonly NumericUpDown _pageJump = Number(1, 1, 1000000);
    private readonly TextBlock _pageLabel = Text("Page 1");
    private Button _firstPage = null!, _previousPage = null!, _nextPage = null!, _lastPage = null!;
    private string InspectionOrder => _inspectionOrder.SelectedIndex switch { 1 => "recent", 2 => "random", _ => "category" };
    private string InspectionRecognition => Recognition.Modes[Math.Max(0, _inspectionRecognition.SelectedIndex)];
    private string InspectionPath
    {
        get
        {
            var root = _dataset.Text?.Trim() ?? "";
            // TextChanged can arrive after an analysis workflow starts its next
            // command. A folder from the previous collection cannot select it.
            return _loadedDataset == root ? (_folderScope.SelectedItem as FolderOption)?.Path ?? root : root;
        }
    }

    private void InitializeSelectionControls()
    {
        _samplingSeed.Text = _settings.SamplingSeed; _inspectionSeed = _settings.InspectionSeed;
        _analysisCount.Value = Math.Max(1, _settings.Limit);
        var stageId = string.IsNullOrWhiteSpace(_settings.ResultStage) ? _settings.ResultView switch { 1 => "fitted", 2 => "areas", 3 => "cleanup", _ => "best" } : _settings.ResultStage;
        _resultView.SelectedIndex = Math.Max(0, Array.FindIndex(StageOptions, stage => stage.Id == stageId));
        _inspectionOrder.SelectedIndex = _settings.InspectionOrder switch { "recent" => 1, "random" => 2, _ => 0 };
        _inspectionRecognition.SelectedIndex = Math.Max(0, Array.IndexOf(Recognition.Modes, _settings.InspectionRecognition));
        _folderScope.SelectionChanged += async (_, _) => { if (!_updatingFolders && _folderScope.SelectedItem is not null) await LoadCatalog(refreshFolders: false); };
        _inspectionOrder.SelectionChanged += async (_, _) => { if (!_updatingFolders && _loadedDataset is not null) await LoadCatalog(refreshFolders: false); };
        _inspectionRecognition.SelectionChanged += async (_, _) => { if (!_updatingFolders && _loadedDataset is not null) await LoadCatalog(refreshFolders: false); };
        _reviewLimit.ValueChanged += async (_, _) => { if (_loadedDataset is not null) await LoadCatalog(refreshFolders: false); };
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
        grid.Children.Add(Field("Categories in this dataset", _folderScope));
        var order = Field("Inspection order", _inspectionOrder); Grid.SetColumn(order, 1); grid.Children.Add(order);
        var shuffle = Button("New random sample", async () =>
        {
            _inspectionSeed = Random.Shared.NextInt64(long.MaxValue);
            _updatingFolders = true; _inspectionOrder.SelectedIndex = 2; _updatingFolders = false;
            await LoadCatalog(refreshFolders: false);
        });
        shuffle.VerticalAlignment = VerticalAlignment.Bottom; Grid.SetColumn(shuffle, 2); grid.Children.Add(shuffle);
        _firstPage = SmallButton("First", async () => await ChangePage(0));
        _previousPage = SmallButton("← Page", async () => await ChangePage(Math.Max(0, _catalogOffset - (int)_reviewLimit.Value!)));
        _nextPage = SmallButton("Page →", async () => await ChangePage(_catalogOffset + (int)_reviewLimit.Value!));
        _lastPage = SmallButton("Last", async () => await ChangePage(Math.Max(0, (_catalogTotal - 1) / (int)_reviewLimit.Value! * (int)_reviewLimit.Value!)));
        _pageJump.Width = 150;
        var paging = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var control in new Control[] { _firstPage, _lastPage, Text("Page"), _pageJump,
                     Button("Go", async () => await ChangePage(((int)_pageJump.Value! - 1) * (int)_reviewLimit.Value!)) })
        { control.VerticalAlignment = VerticalAlignment.Center; paging.Children.Add(control); }
        _reviewLimit.Width = 125;
        paging.Children.Add(Text("Per page")); paging.Children.Add(_reviewLimit);
        return Card(Stack(8, grid, Field("Game recognition", _inspectionRecognition, "Game AI outcome; older saved records are read without changing them."), paging), 10);
    }

    private async Task ChangePage(int offset)
    {
        var size = (int)_reviewLimit.Value!;
        _catalogOffset = Math.Clamp(offset, 0, Math.Max(0, (_catalogTotal - 1) / size * size));
        await LoadCatalog(refreshFolders: false, resetPage: false);
    }

    private async Task LoadCatalog(bool announce = true, bool refreshFolders = true, bool resetPage = true)
    {
        if (resetPage) _catalogOffset = 0;
        var root = _dataset.Text?.Trim() ?? "";
        if(File.Exists(Path.Combine(root,"encoding.json")))
        {
            try
            {
                using var saved=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"encoding.json")));
                if(saved.RootElement.GetProperty("schema").GetString()=="gojo.paired-joint-encoding.v1")
                {
                    var source=saved.RootElement.GetProperty("source").GetString()!;
                    if(Path.GetFullPath(source).Equals(Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("An encoding run cannot be its own source drawings folder.");
                    _dataset.Text=source;
                    await LoadCatalog(announce:false);await LoadEncodingRun(root);return;
                }
            }
            catch(Exception error){SetStatus("Encoding run could not be opened",error.Message,true);return;}
        }
        // TextChanged can be delivered after a picker has already requested
        // this catalog. Do not cancel that same request as a deferred text edit.
        _observedDatasetPath = root;
        var revision = ++_catalogRevision;
        _catalogCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _catalogCancellation = cancellation;
        try
        {
            if (announce) SetStatus("Loading drawings…", "Discovering generated folders and selecting drawings from their saved journals.");
            var savedPipeline = Workspace.PipelineManifest(root);
            if (savedPipeline is { } request) _analysisCount.Value = request.GetProperty("request").GetProperty("count").GetInt32();
            if (refreshFolders || _folderScope.SelectedItem is null)
            {
                var folders = await Task.Run(() => Workspace.GeneratedFolders(root));
                if (revision != _catalogRevision || _dataset.Text?.Trim() != root) return;
                var previous = (_folderScope.SelectedItem as FolderOption)?.Path;
                var choices = new[] { new FolderOption($"All categories · {folders.Count} generated " + (folders.Count == 1 ? "folder" : "folders"), root) }.Concat(folders).ToArray();
                _updatingFolders = true;
                _folderScope.ItemsSource = choices;
                _folderScope.SelectedItem = choices.FirstOrDefault(folder => folder.Path == previous) ?? choices[0];
                _updatingFolders = false;
                if (folders.Count == 0)
                {
                    _loadedDataset = null; _drawings.ItemsSource = Array.Empty<DrawingRow>();
                    _gallery.Children.Clear();
                    _catalogTotal = 0; _pageLabel.Text = "No drawings";
                    _firstPage.IsEnabled = _previousPage.IsEnabled = _nextPage.IsEnabled = _lastPage.IsEnabled = false;
                    _datasetSummary.Text = savedPipeline is not null ? "This pipeline has a saved selection but no completed records yet. Use Complete pipeline to resume."
                        : "No converted drawings in this folder yet. Use Convert drawings to create a dataset here.";
                    if (announce) SetStatus("No drawings here yet", _datasetSummary.Text);
                    return;
                }
            }
            var scope = InspectionPath; var size = (int)_reviewLimit.Value!; var order = InspectionOrder; var seed = _inspectionSeed;
            var recognition = InspectionRecognition;
            var catalog = await Task.Run(() => _browser.Read(scope, size, _catalogOffset, order, seed, cancellation.Token, recognition), cancellation.Token);
            if (revision != _catalogRevision || _dataset.Text?.Trim() != root) return;
            _catalogOffset = catalog.Offset;
            _catalogTotal = catalog.Stored;
            var pageCount = Math.Max(1, (catalog.Stored + catalog.PageSize - 1) / catalog.PageSize);
            _pageJump.Maximum = pageCount; _pageJump.Value = catalog.Offset / catalog.PageSize + 1;
            _pageLabel.Text = $"Page {catalog.Offset / catalog.PageSize + 1:N0} of {pageCount:N0} · {catalog.Drawings.Count:N0} drawings on this page";
            _firstPage.IsEnabled = _previousPage.IsEnabled = catalog.Offset > 0;
            _nextPage.IsEnabled = _lastPage.IsEnabled = catalog.Offset + catalog.Drawings.Count < catalog.Stored;
            var key = (_drawings.SelectedItem as DrawingRow)?.QualifiedId;
            _loadedDataset = root; _drawings.ItemsSource = catalog.Drawings;
            _drawings.SelectedItem = catalog.Drawings.FirstOrDefault(row => row.QualifiedId == key) ?? catalog.Drawings.FirstOrDefault();
            _datasetSummary.Text = $"{catalog.Stored:N0} drawings in scope · {catalog.Errors:N0} conversion errors · showing {catalog.Offset + (catalog.Drawings.Count > 0 ? 1 : 0):N0}–{catalog.Offset + catalog.Drawings.Count:N0}";
            if (catalog.Warnings?.Count > 0) _datasetSummary.Text += " · " + string.Join(" ", catalog.Warnings);
            if (scope == root && savedPipeline is { } pipeline)
                _analysisCount.Value = pipeline.GetProperty("request").GetProperty("count").GetInt32();
            else if (catalog.Stored > 0) _analysisCount.Value = Math.Min(_analysisCount.Value ?? 1000, catalog.Stored);
            await LoadGallery(catalog.Drawings, revision, cancellation.Token);
            var inspector = Path.Combine(Workspace.InspectorFolder(InspectionPath), "evidence.html");
            if (_lastArtifact is null && File.Exists(inspector))
            { _lastArtifact = inspector; _openResult.IsEnabled = _cancellation is null; }
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
            if (announce) SetStatus("Dataset open", "Click a drawing to enlarge it. Choose a result view or move between pages. No review file is needed.");
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (revision != _catalogRevision) return;
            _gallery.Children.Clear(); _galleryCards.Clear();
            _pageLabel.Text = "Dataset could not be read";
            if (announce) SetStatus("Could not load generated drawings", error.Message, true); else AppendLog("Could not reload drawings: " + error.Message);
        }
        finally { if (ReferenceEquals(_catalogCancellation, cancellation)) _catalogCancellation = null; }
    }
}
