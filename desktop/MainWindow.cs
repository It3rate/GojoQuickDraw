using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace GojoQuickDraw.Desktop;

internal sealed partial class MainWindow : Window
{
    private static readonly IBrush Ink = Brush.Parse("#183D43"), Teal = Brush.Parse("#247D72"), Muted = Brush.Parse("#647A79");
    private readonly string _repository = WorkspaceSettings.Repository();
    private readonly WorkspaceSettings _settings;
    private readonly TextBox _input = new(), _dataset = new(), _python = new(), _gojo = new();
    private readonly Button _category;
    private readonly ComboBox _recognition = new() { ItemsSource = Recognition.Labels, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly NumericUpDown _limit = Number(25, 1, 5000000), _reviewLimit = Number(12, 1, 48), _stageCount = Number(5, 1, 1000),
        _fit = Number(6, .1m, 100), _maxPoints = Number(20000, 1, 10000000), _maxSpans = Number(256, 1, 100000);
    private readonly CheckBox _recursive = new() { Content = "Include source subfolders" }, _retry = new() { Content = "Retry retained errors" },
        _verifyAll = new() { Content = "Verify the entire dataset", IsChecked = false },
        _autoOpen = new() { Content = "Open exported reviews when ready", IsChecked = false };
    private readonly CheckBox _analysisRetry = new() { Content = "Retry retained analysis errors" };
    private readonly NumericUpDown _analysisCount = Number(1000, 1, 5000000), _analysisWorkers = Number(4, 1, 8);
    private readonly TextBox _analysisOutput = new() { Watermark = "Automatic: separate folder beside the selected collection" };
    private readonly TextBlock _datasetSummary = Text("Open a generated dataset folder to view it, or create one with Convert drawings."),
        _status = Text("Ready", 15, true), _statusDetail = Text("Choose a dataset folder to begin."),
        _selection = Text("Select a drawing to preview its acquired strokes and fitted intervals."),
        _probeSummary = Text("Machine probes compare reflection, repetition, and alternative containment responses.");
    private readonly TextBox _log = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, MaxHeight = 230, FontSize = 12 };
    private readonly ListBox _drawings = new() { MinHeight = 170, MaxHeight = 230 };
    private readonly DrawingCanvas _raw = new(), _fitted = new() { Fitted = true };
    private readonly ComboBox _stageSelection = new() { ItemsSource = new[] { "Selected drawing", "First drawings in dataset" }, SelectedIndex = 0, MinWidth = 240 };
    private readonly ProgressBar _progress = new() { IsVisible = false, IsIndeterminate = true, Height = 4 };
    private readonly Button _stop, _openResult, _openFolder;
    private readonly TabControl _tabs = new();
    private readonly StackPanel _probeRows = new() { Spacing = 10 };
    private Control _configuration = null!;
    private Border _datasetDrawer = null!, _runDrawer = null!;
    private Button _datasetToggle = null!;
    private CancellationTokenSource? _cancellation;
    private Task _activeWork = Task.CompletedTask;
    private bool _allowClose, _smoke, _startupSmoke;
    private string? _lastArtifact, _loadedDataset, _observedDatasetPath;
    private int _previewRevision;
    internal readonly List<JobResult> Results = [];

    public MainWindow()
    {
        _settings = WorkspaceSettings.Load(_repository);
        _startupSmoke = Program.Arguments.Contains("--startup-smoke");
        _smoke = Program.Arguments.Contains("--smoke") || _startupSmoke;
        Title = "Gojo QuickDraw Studio";
        Width = 1180; Height = 980; MinWidth = 900; MinHeight = 740;
        Background = Brush.Parse("#F1F5F0");
        _statusDetail.MaxLines = 3; _statusDetail.TextTrimming = TextTrimming.WordEllipsis;
        _category = Button("Choose categories…", async () => await ChooseCategories());
        _category.HorizontalAlignment = HorizontalAlignment.Stretch;
        SetSourceCategories(_settings.Category.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        _input.Text = _settings.Input; _dataset.Text = _settings.Dataset;
        var datasetArgument = Array.IndexOf(Program.Arguments, "--dataset");
        if (datasetArgument >= 0 && datasetArgument + 1 < Program.Arguments.Length) _dataset.Text = Program.Arguments[datasetArgument + 1].Trim();
        _observedDatasetPath = _dataset.Text?.Trim();
        _python.Text = _settings.Python; _gojo.Text = _settings.Gojo; _limit.Value = Math.Max(1, _settings.Limit);
        _recognition.SelectedIndex = Math.Max(0, Array.IndexOf(Recognition.Modes, _settings.Recognition));
        _stop = Button("Stop", () => { _cancellation?.Cancel(); SetStatus("Stopping…", "Completed records remain available. Conversion can be resumed."); });
        _stop.IsEnabled = false;
        _openResult = Button("Open exported review", () => Open(_lastArtifact)); _openResult.IsEnabled = false;
        _openFolder = Button("Open dataset folder", () => Open(_dataset.Text));
        _drawings.SelectionChanged += async (_, _) => await LoadPreview();
        _input.TextChanged += (_, _) =>
        {
            if (File.Exists(_input.Text) && CategoryCatalog.FromFile(_input.Text!) is { } category) SetSourceCategories([category]);
        };
        _dataset.TextChanged += (_, _) =>
        {
            var current = _dataset.Text?.Trim();
            if (current == _observedDatasetPath) return;
            _observedDatasetPath = current;
            UpdateDatasetCaption();
            ++_catalogRevision; ++_previewRevision;
            _catalogCancellation?.Cancel();
            _updatingFolders = true; _folderScope.ItemsSource = Array.Empty<FolderOption>(); _updatingFolders = false;
            _loadedDataset = null; _drawings.ItemsSource = Array.Empty<DrawingRow>();
            _raw.Profiles = []; _fitted.Profiles = [];
            _pagePreviews.Clear(); _galleryCards.Clear(); _gallery.Children.Clear();
            _lastArtifact = null; _openResult.IsEnabled = false;
            _datasetSummary.Text = "Load this dataset to see its drawings and recorded settings.";
        };
        var datasetRow = Row(_dataset, Button("Open folder…", async () => await PickDataset()),
            Button("Load", async () => { _tabs.SelectedIndex = 0; await LoadCatalog(); }), Button("New dataset…", async () => await NewDataset()));
        _dataset.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { _tabs.SelectedIndex = 0; await LoadCatalog(); } };
        _datasetDrawer = Card(Stack(7, Text("Dataset folder — viewer and converter", 13, true), datasetRow, _datasetSummary,
            Buttons(_openFolder, SmallButton("Hide", () => _datasetDrawer.IsVisible = false))), 12);
        _datasetDrawer.IsVisible = false;
        _datasetDrawer.Margin = new Thickness(16, 0, 16, 8);
        _tabs.ItemsSource = new[]
        {
            new TabItem { Header = Text("View drawings", 15, true), Content = Scroll(EvidencePage()) },
            new TabItem { Header = Text("Convert drawings", 15, true), Content = Scroll(ConvertPage()) },
            new TabItem { Header = Text("Train model", 15, true), Content = Scroll(TrainingPage()) },
            new TabItem { Header = Text("Encoding", 15, true), Content = Scroll(EncodingPage()) }
        };
        _tabs.Margin = new Thickness(16, 0, 16, 6);
        _tabs.VerticalContentAlignment = VerticalAlignment.Stretch;
        _tabs.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _log.IsVisible = true;
        _runDrawer = Card(Stack(5, _statusDetail, _log), 10);
        _runDrawer.IsVisible = false; _runDrawer.Margin = new Thickness(16, 0, 16, 6);
        _datasetToggle = SmallButton("Dataset…", () => _datasetDrawer.IsVisible = !_datasetDrawer.IsVisible);
        _datasetToggle.MaxWidth = 290; UpdateDatasetCaption();
        _status.FontSize = 12; _status.MaxWidth = 170; _status.MaxLines = 1;
        _stop.Padding = new Thickness(9, 5);
        var commands = Buttons(_datasetToggle, _status, _stop, SmallButton("Run details", () => _runDrawer.IsVisible = !_runDrawer.IsVisible));
        foreach (var child in commands.Children) child.VerticalAlignment = VerticalAlignment.Center;
        var header = Row(Text("QuickDraw Studio", 20, true), commands);
        header.Margin = new Thickness(16, 9, 16, 7);
        // Drawers take space only while open. The viewport owns the remaining
        // height; no fixed footer can cover drawings or controls.
        var main = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*") };
        main.Children.Add(header); Grid.SetRow(_runDrawer, 1); main.Children.Add(_runDrawer);
        Grid.SetRow(_progress, 2); main.Children.Add(_progress);
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        body.Children.Add(_datasetDrawer); Grid.SetRow(_tabs, 1); body.Children.Add(_tabs);
        _configuration = body; Grid.SetRow(body, 3); main.Children.Add(body); Content = main;
        InitializeSelectionControls();
        AttachViewerKeys(this);
        Closing += async (_, args) =>
        {
            _catalogCancellation?.Cancel();
            if (_allowClose || _cancellation is null) { SaveSettings(); return; }
            args.Cancel = true; _cancellation.Cancel();
            SetStatus("Stopping before closing…", "Waiting for the active worker to finish stopping.");
            await _activeWork; _allowClose = true; Close();
        };
        Opened += async (_, _) =>
        {
            if (_startupSmoke) { await RunStartupSmoke(); return; }
            if (_smoke) { await RunSmoke(); return; }
            if (Directory.Exists(_dataset.Text)) await LoadCatalog(); else _tabs.SelectedIndex = 1;
            OpenRequestedTrainingRun();
            await OpenRequestedEncodingRun();
        };
    }

    private Control ConvertPage()
    {
        var source = Row(_input, Button("File…", async () => await PickInput(false)), Button("Folder…", async () => await PickInput(true)));
        var options = new Grid { ColumnDefinitions = new ColumnDefinitions("*,180"), ColumnSpacing = 18 };
        options.Children.Add(Field("Source categories", _category, "Choose categories with toggle buttons, Select all, and Clear all."));
        var count = Field("Target drawing total", _limit, "Across selected categories. Existing drawings count toward this total."); Grid.SetColumn(count, 1); options.Children.Add(count);
        var advanced = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 14 };
        advanced.Children.Add(Field("Fitting tolerance", _fit));
        var points = Field("Maximum points", _maxPoints); Grid.SetColumn(points, 1); advanced.Children.Add(points);
        var spans = Field("Maximum intervals", _maxSpans); Grid.SetColumn(spans, 2); advanced.Children.Add(spans);
        var convert = JobButton("Convert drawings", [PipelineAction.Convert], true);
        var summary = Text("", 14, true);
        void UpdateRequest()
        {
            convert.Content = $"Create / extend to {_limit.Value:N0} drawings";
            var categories = _sourceCategories.Count == 1 ? "1 category" : $"{_sourceCategories.Count:N0} categories";
            summary.Text = $"Target: {_limit.Value:N0} matching drawings across {categories}. Keep saved drawings; run new ones through every available stage.";
        }
        _limit.ValueChanged += (_, _) => UpdateRequest(); _category.PropertyChanged += (_, e) => { if (e.Property == ContentControl.ContentProperty) UpdateRequest(); };
        UpdateRequest();
        return Stack(14, Text("Create or extend a dataset", 21, true),
            Text("Choose source files, categories and a target total. Raise the total to add drawings to the current dataset. Existing drawings are kept."),
            Card(Stack(12, Field("QuickDraw source folder or file", source, "Use the folder containing the category NDJSON / JSONL files, including gzip files."),
                options, Field("Game recognition", _recognition, "The game's AI outcome, not human review. The target counts matching drawings; other saved drawings are kept."), summary, convert,
                Text("For example: 100 saved + a target of 300 adds up to 200 new drawings. A lower target deletes nothing. Use New dataset for a separate collection; interrupted runs resume.", 12),
                new Expander { Header = "Advanced conversion options", Content = Stack(12, _recursive, advanced,
                    Field("Sampling seed (optional)", _samplingSeed, "Leave blank for a new random selection; interrupted selections resume with the same settings."), _retry), HorizontalContentAlignment = HorizontalAlignment.Stretch })),
            Text("Every stage is saved: original → fitted outlines → long strokes → primitive shapes → joint and shape adjustments → reconstructed shapes. Full shape interpretation currently applies to churches; other categories retain their original and fitted evidence.", 13),
            new Expander { Header = "Tool settings", Content = SetupPage(), HorizontalContentAlignment = HorizontalAlignment.Stretch });
    }

    private Control EvidencePage() => BrowserPage();

    private Control StagesPage() => Stack(14, Text("Draw through successive interpretations", 21, true),
        Text("Generate the existing church passes from fitted source through simplification, joint merging, and synthesis."),
        Card(Stack(12, Field("Drawings to use", _stageSelection), Field("Number of drawings when using the first records", _stageCount),
            Text("Click a drawing in View drawings to select it. Generated steps retain their source correspondence."))),
        Buttons(JobButton("Generate drawing steps", [PipelineAction.Stages], true)),
        Text("These passes use authored church expectations. Unusual drawings can produce poor interpretations; review warnings and the resulting geometry.", 13));

    private Control ProbesPage() => Stack(14, Text("Test how a proposed machine responds", 21, true),
        Text("Compare changing spacing, preserving a mirror, and moving a part or its enclosing wall."),
        Card(Stack(12, Text("Selected drawing", 14, true),
            Text("Uses the drawing most recently selected in the viewer. Some drawings have no supported pair or containment candidates yet."),
            Buttons(JobButton("Probe selected drawing", [PipelineAction.Probe], true)),
            Text("Or start with the authored two-window and door example."),
            Buttons(JobButton("Run window example", [PipelineAction.ExampleProbe])))), _probeSummary, _probeRows,
        Text("Matching outcomes can leave multiple machines plausible. Probe results report those alternatives and their remaining discrepancies.", 12));

    private Control SetupPage() => Stack(14, Text("Local tools and workspace", 21, true),
        Text("The desktop app runs the same Python pipeline and linked native Gojo bridge as the command line."),
        Card(Stack(14, Field("Python executable", Row(_python, Button("Browse…", async () => await PickPython()))),
            Field("Linked Gojo checkout", Row(_gojo, Button("Browse…", async () => await PickGojo()))),
            Text("A Python 3.10+ installation and the .NET 10 SDK are required. Check setup builds the native bridge and checks the linked revision.", 12))),
        Buttons(JobButton("Check setup", [PipelineAction.Setup], true), Button("Open project folder", () => Open(_repository))));

    private PipelineOptions Options(PipelineAction action, string run)
    {
        var limit = action == PipelineAction.Refine ? (int)_refinementCount.Value! : action is PipelineAction.Catalog or PipelineAction.Review or PipelineAction.Refresh or PipelineAction.Verify ? (int)_reviewLimit.Value! : action == PipelineAction.Analyze ? (int)_analysisCount.Value! : action == PipelineAction.Train ? (int)_trainingCount.Value! : action == PipelineAction.Encode ? (int)_encodingCount.Value! : action == PipelineAction.Stages ? (int)_stageCount.Value! : (int)_limit.Value!;
        var selected = _drawings.SelectedItem as DrawingRow;
        var target = action is PipelineAction.Convert or PipelineAction.Setup ? _dataset.Text?.Trim() ?? "" : InspectionPath;
        if ((action == PipelineAction.Probe || action == PipelineAction.Stages && _stageSelection.SelectedIndex == 0) && selected is not null)
            target = selected.DatasetPath;
        long? sampleSeed = null;
        if (action == PipelineAction.Convert && !string.IsNullOrWhiteSpace(_samplingSeed.Text))
            sampleSeed = long.TryParse(_samplingSeed.Text, out var seed) ? seed : throw new ArgumentException("Sampling seed must be a whole number, or blank for random selection.");
        if (action is PipelineAction.Review or PipelineAction.Gallery or PipelineAction.Refresh) run = Workspace.InspectorFolder(target);
        return new(_repository, _python.Text?.Trim() ?? "", _gojo.Text?.Trim() ?? "", _input.Text?.Trim() ?? "", target,
            string.Join(',', _sourceCategories.Order(StringComparer.OrdinalIgnoreCase)), limit, action == PipelineAction.Verify && _verifyAll.IsChecked == true,
            _recursive.IsChecked == true, action == PipelineAction.Analyze ? _analysisRetry.IsChecked == true : _retry.IsChecked == true, (double)(_fit.Value ?? 6), (int)(_maxPoints.Value ?? 20000), (int)(_maxSpans.Value ?? 256),
            action != PipelineAction.Stages || _stageSelection.SelectedIndex == 0 ? selected?.KeyId : null, run,
            InspectionOrder, _inspectionSeed, sampleSeed, _catalogOffset, (int)_analysisWorkers.Value!,
            string.IsNullOrWhiteSpace(_analysisOutput.Text) ? null : _analysisOutput.Text.Trim(),
            action is PipelineAction.Analyze or PipelineAction.Catalog or PipelineAction.Train or PipelineAction.Encode ? InspectionRecognition : Recognition.Modes[Math.Max(0, _recognition.SelectedIndex)],
            (int)_trainingSynthetic.Value!, (int)_trainingEpochs.Value!, (long)_trainingSeed.Value!,
            string.IsNullOrWhiteSpace(_trainingOutput.Text) ? Workspace.ModelFolder(_dataset.Text ?? "") : _trainingOutput.Text.Trim(),
            _encodingMode.SelectedIndex==0 ? [_encodingNeighborhoodModel.Text?.Trim() ?? ""] : _encodingUseModel.IsChecked==true ? [_encodingModel.Text?.Trim() ?? ""] : [],
            string.IsNullOrWhiteSpace(_encodingOutput.Text) ? null : _encodingOutput.Text.Trim(), (int)_encodingPatches.Value!,
            _trainingMode.SelectedIndex==0,_encodingMode.SelectedIndex==0,_encodingUseModel.IsChecked==true,_encodingUseLine.IsChecked==true,(int)_encodingPasses.Value!,_encodingUseClosure.IsChecked==true,
            string.IsNullOrWhiteSpace(_refinementOutput.Text) ? null : _refinementOutput.Text.Trim(), (int)_refinementPasses.Value!, _refinementHistory.IsChecked==true);
    }

    private Task Execute(params PipelineAction[] actions) => _activeWork = Workflow(actions);

    private async Task Workflow(PipelineAction[] actions)
    {
        if (_cancellation is not null) return;
        _catalogCancellation?.Cancel();
        _cancellation = new();
        _configuration.IsEnabled = false; _stop.IsEnabled = true; _openResult.IsEnabled = false; _progress.IsVisible = true;
        _log.Text = "";
        var retained = false;
        string? refreshedDataset = null;
        try
        {
            SaveSettings();
            var run = actions.All(action => action == PipelineAction.Setup) ? Path.GetTempPath() : Workspace.RunFolder(_dataset.Text ?? "");
            foreach (var action in actions)
            {
                var options = Options(action, run);
                if (action == PipelineAction.Refresh && refreshedDataset is not null)
                    options = options with { Dataset = refreshedDataset, RunFolder = Workspace.InspectorFolder(refreshedDataset) };
                var plan = Pipeline.Plan(action, options);
                if (action == PipelineAction.Refine && plan.Artifact is { } refinementPage)
                    _refinementOutput.Text = Path.GetDirectoryName(refinementPage);
                SetStatus(TitleFor(action), "Working locally. You can stop this run and resume conversion later.");
                AppendLog($"{DateTime.Now:HH:mm:ss} · {TitleFor(action)}");
                var result = await Pipeline.Run(plan, AppendLog, _cancellation.Token);
                Results.Add(result);
                if (result.Cancelled)
                {
                    if (action == PipelineAction.Analyze && plan.Artifact is { } partial)
                    {
                        var target = Path.GetDirectoryName(partial)!;
                        if (Workspace.GeneratedFolders(target).Count > 0) _dataset.Text = target;
                    }
                    SetStatus("Stopped", "Completed drawings are available in View drawings. No further operation was started."); break;
                }
                if (!result.Successful && !result.RetainedErrors)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? "The worker did not return a successful result. See run details." : result.Error.Trim());
                retained |= result.RetainedErrors;
                if (action == PipelineAction.Convert) refreshedDataset = options.Dataset;
                if (action == PipelineAction.Analyze && result.Data is { } analyzed && analyzed.TryGetProperty("output", out var analyzedPath))
                    _dataset.Text = refreshedDataset = analyzedPath.GetString();
                if (action != PipelineAction.Analyze && plan.Artifact is { } artifact && File.Exists(artifact))
                {
                    _lastArtifact = artifact;
                    if ((action == PipelineAction.RefineReview || action is PipelineAction.Refine or PipelineAction.Review or PipelineAction.Stages or PipelineAction.Gallery or PipelineAction.Refresh && _autoOpen.IsChecked == true) && !_smoke
                        && !(action == PipelineAction.Review && actions.Contains(PipelineAction.Gallery))) Open(artifact);
                }
                SetStatus(result.RetainedErrors ? "Converted with retained errors" : "Completed", Describe(action, result));
                if (action == PipelineAction.Train) ShowTraining(result);
                if (action == PipelineAction.Encode && result.Data is { } encoded) await LoadEncodingRun(encoded.GetProperty("output").GetString()!);
                if (action == PipelineAction.Refine && result.Data is { } refined)
                    _refinementOutput.Text = refined.GetProperty("output").GetString();
                if (action is PipelineAction.Probe or PipelineAction.ExampleProbe) ShowProbes(result);
            }
            if (retained) SetStatus("Finished with records to review", "Successful records were processed. Failed sources remain in the dataset's errors folder; see run details.", true);
            SaveSettings();
        }
        catch (OperationCanceledException) { SetStatus("Stopped", "No additional operation was started."); }
        catch (Exception error) { SetStatus("Run needs attention", error.Message, true); AppendLog(error.ToString()); }
        finally
        {
            _cancellation.Dispose(); _cancellation = null; _configuration.IsEnabled = true;
            _stop.IsEnabled = false; _progress.IsVisible = false; _openResult.IsEnabled = _lastArtifact is not null;
            if (Directory.Exists(_dataset.Text)) await LoadCatalog(false);
            if (actions.Any(action => action is PipelineAction.Convert or PipelineAction.Analyze)) _tabs.SelectedIndex = 0;
        }
    }

    private static string TitleFor(PipelineAction action) => action switch
    {
        PipelineAction.Refine => "Settling connected church shapes…",
        PipelineAction.RefineReview => "Opening saved connected results…",
        PipelineAction.Convert => "Converting through every drawing stage…", PipelineAction.Verify => "Verifying native archives…",
        PipelineAction.Review => "Building the evidence review…", PipelineAction.Stages => "Generating drawing steps…",
        PipelineAction.Gallery => "Building input / final comparisons…",
        PipelineAction.Refresh => "Refreshing the inspector and comparison gallery…",
        PipelineAction.Analyze => "Completing the drawing pipeline…",
        PipelineAction.Train => "Training the selected local models…",
        PipelineAction.Encode => "Applying selected evidence providers…",
        PipelineAction.Setup => "Checking local tools…", _ => "Comparing machine responses…"
    };

    private static string Describe(PipelineAction action, JobResult result)
    {
        if (result.Data is not { } data) return "The operation finished. See run details.";
        int Count(string field) => data.TryGetProperty(field, out var value) ? value.GetInt32() : 0;
        return action switch
        {
            PipelineAction.Refine => $"{Count("completed")} churches refined · {Count("resumed")} reused · {Count("errors")} errors. The original ten stages are unchanged; open the comparison to inspect connected passes.",
            PipelineAction.RefineReview => $"Comparison refreshed from {Count("completed")} completed churches. No settlement was run.",
            PipelineAction.Train => $"Trained {Count("parameterCount")} weights from {Count("sourceDrawings")} source drawings and controlled constructions. Open the model review for held-out results.",
            PipelineAction.Encode => $"{Count("sourceDrawings")} paired drawings · {Count("localPatches")} local opinions · {Count("changedPatches")} different / additional patch opinions. Inspect alternatives in Encoding.",
            PipelineAction.Convert => $"{Count("converted")} converted · {Count("resumed")} reused · {Count("errors")} retained errors · {Count("storedRecords")} stored drawings"
                + (data.TryGetProperty("recognition", out var filter) && filter.GetString() != "all" ? $" · {Count("matchingAcquisitions")} match the recognition filter" : "")
                + (Count("selectionShortfall") > 0 ? $" · source has {Count("selectionShortfall")} fewer eligible drawings than the target" : ""),
            PipelineAction.Verify => $"{Count("records")} drawings independently restored; artifact hashes checked.",
            PipelineAction.Review => $"Visual review ready for {Count("drawings")} drawings across {Math.Max(1, Count("pages"))} page(s). Use Open last result to inspect it.",
            PipelineAction.Gallery => $"Comparison gallery ready: {Count("drawings")} drawings across {Count("pages")} pages, 12 pairs per page. Use Open last result to inspect it.",
            PipelineAction.Refresh => $"Inspector and comparison gallery updated for {Count("drawings")} drawings. Reload an already open browser tab to see the latest results.",
            PipelineAction.Analyze => $"{Count("converted")} analyzed · {Count("resumed")} reused · {Count("storedErrors")} retained errors. Source collection preserved.",
            PipelineAction.Stages => $"{Count("passes")} steps generated for {Count("drawings")} drawings." +
                (data.TryGetProperty("warnings", out var warnings) && warnings.GetArrayLength() > 0 ? $" {warnings.GetArrayLength()} viewing-frame warning(s); inspect the review." : " Inspect the proposed interpretations in the review."),
            PipelineAction.Probe or PipelineAction.ExampleProbe => $"{(data.TryGetProperty("probes", out var probes) ? probes.GetArrayLength() : 0)} intervention groups compared. Open last result for the full response report. Equivalent responses remain tied.",
            PipelineAction.Setup => data.GetProperty("upstream").GetProperty("matchesPinnedRevision").GetBoolean()
                ? "Python and the native bridge are ready. The linked Gojo revision matches the tested dependency."
                : "Tools are ready. The linked Gojo revision differs from the tested dependency; its actual revision is recorded in each run.",
            _ => "Completed."
        };
    }

    private void ShowProbes(JobResult result)
    {
        _probeRows.Children.Clear();
        _probeSummary.Text = Describe(PipelineAction.Probe, result);
        if (result.Data is not { } data || !data.TryGetProperty("probes", out var probes)) return;
        if (probes.GetArrayLength() == 0)
        {
            _probeSummary.Text = "No eligible placement or containment candidates were found for this drawing. The window example provides an authored control.";
            return;
        }
        if (data.TryGetProperty("containmentMovement", out var movement))
            foreach (var group in movement.EnumerateArray().Take(4))
            {
                var carried = group.GetProperty("moveContainer").GetProperty("movedParticipants").GetArrayLength() - 1;
                _probeRows.Children.Add(Card(Stack(6, Text("Container movement default", 14, true),
                    Text("Moving the selected child leaves its container fixed."),
                    Text($"Moving the container carries {carried} contained part(s), each once."),
                    Text("This is a revisable frame-coupling expectation. Existing gaps, containment discrepancy, or alternative parents remain available in the report.", 12))));
            }
        foreach (var probe in probes.EnumerateArray().Take(12))
        {
            var content = Stack(6, Text(probe.GetProperty("id").GetString()?.Replace('-', ' ') ?? "Machine comparison", 14, true));
            foreach (var variant in probe.GetProperty("variants").EnumerateArray().Take(8))
            {
                var name = variant.GetProperty("machine").GetString()?.Replace('-', ' ');
                var residue = variant.GetProperty("sumDistanceResidue").GetDouble();
                content.Children.Add(Text($"{name}  ·  remaining distance discrepancy {residue:0.##}"));
            }
            content.Children.Add(Text("Distances use this experiment's normalized page frame. A smaller discrepancy supports consistency under these constraints.", 12));
            _probeRows.Children.Add(Card(content));
        }
    }

    private async Task LoadPreview()
    {
        var revision = ++_previewRevision;
        if (_drawings.SelectedItem is not DrawingRow row || _loadedDataset is null) return;
        _selection.Text = $"{row.Category} · {row.KeyId} · {row.Samples} original samples · {row.Intervals} fitted intervals";
        try
        {
            var preview = await Task.Run(() => Workspace.ReadPreview(row.DatasetPath, row));
            if (_previewRevision != revision) return;
            _raw.Profiles = preview.Strokes; _fitted.Profiles = preview.Profiles;
        }
        catch (Exception error) { if (_previewRevision == revision) _selection.Text = "Preview unavailable: " + error.Message; }
    }

    private async Task PickInput(bool folder)
    {
        if (folder)
        {
            var chosen = await StorageProvider.OpenFolderPickerAsync(new() { Title = "QuickDraw input folder", AllowMultiple = false });
            if (chosen.Count > 0) _input.Text = chosen[0].TryGetLocalPath();
        }
        else
        {
            var chosen = await StorageProvider.OpenFilePickerAsync(new() { Title = "QuickDraw input file", AllowMultiple = false,
                FileTypeFilter = [new("QuickDraw records") { Patterns = ["*.ndjson", "*.jsonl", "*.gz"] }, FilePickerFileTypes.All] });
            if (chosen.Count > 0) _input.Text = chosen[0].TryGetLocalPath();
        }
    }

    private async Task PickDataset()
    {
        var chosen = await StorageProvider.OpenFolderPickerAsync(new() { Title = "Generated collection or category folder", AllowMultiple = false });
        if (chosen.Count > 0) { _dataset.Text = chosen[0].TryGetLocalPath(); _tabs.SelectedIndex = 0; await LoadCatalog(); }
    }
    private async Task PickGojo()
    {
        var chosen = await StorageProvider.OpenFolderPickerAsync(new() { Title = "Linked Gojo checkout", AllowMultiple = false });
        if (chosen.Count > 0) _gojo.Text = chosen[0].TryGetLocalPath();
    }
    private async Task PickPython()
    {
        var chosen = await StorageProvider.OpenFilePickerAsync(new() { Title = "Python executable", AllowMultiple = false });
        if (chosen.Count > 0) _python.Text = chosen[0].TryGetLocalPath();
    }
    private async Task NewDataset()
    {
        var chosen = await StorageProvider.OpenFolderPickerAsync(new() { Title = "Choose where to create the new dataset", AllowMultiple = false });
        if (chosen.Count == 0) return;
        _dataset.Text = WorkspaceSettings.NewDataset(chosen[0].TryGetLocalPath()!);
        _tabs.SelectedIndex = 1;
        SetStatus("New dataset selected", "You can edit its name in Dataset folder. Choose source categories and the total drawing count, then Convert.");
    }
    private void SaveSettings()
    {
        if (_smoke) return;
        _settings.Python = _python.Text ?? "python"; _settings.Gojo = _gojo.Text ?? ""; _settings.Input = _input.Text ?? "";
        _settings.Dataset = _dataset.Text ?? ""; _settings.Category = string.Join(',', _sourceCategories); _settings.Limit = (int)(_limit.Value ?? 25);
        _settings.SamplingSeed = _samplingSeed.Text ?? ""; _settings.InspectionOrder = InspectionOrder; _settings.InspectionSeed = _inspectionSeed;
        _settings.Recognition = Recognition.Modes[Math.Max(0, _recognition.SelectedIndex)];
        _settings.InspectionRecognition = InspectionRecognition;
        _settings.ResultView = _resultView.SelectedIndex;
        _settings.ResultStage = ( _resultView.SelectedItem as StageOption)?.Id ?? "best";
        _settings.EncodingModel = _encodingModel.Text ?? ""; _settings.EncodingUseModel = _encodingUseModel.IsChecked==true;
        _settings.NeighborhoodModel=_encodingNeighborhoodModel.Text ?? "";_settings.EncodingNeighborhoods=_encodingMode.SelectedIndex==0;_settings.EncodingUseLine=_encodingUseLine.IsChecked==true;
        _settings.EncodingPasses=(int)_encodingPasses.Value!;
        _settings.EncodingUseClosure=_encodingUseClosure.IsChecked==true;
        try { _settings.Save(); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { AppendLog("Workspace settings could not be saved: " + error.Message); }
    }
    private void Open(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception error) { SetStatus("Could not open result", error.Message, true); }
    }
    private void SetStatus(string title, string detail, bool error = false)
    {
        _status.Text = title; _status.Foreground = error ? Brush.Parse("#A14C30") : Ink;
        _statusDetail.Text = detail;
        ToolTip.SetTip(_status, detail);
        if (error && _runDrawer is not null) _runDrawer.IsVisible = true;
    }
    private void UpdateDatasetCaption()
    {
        if (_datasetToggle is null) return;
        var name = Path.GetFileName((_dataset.Text ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        _datasetToggle.Content = "Dataset: " + (string.IsNullOrEmpty(name) ? "choose…" : name) + " ▾";
        ToolTip.SetTip(_datasetToggle, _dataset.Text);
    }
    private void AppendLog(string line) => Dispatcher.UIThread.Post(() =>
    {
        var text = (_log.Text ?? "") + line + Environment.NewLine;
        _log.Text = text.Length > 60000 ? text[^60000..] : text;
        _log.CaretIndex = _log.Text.Length;
        if (_cancellation is not null && (line.StartsWith("Pipeline:") || line.StartsWith("Analysis ") || line.StartsWith("Indexing ") || line.StartsWith("Category ") || line.Contains(" selected: ") || line.StartsWith("Resuming ")))
            _statusDetail.Text = line;
    });

    private Button JobButton(string label, PipelineAction[]? actions, bool primary = false)
    {
        var button = Button(label, async () =>
        {
            if (actions is null) await LoadCatalog();
            else await Execute(actions);
        }, primary);
        AutomationProperties.SetAutomationId(button, actions is null ? "load-drawings" : string.Join("-", actions).ToLowerInvariant());
        return button;
    }
    private static Button Button(string label, Action action, bool primary = false)
    {
        var button = new Button { Content = label, Padding = new Thickness(15, 9), CornerRadius = new CornerRadius(6) };
        if (primary) { button.Background = Teal; button.Foreground = Brushes.White; }
        button.Click += (_, _) => action(); return button;
    }
    private static Button SmallButton(string label, Action action)
    {
        var button = Button(label, action); button.Padding = new Thickness(9, 5); button.FontSize = 13; return button;
    }
    private static NumericUpDown Number(decimal value, decimal minimum, decimal maximum) => new()
    { Value = value, Minimum = minimum, Maximum = maximum, Increment = minimum < 1 ? .5m : 1, FormatString = minimum < 1 ? "0.0" : "0", HorizontalAlignment = HorizontalAlignment.Stretch };
    private static TextBlock Text(string text, double size = 13, bool bold = false) => new()
    { Text = text, FontSize = size, FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal, Foreground = bold ? Ink : Muted, TextWrapping = TextWrapping.Wrap };
    private static StackPanel Stack(double spacing, params Control[] children)
    {
        var stack = new StackPanel { Spacing = spacing }; foreach (var child in children) stack.Children.Add(child); return stack;
    }
    private static StackPanel Buttons(params Control[] buttons)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 }; foreach (var button in buttons) panel.Children.Add(button); return panel;
    }
    private static Grid Row(Control field, params Control[] buttons)
    {
        var grid = new Grid { ColumnSpacing = 8 }; grid.ColumnDefinitions.Add(new(GridLength.Star)); grid.Children.Add(field);
        for (var i = 0; i < buttons.Length; i++) { grid.ColumnDefinitions.Add(new(GridLength.Auto)); Grid.SetColumn(buttons[i], i + 1); grid.Children.Add(buttons[i]); }
        return grid;
    }
    private static Control Field(string label, Control control, string? hint = null) => hint is null
        ? Stack(6, Text(label, 13, true), control) : Stack(6, Text(label, 13, true), control, Text(hint, 12));
    private static Border Card(Control content, double padding = 18) => new()
    { Background = Brushes.White, BorderBrush = Brush.Parse("#D7E2DB"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(padding), Child = content };
    private static ScrollViewer Scroll(Control content) => new()
    {
        // Padding on the viewer itself is not included in its content extent.
        // Keep it inside the measured content so the final control can scroll
        // fully into view, including its bottom margin.
        Content = new Border { Padding = new Thickness(0, 6, 6, 20), Child = content },
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
    };
}
