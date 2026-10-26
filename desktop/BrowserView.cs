using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace GojoQuickDraw.Desktop;

internal sealed partial class MainWindow
{
    private readonly Grid _gallery = new() { ColumnSpacing = 12, RowSpacing = 12 };
    private readonly ComboBox _resultView = new()
    {
        ItemsSource = new[] { "Best available result", "Fitted lines", "Area estimates", "Final church shapes" },
        SelectedIndex = 0, MinWidth = 235
    };
    private readonly TextBlock _layerSummary = Text("Open a dataset folder to see original/result pairs.");
    private readonly Dictionary<string, Preview> _pagePreviews = [];
    private readonly Dictionary<string, string> _previewErrors = [];
    private readonly List<Control> _galleryCards = [];
    private Expander _analysisOptions = null!;

    private Control BrowserPage()
    {
        _resultView.SelectionChanged += (_, _) => RenderGallery();
        _gallery.SizeChanged += (_, _) => ArrangeGallery();
        var analyze = JobButton("Create final shapes", [PipelineAction.Analyze], true);
        _analysisCount.ValueChanged += (_, _) => analyze.Content = $"Analyze up to {_analysisCount.Value:N0} drawings";
        analyze.Content = $"Analyze up to {_analysisCount.Value:N0} drawings";
        _analysisOptions = new Expander
        {
            Header = "Create final shapes from converted drawings",
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = Card(Stack(10,
                Text("This adds the church area, part, mapping and cleanup passes to converted drawings. It writes an analysis dataset and opens that result here."),
                Field("Maximum drawings to analyze", _analysisCount, "Counted from the start of the selected category scope. The count is always enforced."),
                analyze,
                new Expander { Header = "Analysis options", HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Content = Stack(10, Field("Analysis dataset folder (optional)", _analysisOutput,
                        "Automatic: a separate analysis folder beside this dataset. Use a new folder when rerunning changed analysis code."),
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
        return Stack(12, InspectionControls(),
            Buttons(Field("Show beside original", _resultView), Button("Refresh folder", async () => await LoadCatalog())),
            _layerSummary, _analysisOptions, _gallery,
            Text("Click a pair to enlarge it. Faint ink is the original; gray dotted doors/windows are added expectations. Changing pages keeps the selected result view.", 12),
            expert);
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

    private static (string Label, double[][][] Profiles, PreviewPart[] Parts) Result(Preview preview, int mode)
    {
        if (mode == 1) return ("Fitted lines", preview.Profiles, []);
        if (mode == 2) return (preview.Areas.Length > 0 ? "Area estimates" : "No area estimates saved", [], preview.Areas);
        if (mode == 3) return (preview.Cleanup.Length > 0 ? "Final church shapes" : "No final shapes saved", [], preview.Cleanup);
        if (preview.Cleanup.Length > 0) return ("Final church shapes", [], preview.Cleanup);
        if (preview.Areas.Length > 0) return ("Area estimates", [], preview.Areas);
        return ("Fitted lines", preview.Profiles, []);
    }

    private static double[] DisplayFrame(Preview preview, PreviewPart[] parts)
    {
        var points = preview.Strokes.SelectMany(s => s).Concat(preview.Profiles.SelectMany(s => s)).Concat(parts.SelectMany(p => p.Points)).ToArray();
        if (points.Length == 0) return [-18, -18, 530, 530];
        return [Math.Min(0, points.Min(p => p[0])) - 18, Math.Min(0, points.Min(p => p[1])) - 18,
            Math.Max(512, points.Max(p => p[0])) + 18, Math.Max(512, points.Max(p => p[1])) + 18];
    }

    private Control Comparison(Preview preview, int mode, double height = 175)
    {
        var result = Result(preview, mode); var frame = DisplayFrame(preview, result.Parts);
        var original = new DrawingCanvas { Profiles = preview.Strokes, Frame = frame, Height = height };
        var output = new DrawingCanvas { Fitted = true, Underlay = preview.Strokes, Parts = result.Parts, Frame = frame, Profiles = result.Profiles, Height = height };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 10 };
        grid.Children.Add(Stack(4, Text("Original", 12, true), original));
        var right = Stack(4, Text(result.Label, 12, true), output); Grid.SetColumn(right, 1); grid.Children.Add(right);
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
        if (_pagePreviews.Count > 0)
        {
            var finals = _pagePreviews.Values.Count(p => p.Cleanup.Length > 0);
            _layerSummary.Text = finals > 0 ? $"{finals} of {_pagePreviews.Count} drawings on this page have final shapes saved."
                : "This page has converted stroke evidence. Use Create final shapes to add the church interpretation passes.";
        }
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
        var picture = new ContentControl();
        void Update() { picture.Content = Comparison(preview, view.SelectedIndex, 475); }
        view.SelectionChanged += (_, _) => { _resultView.SelectedIndex = view.SelectedIndex; Update(); };
        Update();
        var note = preview.Cleanup.Length > 0 ? "Proposed shapes are saved authored interpretations. Gray dotted openings were added from expectations." : "Final church shapes have not been generated for this drawing. Its original and fitted evidence are already available.";
        window.Content = Scroll(Stack(12, Text(row.Category + " · " + row.KeyId, 23, true),
            Buttons(view, Button("Close", window.Close)), picture,
            Text($"{row.Strokes} strokes · {row.Samples} acquired samples · {row.Intervals} fitted intervals"), Text(note),
            Text(string.Join(" · ", preview.Warnings))), new Thickness(20));
        await window.ShowDialog(this);
    }

    private static ScrollViewer Scroll(Control content, Thickness margin) => new() { Content = content, Margin = margin };
}
