using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using GojoQuickDraw.Reads;

namespace GojoQuickDraw.Desktop;

internal sealed partial class MainWindow
{
    private sealed record StageOption(string Id, string Label) { public override string ToString() => Label; }
    private static readonly StageOption[] StageOptions = [new("best", "Latest available stage"),
        new("original", "1. Original drawing"), new("fitted", "2. Close fitted outlines"),
        new("outline-1", "3. Long strokes and curves"), new("outline-2", "4. Primitive shapes"),
        new("outline-3", "5. Balance related parts"), new("outline-4", "6. Merge nearby joints"),
        new("outline-5", "7. Regularize shapes"), new("outline-6", "8. Synthesize structure"),
        new("areas", "9. Shape areas and attachments"), new("cleanup", "10. Refined church shapes")];
    private readonly Grid _gallery = new() { ColumnSpacing = 12, RowSpacing = 12 };
    private readonly ComboBox _resultView = new()
    {
        ItemsSource = StageOptions, SelectedIndex = 0, MinWidth = 270
    };
    private readonly TextBlock _layerSummary = Text("Open a dataset folder to see original/result pairs.");
    private readonly Dictionary<string, Preview> _pagePreviews = [];
    private readonly Dictionary<string, string> _previewErrors = [];
    private readonly List<Control> _galleryCards = [];
    private Expander _analysisOptions = null!;
    private Control _inspectionDrawer = null!;
    private readonly CheckBox _showJoints = new() { Content = "Joints", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
    private readonly NumericUpDown _refinementCount = Number(12, 1, 10000), _refinementPasses = Number(8, 1, 16);
    private readonly TextBox _refinementOutput = new();

    private Control RefinementControls() => new Expander
    {
        Header = "Connected church refinement — compare the newer method",
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Content = Card(Stack(10,
            Text("Keep the original ten stages and add a separate comparison using connected joints, shapes, symmetry, CAD relations and residual steeple/cross recovery."),
            Text("Uses game-recognized churches from the selected folder, in the selected inspection order. No raw drawing conversion or new training. Native history can take about a minute per church and substantial disk space.", 12),
            Buttons(Field("Maximum churches", _refinementCount), Field("Settlement passes", _refinementPasses)),
            Field("Result folder (optional)", _refinementOutput, "Blank creates a new sibling result folder. Reuse the same path and settings to resume; choose a new folder after processing code changes."),
            Buttons(JobButton("Run connected refinement", [PipelineAction.Refine], true),
                JobButton("Open comparison", [PipelineAction.RefineReview]))))
    };

    private Control BrowserPage()
    {
        _resultView.SelectionChanged += (_, _) => RenderGallery();
        _showJoints.IsCheckedChanged += (_,_) => { RenderGallery();_updateDrawing?.Invoke(); };
        _gallery.SizeChanged += (_, _) => ArrangeGallery();
        var analyze = JobButton("Complete pipeline", [PipelineAction.Analyze], true);
        _analysisCount.ValueChanged += (_, _) => analyze.Content = $"Complete up to {_analysisCount.Value:N0} drawings";
        analyze.Content = $"Complete up to {_analysisCount.Value:N0} drawings";
        _analysisOptions = new Expander
        {
            Header = "Complete pipeline for existing drawings",
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = Card(Stack(10,
                Text("Add all missing drawing stages, shape areas and final refinement. Older datasets get a separate result folder. An interrupted pipeline dataset resumes its saved selection."),
                Field("Maximum drawings to complete", _analysisCount, "Counted from the start of the selected category folder. The count is always enforced."),
                analyze,
                new Expander { Header = "Analysis options", HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Content = Stack(10, Field("Completed dataset folder (optional)", _analysisOutput,
                        "Automatic: this folder's name followed by -processed. Use a new folder when rerunning changed processing code."),
                        Field("Parallel workers", _analysisWorkers), _analysisRetry) }))
        };
        var exports = Stack(10, Text("Optional checks and portable browser exports. Viewing drawings here does not require these."),
            Buttons(JobButton("Verify saved archives", [PipelineAction.Verify]), JobButton("Export browser inspector + gallery", [PipelineAction.Refresh])),
            _verifyAll, _autoOpen, _openResult);
        var expert = new Expander
        {
            Header = "Advanced tools", HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = Stack(14, exports,
                new Expander { Header = "Drawing steps — execute a church construction", HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = StagesPage() },
                new Expander { Header = "Machine probes — test placement and containment assumptions", HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = ProbesPage() },
                new Expander { Header = "Selected drawing for advanced tools", HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = Stack(8, _selection, _drawings) })
        };
        _inspectionDrawer = InspectionControls(); _inspectionDrawer.IsVisible = false;
        var paging = Buttons(_previousPage, _pageLabel, _nextPage,
            SmallButton("Filters & pages ▾", () => _inspectionDrawer.IsVisible = !_inspectionDrawer.IsVisible),
            SmallButton("Refresh", async () => await LoadCatalog()));
        foreach (var child in paging.Children) child.VerticalAlignment = VerticalAlignment.Center;
        _resultView.FontSize = 13;
        return Stack(8, Buttons(_resultView, SmallButton("◀", () => MoveStage(_resultView, -1)),
                SmallButton("▶", () => MoveStage(_resultView, 1)),_showJoints),
            paging, _inspectionDrawer, _gallery,
            Text("Click a pair to enlarge it. ← / → drawing · Shift + ← / → page · ↑ / ↓ stage. Gray dotted doors, windows and crosses are added expectations. Paging keeps the stage.", 12),
            _layerSummary, _analysisOptions, RefinementControls(), expert);
    }

    private async Task LoadGallery(IReadOnlyList<DrawingRow> rows, long revision, CancellationToken cancellation)
    {
        var previews = await Task.WhenAll(rows.Select(row => Task.Run(() =>
        {
            cancellation.ThrowIfCancellationRequested();
            try { return (Row: row, Preview: Workspace.ReadPreview(row.DatasetPath, row), Error: (string?)null); }
            catch (Exception error) when (error is IOException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException)
            { return (Row: row, Preview: (Preview?)null, Error: error.Message); }
        }, cancellation)));
        if (revision != _catalogRevision) return;
        _pagePreviews.Clear(); _previewErrors.Clear();
        foreach (var item in previews)
            if (item.Preview is not null) _pagePreviews[item.Row.QualifiedId] = item.Preview;
            else _previewErrors[item.Row.QualifiedId] = item.Error ?? "Saved preview unavailable.";
        RenderGallery();
    }

    private static void MoveStage(ComboBox selector, int direction)
    {
        selector.SelectedIndex = Math.Clamp(selector.SelectedIndex == 0 ? direction > 0 ? 1 : StageOptions.Length - 1 : selector.SelectedIndex + direction, 1, StageOptions.Length - 1);
    }

    private static PreviewStage Result(Preview preview, int mode)
    {
        var id = StageOptions[Math.Clamp(mode, 0, StageOptions.Length - 1)].Id;
        var available = preview.Stages ?? [];
        if (id == "best") return available.LastOrDefault() ?? new("fitted", "Close fitted outlines", preview.Profiles, []);
        return available.FirstOrDefault(s => s.Id == id) ?? new(id, "Stage not saved / not applicable", [], [],
            Note: "Use Complete pipeline for older converted drawings. The church-specific stages apply only to churches.");
    }

    private static double[] DisplayFrame(Preview preview, PreviewStage stage)
    {
        var points = preview.Strokes.SelectMany(s => s).Concat(preview.Profiles.SelectMany(s => s)).Concat(stage.Profiles.SelectMany(s => s))
            .Concat((stage.Residue ?? []).SelectMany(s => s)).Concat(stage.Parts.SelectMany(p => p.Points)).ToArray();
        if (points.Length == 0) return [-18, -18, 530, 530];
        return [Math.Min(0, points.Min(p => p[0])) - 18, Math.Min(0, points.Min(p => p[1])) - 18,
            Math.Max(512, points.Max(p => p[0])) + 18, Math.Max(512, points.Max(p => p[1])) + 18];
    }

    private Control Comparison(Preview preview, int mode, double height = 175)
    {
        var result = Result(preview, mode); var frame = DisplayFrame(preview, result);
        var read=preview.Joints;var show=_showJoints.IsChecked==true;
        var sourceJoints=show ? read?.Source.Nodes ?? [] : [];
        var resultJoints=!show ? [] : result.Id=="cleanup" ? read?.Final.Nodes ?? [] : result.Id is "fitted" or "original" ? sourceJoints : [];
        var original = new DrawingCanvas { Profiles = preview.Strokes, Frame = frame, Height = height,Joints=sourceJoints };
        var output = new DrawingCanvas { Fitted = result.Curves, Underlay = preview.Strokes, Residue = result.Residue ?? [], Parts = result.Parts, Frame = frame, Profiles = result.Profiles, Height = height,
            Joints=resultJoints,JointStatuses=result.Id=="cleanup" ? (read?.Matches ?? []).ToDictionary(m=>m.Target,m=>m.Status) : new Dictionary<string,string>() };
        void Highlight(DrawingCanvas canvas,IEnumerable<string> ids){canvas.Highlighted=ids.ToHashSet();canvas.InvalidateVisual();}
        original.JointHovered=node=>Highlight(output,(read?.Matches ?? []).Where(m=>node is not null && m.Candidates.Contains(node.Id)).Select(m=>m.Target));
        output.JointHovered=node=>Highlight(original,(read?.Matches ?? []).Where(m=>m.Target==node?.Id).SelectMany(m=>m.Candidates));
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 10 };
        grid.Children.Add(Stack(4, Text("Original", 12, true), original));
        var right = Stack(4, Text(result.Title, 12, true), output); Grid.SetColumn(right, 1); grid.Children.Add(right);
        return grid;
    }

    private void RenderGallery()
    {
        if (_gallery is null) return;
        _galleryCards.Clear();
        var rows = _drawings.Items.Cast<DrawingRow>().ToArray();
        foreach (var row in rows)
        {
            Control image = _pagePreviews.TryGetValue(row.QualifiedId, out var preview)
                ? Comparison(preview, _resultView.SelectedIndex)
                : Text(_previewErrors.GetValueOrDefault(row.QualifiedId, "Loading saved drawing…"));
            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12), Background = Avalonia.Media.Brushes.White,
                BorderBrush = Avalonia.Media.Brush.Parse("#CBDCD3"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Content = Stack(7, Text($"{_catalogOffset + Array.IndexOf(rows, row) + 1:N0}. {row.Category}", 14, true), image, Text(row.KeyId + " · click to enlarge", 11))
            };
            button.Click += async (_, _) => await ShowDrawing(row);
            _galleryCards.Add(button);
        }
        ArrangeGallery();
        MarkSelection();
        if (_pagePreviews.Count > 0)
        {
            var finals = _pagePreviews.Values.Count(p => p.Cleanup.Length > 0);
            _layerSummary.Text = finals > 0 ? $"{finals} of {_pagePreviews.Count} drawings on this page have refined shapes. Select a drawing stage above; paging keeps your choice."
                : "This page has converted stroke evidence. Complete pipeline adds and saves the church interpretation stages.";
        }
        else _layerSummary.Text = rows.Length == 0 ? "No drawings match these filters. Change Game recognition or the selected category to view other drawings."
            : "Saved previews are unavailable for this page; inspect the drawing's error details.";
    }

    private void ArrangeGallery()
    {
        var columns = _gallery.Bounds.Width is > 0 and < 1000 ? 2 : 3;
        _gallery.Children.Clear(); _gallery.ColumnDefinitions.Clear(); _gallery.RowDefinitions.Clear();
        for (var i = 0; i < columns; i++) _gallery.ColumnDefinitions.Add(new(GridLength.Star));
        for (var i = 0; i < (_galleryCards.Count + columns - 1) / columns; i++) _gallery.RowDefinitions.Add(new(GridLength.Auto));
        for (var i = 0; i < _galleryCards.Count; i++)
        { Grid.SetColumn(_galleryCards[i], i % columns); Grid.SetRow(_galleryCards[i], i / columns); _gallery.Children.Add(_galleryCards[i]); }
    }

    private async Task ShowDrawing(DrawingRow row)
    {
        _drawings.SelectedItem = row;
        if (!_pagePreviews.TryGetValue(row.QualifiedId, out var preview)) return;
        var window = new Window { Title = row.Category + " · " + row.KeyId, Width = 1080, Height = 780, MinWidth = 760, MinHeight = 580, Background = Avalonia.Media.Brush.Parse("#F1F5F0") };
        var view = new ComboBox { ItemsSource = _resultView.ItemsSource, SelectedIndex = _resultView.SelectedIndex, MinWidth = 235 };
        var picture = new ContentControl(); var stageNote = Text("");
        var joints=new CheckBox {Content="Joints",IsChecked=_showJoints.IsChecked,VerticalAlignment=VerticalAlignment.Center};
        joints.IsCheckedChanged+=(_,_)=>_showJoints.IsChecked=joints.IsChecked;
        var heading = Text("", 23, true); var counts = Text(""); var note = Text(""); var warnings = Text(""); var position = Text("");
        void Update()
        {
            if (_drawings.SelectedItem is not DrawingRow current || !_pagePreviews.TryGetValue(current.QualifiedId, out var currentPreview)) return;
            row = current; preview = currentPreview;
            heading.Text = window.Title = row.Category + " · " + row.KeyId;
            counts.Text = $"{row.Strokes} strokes · {row.Samples} acquired samples · {row.Intervals} fitted intervals";
            note.Text = preview.Cleanup.Length > 0 ? "Proposed shapes are saved authored interpretations. Gray dotted doors, windows and crosses were added from expectations."
                : "Final church shapes have not been generated for this drawing. Its original and fitted evidence are already available.";
            warnings.Text = string.Join(" · ", preview.Warnings);
            position.Text = $"{_catalogOffset + _drawings.SelectedIndex + 1:N0} of {_catalogTotal:N0} · Page {_catalogOffset / (int)_reviewLimit.Value! + 1:N0}";
            picture.Content = Comparison(preview, view.SelectedIndex, 475);
            var stage = Result(preview, view.SelectedIndex);
            stageNote.Text = stage.Note + ((stage.Dependencies?.Length ?? 0) > 0 ? " Builds on: " + string.Join(", ", stage.Dependencies!.Select(id => StageOptions.FirstOrDefault(s => s.Id == id)?.Label ?? id)) + "." : "");
        }
        view.SelectionChanged += (_, _) => { _resultView.SelectedIndex = view.SelectedIndex; Update(); };
        Update();
        window.Content = Scroll(Stack(12, heading,
            Buttons(view, SmallButton("◀ Stage", () => MoveStage(view, -1)), SmallButton("Stage ▶", () => MoveStage(view, 1)),joints,
                SmallButton("Compare joints", async () => await ShowJointComparison(row,preview)), SmallButton("Close", window.Close)),
            Buttons(Button("← Previous drawing", async () => await NavigateDrawing(-1)), Button("Next drawing →", async () => await NavigateDrawing(1)), position),
            Text("← / → drawing · Shift + ← / → page · ↑ / ↓ stage", 12), picture,
            counts, stageNote, note,
            Buttons(Button("Open saved artifacts", () => Open(row.DatasetPath)), Button("View stage details", () => ShowStageDetails(row, preview, view.SelectedIndex))),
            warnings), new Thickness(20));
        _drawingStage = view; _updateDrawing = Update; AttachViewerKeys(window); MarkSelection();
        try { await window.ShowDialog(this); }
        finally { _drawingStage = null; _updateDrawing = null; }
    }

    private async void ShowStageDetails(DrawingRow row, Preview preview, int mode)
    {
        try
        {
            // Read-only in-app details avoid generating an HTML/temporary file.
            using var input = new System.IO.Compression.GZipStream(File.OpenRead(Path.Combine(row.DatasetPath, row.EvidencePath)), System.IO.Compression.CompressionMode.Decompress);
            using var document = System.Text.Json.JsonDocument.Parse(input);
            var packet = document.RootElement; var id = Result(preview, mode).Id;
            var details = packet;
            if (packet.TryGetProperty("pipeline", out var pipeline))
                foreach (var stage in pipeline.GetProperty("stages").EnumerateArray())
                    if (stage.GetProperty("id").GetString() == id) { details = stage; break; }
            var dialog = new Window { Title = "Saved stage evidence · " + id, Width = 850, Height = 650 };
            dialog.Content = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Text = System.Text.Json.JsonSerializer.Serialize(details, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), Margin = new Thickness(15) };
            await dialog.ShowDialog(this);
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or InvalidOperationException)
        { SetStatus("Saved details unavailable", error.Message, true); }
    }

    private static ScrollViewer Scroll(Control content, Thickness margin) => new() { Content = content, Margin = margin };
}
