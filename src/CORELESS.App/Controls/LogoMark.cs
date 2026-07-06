using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Coreless.Controls;

/// <summary>Vector CORELESS mark: three concentric "C" arcs opening right + a tech node.</summary>
public sealed class LogoMark : Control
{
    public static readonly DependencyProperty MarkBrushProperty = DependencyProperty.Register(
        nameof(MarkBrush), typeof(Brush), typeof(LogoMark),
        new FrameworkPropertyMetadata(
            new SolidColorBrush(Color.FromRgb(0xFF, 0x17, 0x44)),
            FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush MarkBrush { get => (Brush)GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }

    private const double StartDeg = 125;  // gap centred on the right (east)
    private const double SweepDeg = 290;

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double s = Math.Min(w, h);
        double cx = w / 2, cy = h / 2;
        double thick = 0.085 * s;

        double[] radii = { 0.42 * s, 0.32 * s, 0.22 * s };
        foreach (double r in radii)
            dc.DrawGeometry(null, ArcPen(thick), CArc(cx, cy, r));

        // node ring near the upper-right terminus
        double rn = 0.32 * s;
        Point node = P(cx, cy, 60, rn);
        var ringPen = new Pen(MarkBrush, 0.04 * s);
        ringPen.Freeze();
        dc.DrawEllipse(null, ringPen, node, 0.055 * s, 0.055 * s);
    }

    private Pen ArcPen(double thick)
    {
        var pen = new Pen(MarkBrush, thick) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }

    private static Geometry CArc(double cx, double cy, double r)
    {
        Point start = P(cx, cy, StartDeg, r);
        Point end = P(cx, cy, StartDeg + SweepDeg, r);
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(r, r), 0, SweepDeg > 180, SweepDirection.Clockwise, true, false);
        }
        geo.Freeze();
        return geo;
    }

    private static Point P(double cx, double cy, double deg, double r)
    {
        double t = deg * Math.PI / 180;
        return new Point(cx + r * Math.Sin(t), cy - r * Math.Cos(t));
    }
}
