using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace GojoQuickDraw.Desktop;

internal sealed class DrawingCanvas : Control
{
    private double[][][] _profiles = [];
    public bool Fitted { get; init; }
    public double[][][] Profiles { get => _profiles; set { _profiles = value; InvalidateVisual(); } }
    public DrawingCanvas() { MinHeight = 240; ClipToBounds = true; }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.Custom(new DrawingOperation(new Rect(Bounds.Size), _profiles, Fitted));
    }
}

internal sealed class DrawingOperation(Rect bounds, double[][][] profiles, bool fitted) : ICustomDrawOperation
{
    public Rect Bounds { get; } = bounds;
    public void Dispose() { }
    public bool HitTest(Point point) => Bounds.Contains(point);
    public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);
    public void Render(ImmediateDrawingContext context)
    {
        if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature feature) return;
        using var lease = feature.Lease();
        var canvas = lease.SkCanvas;
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height));
        canvas.Clear(SKColors.White);
        var scale = Math.Max(.01f, ((float)Math.Min(Bounds.Width, Bounds.Height) - 28) / 512);
        canvas.Translate(((float)Bounds.Width - 512 * scale) / 2, ((float)Bounds.Height - 512 * scale) / 2);
        canvas.Scale(scale);
        using var paint = new SKPaint { IsAntialias = true, Color = SKColor.Parse(fitted ? "#247D72" : "#95A5A0"),
            Style = SKPaintStyle.Stroke, StrokeWidth = 2.2f / scale, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        foreach (var points in profiles)
        {
            if (points.Length == 0 || points.Any(p => p.Length != 2 || p.Any(n => !double.IsFinite(n)))) continue;
            using var path = new SKPath();
            path.MoveTo((float)points[0][0], (float)points[0][1]);
            if (fitted && points.Length == 3)
                path.QuadTo((float)points[1][0], (float)points[1][1], (float)points[2][0], (float)points[2][1]);
            else if (fitted && points.Length == 4)
                path.CubicTo((float)points[1][0], (float)points[1][1], (float)points[2][0], (float)points[2][1], (float)points[3][0], (float)points[3][1]);
            else foreach (var point in points.Skip(1)) path.LineTo((float)point[0], (float)point[1]);
            canvas.DrawPath(path, paint);
            if (points.Length == 1) canvas.DrawCircle((float)points[0][0], (float)points[0][1], 1.5f / scale, paint);
        }
        canvas.Restore();
    }
}
