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
    public double[][][] Underlay { get; set; } = [];
    public double[][][] Residue { get; set; } = [];
    public PreviewPart[] Parts { get; set; } = [];
    public double[] Frame { get; set; } = [0, 0, 512, 512];
    public DrawingCanvas() { MinHeight = 160; ClipToBounds = true; }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.Custom(new DrawingOperation(new Rect(Bounds.Size), _profiles, Fitted, Underlay, Parts, Frame, Residue));
    }
}

internal sealed class DrawingOperation(Rect bounds, double[][][] profiles, bool fitted, double[][][] underlay,
    PreviewPart[] parts, double[] frame, double[][][] residue) : ICustomDrawOperation
{
    public Rect Bounds { get; } = bounds;
    public void Dispose() { }
    public bool HitTest(Point point) => Bounds.Contains(point);
    public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);
    public void Render(ImmediateDrawingContext context)
    {
        if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature feature) return;
        using var lease = feature.Lease(); var canvas = lease.SkCanvas;
        canvas.Save(); canvas.ClipRect(new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height)); canvas.Clear(SKColors.White);
        var width = (float)Math.Max(1, frame[2] - frame[0]); var height = (float)Math.Max(1, frame[3] - frame[1]);
        var scale = Math.Max(.01f, Math.Min(((float)Bounds.Width - 16) / width, ((float)Bounds.Height - 16) / height));
        canvas.Translate(((float)Bounds.Width - width * scale) / 2, ((float)Bounds.Height - height * scale) / 2);
        canvas.Scale(scale); canvas.Translate(-(float)frame[0], -(float)frame[1]);
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f / scale,
            StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        void Paths(double[][][] paths, bool curves, SKColor color)
        {
            paint.Color = color;
            foreach (var points in paths)
            {
                if (points.Length == 0 || points.Any(p => p.Length != 2 || p.Any(n => !double.IsFinite(n)))) continue;
                using var path = new SKPath(); path.MoveTo((float)points[0][0], (float)points[0][1]);
                if (curves && points.Length == 3) path.QuadTo((float)points[1][0], (float)points[1][1], (float)points[2][0], (float)points[2][1]);
                else if (curves && points.Length == 4) path.CubicTo((float)points[1][0], (float)points[1][1], (float)points[2][0], (float)points[2][1], (float)points[3][0], (float)points[3][1]);
                else foreach (var point in points.Skip(1)) path.LineTo((float)point[0], (float)point[1]);
                canvas.DrawPath(path, paint);
                if (points.Length == 1) canvas.DrawCircle((float)points[0][0], (float)points[0][1], 1.5f / scale, paint);
            }
        }
        Paths(underlay, false, SKColor.Parse("#465852").WithAlpha(45));
        Paths(residue, true, SKColor.Parse("#647A79").WithAlpha(90));
        Paths(profiles, fitted, SKColor.Parse(fitted ? "#247D72" : "#465852"));
        foreach (var part in parts)
        {
            var prior = part.Status == "prior-only"; var added = prior && part.Role is "door" or "window" or "cross";
            var color = SKColor.Parse(added ? "#9AA2A6" : part.Color);
            if (part.Fill.Length > 0)
            {
                using var fill = new SKPath(); var first = part.Points[part.Fill[0]]; fill.MoveTo((float)first[0], (float)first[1]);
                foreach (var i in part.Fill.Skip(1)) fill.LineTo((float)part.Points[i][0], (float)part.Points[i][1]);
                fill.Close(); paint.Color = color.WithAlpha(20); paint.Style = SKPaintStyle.Fill; canvas.DrawPath(fill, paint); paint.Style = SKPaintStyle.Stroke;
            }
            paint.Color = color; paint.StrokeWidth = 1.5f / scale;
            using var dash = added ? SKPathEffect.CreateDash([1 / scale, 4 / scale], 0) : prior || part.Role == "ground" ? SKPathEffect.CreateDash([5 / scale, 3 / scale], 0) : null;
            paint.PathEffect = dash;
            foreach (var segment in part.Segments)
            {
                var a = part.Points[segment[0]]; var b = part.Points[segment[1]];
                canvas.DrawLine((float)a[0], (float)a[1], (float)b[0], (float)b[1], paint);
            }
            paint.PathEffect = null;
        }
        canvas.Restore();
    }
}
