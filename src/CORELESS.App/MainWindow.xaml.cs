using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Coreless.ViewModels;

namespace Coreless;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly List<Blob> _blobs = new();
    private readonly Random _rng = new();
    private bool _blobsBuilt;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        _vm.Benchmark.PropertyChanged += Benchmark_PropertyChanged;
        Closed += (_, _) =>
        {
            StopGpuLoad();
            _vm.Dispose();
        };
    }

    // ---------- window chrome ----------
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ---------- GPU stress: heavy animated compositing = real GPU fill load ----------
    private void Benchmark_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BenchmarkViewModel.IsGpuStressing)) return;
        if (_vm.Benchmark.IsGpuStressing) StartGpuLoad();
        else StopGpuLoad();
    }

    private void StartGpuLoad()
    {
        BuildBlobs();
        GpuSurface.Visibility = Visibility.Visible;
        CompositionTarget.Rendering -= OnRenderTick;
        CompositionTarget.Rendering += OnRenderTick;
    }

    private void StopGpuLoad()
    {
        CompositionTarget.Rendering -= OnRenderTick;
        GpuSurface.Visibility = Visibility.Collapsed;
    }

    private void BuildBlobs()
    {
        if (_blobsBuilt) return;
        double w = Math.Max(GpuSurface.ActualWidth, 900);
        double h = Math.Max(GpuSurface.ActualHeight, 650);
        Color[] palette =
        {
            Color.FromRgb(0x35, 0xC9, 0xF0),
            Color.FromRgb(0x2E, 0x7D, 0xF7),
            Color.FromRgb(0x7C, 0x5C, 0xFF),
            Color.FromRgb(0x3F, 0xD6, 0x8B),
        };

        for (int i = 0; i < 30; i++)
        {
            double size = 170 + _rng.Next(140);
            Color c = palette[i % palette.Length];
            var fill = new RadialGradientBrush(
                Color.FromArgb(210, c.R, c.G, c.B),
                Color.FromArgb(0, c.R, c.G, c.B));
            var el = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = fill,
                Effect = new BlurEffect { Radius = 55 + _rng.Next(45), RenderingBias = RenderingBias.Quality },
            };
            Canvas.SetLeft(el, _rng.NextDouble() * w);
            Canvas.SetTop(el, _rng.NextDouble() * h);
            GpuSurface.Children.Add(el);

            var v = new Vector((_rng.NextDouble() * 2 - 1) * 5, (_rng.NextDouble() * 2 - 1) * 5);
            _blobs.Add(new Blob(el, v));
        }
        _blobsBuilt = true;
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        double w = GpuSurface.ActualWidth, h = GpuSurface.ActualHeight;
        if (w <= 0 || h <= 0) return;

        foreach (Blob b in _blobs)
        {
            double x = Canvas.GetLeft(b.El) + b.Velocity.X;
            double y = Canvas.GetTop(b.El) + b.Velocity.Y;
            if (x < 0 || x > w) b.Velocity = new Vector(-b.Velocity.X, b.Velocity.Y);
            if (y < 0 || y > h) b.Velocity = new Vector(b.Velocity.X, -b.Velocity.Y);
            Canvas.SetLeft(b.El, Math.Clamp(x, 0, w));
            Canvas.SetTop(b.El, Math.Clamp(y, 0, h));
        }
    }

    private sealed class Blob
    {
        public Blob(Ellipse el, Vector velocity) { El = el; Velocity = velocity; }
        public Ellipse El { get; }
        public Vector Velocity { get; set; }
    }
}
