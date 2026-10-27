using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace GojoQuickDraw.Desktop;

internal sealed partial class MainWindow
{
    private Task _navigation = Task.CompletedTask;
    private Action? _updateDrawing;
    private ComboBox? _drawingStage;
    private bool _navigating;

    private void AttachViewerKeys(Window window)
    {
        window.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (_tabs.SelectedIndex != 0 || _cancellation is not null || args.Handled ||
                (args.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0) return;
            // Keep caret movement, numeric editing and open selectors usable.
            if (args.Source is Visual visual)
            {
                var ancestors = visual.GetVisualAncestors().Prepend(visual).ToArray();
                if (ancestors.Any(v => v is TextBox or NumericUpDown)) return;
                var selector = ancestors.OfType<ComboBox>().FirstOrDefault();
                if (selector is not null && (selector.IsDropDownOpen || selector != _resultView && selector != _drawingStage)) return;
            }
            if (args.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;
            args.Handled = true;
            if (args.Key is Key.Up or Key.Down)
            {
                MoveStage(_drawingStage ?? _resultView, args.Key == Key.Up ? -1 : 1);
                return;
            }
            if (!_navigation.IsCompleted || _catalogCancellation is not null) return;
            _navigation = NavigateDrawing(args.Key == Key.Left ? -1 : 1, args.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }, RoutingStrategies.Tunnel);
    }

    private async Task NavigateDrawing(int direction, bool page = false)
    {
        if (_navigating || _catalogTotal == 0 || _drawings.ItemCount == 0) return;
        var size = (int)_reviewLimit.Value!; var index = Math.Max(0, _drawings.SelectedIndex);
        var target = ViewerNavigation.Target(_catalogOffset, index, size, _catalogTotal, direction, page);
        var offset = target / size * size;
        try
        {
            _navigating = true;
            if (offset != _catalogOffset) await ChangePage(offset);
            if (offset != _catalogOffset || _drawings.ItemCount == 0) return;
            _drawings.SelectedIndex = Math.Clamp(target - offset, 0, _drawings.ItemCount - 1);
            MarkSelection();
            _updateDrawing?.Invoke();
            if (_updateDrawing is null && _drawings.SelectedIndex < _galleryCards.Count)
                _galleryCards[_drawings.SelectedIndex].BringIntoView();
        }
        catch (Exception error) { SetStatus("Could not open drawing", error.Message, true); }
        finally { _navigating = false; }
    }

    private void MarkSelection()
    {
        for (var i = 0; i < _galleryCards.Count; i++)
            if (_galleryCards[i] is Button button)
            {
                button.BorderBrush = i == _drawings.SelectedIndex ? Teal : Avalonia.Media.Brush.Parse("#CBDCD3");
                button.BorderThickness = new Thickness(i == _drawings.SelectedIndex ? 2 : 1);
            }
    }
}

internal static class ViewerNavigation
{
    // Page movement preserves the slot on a page; the short final page clamps
    // to its last drawing. Element movement crosses page boundaries normally.
    internal static int Target(int offset, int slot, int size, int total, int direction, bool page)
    {
        if (total <= 0) return 0;
        var lastPage = (total - 1) / size * size;
        return page ? Math.Min(total - 1, Math.Clamp(offset + direction * size, 0, lastPage) + slot)
            : Math.Clamp(offset + slot + direction, 0, total - 1);
    }
}
