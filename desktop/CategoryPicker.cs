using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace GojoQuickDraw.Desktop;

public static class CategoryCatalog
{
    public static IReadOnlyList<string> Names { get; } = Load();
    private static string[] Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(name => name.EndsWith(".categories.json")))!;
        return JsonSerializer.Deserialize<string[]>(stream)!;
    }

    public static string? FromFile(string path)
    {
        var name = Path.GetFileName(path);
        foreach (var suffix in new[] { ".ndjson.gz", ".jsonl.gz", ".ndjson", ".jsonl" })
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { name = name[..^suffix.Length]; break; }
        if (name.StartsWith("full_raw_", StringComparison.OrdinalIgnoreCase)) name = name[9..];
        return Names.FirstOrDefault(category => category.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class CategoryPicker : Window
{
    private readonly HashSet<string> _selected;
    private readonly Dictionary<string, ToggleButton> _buttons = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextBox _search = new() { Watermark = "Find a category…" };
    private readonly TextBlock _count = new() { Foreground = Brush.Parse("#647A79") };
    internal IReadOnlyCollection<string> Selected => _selected;

    public CategoryPicker(IEnumerable<string> selected)
    {
        _selected = new(selected, StringComparer.OrdinalIgnoreCase);
        Title = "Choose QuickDraw categories"; Width = 1040; Height = 730; MinWidth = 650; MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        Background = Brush.Parse("#F1F5F0");
        var panel = new DockPanel { Margin = new Thickness(22) };
        var heading = new StackPanel { Spacing = 10 };
        heading.Children.Add(new TextBlock { Text = "Choose categories", FontSize = 23, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#183D43") });
        heading.Children.Add(new TextBlock { Text = "Toggle categories below. The sample count is a total across your selection.", TextWrapping = TextWrapping.Wrap });
        heading.Children.Add(_search);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        actions.Children.Add(Action("Select all", () => SetAll(true)));
        actions.Children.Add(Action("Clear all", () => SetAll(false)));
        actions.Children.Add(_count); heading.Children.Add(actions);
        heading.Margin = new Thickness(0, 0, 0, 14); DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Margin = new Thickness(0, 14, 0, 0) };
        footer.Children.Add(Action("Cancel", () => Close(null)));
        footer.Children.Add(Action("Use selected categories", () => Close(_selected.Order(StringComparer.OrdinalIgnoreCase).ToArray())));
        DockPanel.SetDock(footer, Dock.Bottom); panel.Children.Add(footer);
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var category in CategoryCatalog.Names)
        {
            var button = new ToggleButton { Content = category, IsChecked = _selected.Contains(category), Width = 153,
                MinHeight = 39, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(8, 6), CornerRadius = new CornerRadius(6) };
            AutomationProperties.SetAutomationId(button, "category-" + category);
            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked == true) _selected.Add(category); else _selected.Remove(category);
                Style(button); UpdateCount();
            };
            _buttons.Add(category, button); Style(button); wrap.Children.Add(button);
        }
        panel.Children.Add(new ScrollViewer { Content = wrap, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        _search.TextChanged += (_, _) =>
        {
            foreach (var (name, button) in _buttons) button.IsVisible = name.Contains(_search.Text ?? "", StringComparison.OrdinalIgnoreCase);
            UpdateCount();
        };
        Content = panel; UpdateCount();
    }

    internal void SetAll(bool selected)
    {
        foreach (var button in _buttons.Values) button.IsChecked = selected;
        UpdateCount();
    }
    internal void SelectCategory(string category, bool selected) => _buttons[category].IsChecked = selected;
    private void UpdateCount() => _count.Text = $"{_selected.Count} / {CategoryCatalog.Names.Count} selected";
    private static void Style(ToggleButton button)
    {
        button.Background = Brush.Parse(button.IsChecked == true ? "#247D72" : "#FFFFFF");
        button.Foreground = button.IsChecked == true ? Brushes.White : Brush.Parse("#183D43");
    }
    private static Button Action(string label, System.Action action)
    {
        var button = new Button { Content = label, Padding = new Thickness(14, 8) }; button.Click += (_, _) => action(); return button;
    }
}
