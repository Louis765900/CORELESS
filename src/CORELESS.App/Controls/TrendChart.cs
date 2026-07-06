using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Coreless.Controls;

/// <summary>Lightweight real-time line chart: auto-scaled polyline with area fill.</summary>
public sealed class TrendChart : Control
{
    static TrendChart()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TrendChart), new FrameworkPropertyMetadata(typeof(TrendChart)));
    }

    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(ObservableCollection<double>), typeof(TrendChart),
        new FrameworkPropertyMetadata(null, OnValuesChanged));

    public ObservableCollection<double>? Values
    {
        get => (ObservableCollection<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush), typeof(Brush), typeof(TrendChart),
        new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public static readonly DependencyProperty AreaBrushProperty = DependencyProperty.Register(
        nameof(AreaBrush), typeof(Brush), typeof(TrendChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush? AreaBrush
    {
        get => (Brush?)GetValue(AreaBrushProperty);
        set => SetValue(AreaBrushProperty, value);
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (TrendChart)d;
        if (e.OldValue is ObservableCollection<double> oldC)
            oldC.CollectionChanged -= chart.OnCollectionChanged;
        if (e.NewValue is ObservableCollection<double> newC)
            newC.CollectionChanged += chart.OnCollectionChanged;
        chart.InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var values = Values;
        if (values is null || values.Count < 2)
        {
            // baseline hint
            var pen0 = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
            dc.DrawLine(pen0, new Point(0, h - 1), new Point(w, h - 1));
            return;
        }

        int n = values.Count;
        double min = double.MaxValue, max = double.MinValue;
        foreach (double v in values) { if (v < min) min = v; if (v > max) max = v; }
        double range = max - min;
        if (range < 1e-6) { min -= 1; max += 1; range = max - min; }
        double pad = range * 0.12;
        min -= pad; max += pad; range = max - min;

        double X(int i) => i / (double)(n - 1) * w;
        double Y(double v) => h - (v - min) / range * h;

        var line = new StreamGeometry();
        var area = new StreamGeometry();
        using (var lc = line.Open())
        using (var ac = area.Open())
        {
            var p0 = new Point(X(0), Y(values[0]));
            lc.BeginFigure(p0, false, false);
            ac.BeginFigure(new Point(X(0), h), true, true);
            ac.LineTo(p0, true, false);
            for (int i = 1; i < n; i++)
            {
                var p = new Point(X(i), Y(values[i]));
                lc.LineTo(p, true, true);
                ac.LineTo(p, true, false);
            }
            ac.LineTo(new Point(X(n - 1), h), true, false);
        }
        line.Freeze();
        area.Freeze();

        if (AreaBrush is not null) dc.DrawGeometry(AreaBrush, null, area);
        var pen = new Pen(LineBrush, 2) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        dc.DrawGeometry(null, pen, line);

        // last-point marker
        var last = new Point(X(n - 1), Y(values[n - 1]));
        dc.DrawEllipse(LineBrush, null, last, 3, 3);
    }
}
