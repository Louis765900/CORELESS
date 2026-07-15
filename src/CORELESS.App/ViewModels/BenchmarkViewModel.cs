using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Coreless.Models;
using Coreless.Mvvm;
using Coreless.Services.Benchmarks;

namespace Coreless.ViewModels;

/// <summary>Drives benchmark runs and the live CPU stress test panel.</summary>
public sealed class BenchmarkViewModel : ObservableObject
{
    private readonly Func<ComponentViewModel?> _cpuAccessor;
    private readonly Func<ComponentViewModel?> _gpuAccessor;
    private readonly StressTest _stress = new();
    private readonly DispatcherTimer _stressTimer;
    private readonly DispatcherTimer _gpuTimer;

    private CancellationTokenSource? _cts;
    private DateTime _stressStart;
    private double _observedMaxClock;
    private DateTime _gpuStart;
    private double _gpuMaxTempValue;

    public BenchmarkViewModel(Func<ComponentViewModel?> cpuAccessor, Func<ComponentViewModel?> gpuAccessor)
    {
        _cpuAccessor = cpuAccessor;
        _gpuAccessor = gpuAccessor;

        RunCpuCommand = new RelayCommand(() => _ = RunOne(CpuBenchmark.RunAsync, "CPU débit"), () => CanRun);
        RunRenderCommand = new RelayCommand(() => _ = RunOne(RenderBenchmark.RunAsync, "Rendu 3D"), () => CanRun);
        RunPiCommand = new RelayCommand(() => _ = RunOne(PiBenchmark.RunAsync, "Calcul Pi"), () => CanRun);
        RunCompressionCommand = new RelayCommand(() => _ = RunOne(CompressionBenchmark.RunAsync, "Compression"), () => CanRun);
        RunRamSuiteCommand = new RelayCommand(() => _ = RunOne(RamSuiteBenchmark.RunAsync, "Mémoire (suite)"), () => CanRun);
        RunDiskSuiteCommand = new RelayCommand(() => _ = RunOne(DiskSuiteBenchmark.RunAsync, "Stockage (suite)"), () => CanRun);
        RunAllCommand = new RelayCommand(() => _ = RunSuite(), () => CanRun);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsRunning);
        StartStressCommand = new RelayCommand(StartStress, () => !IsStressing && !IsRunning);
        StopStressCommand = new RelayCommand(StopStress, () => IsStressing);
        StartGpuStressCommand = new RelayCommand(StartGpuStress, () => !IsGpuStressing && !IsRunning);
        StopGpuStressCommand = new RelayCommand(StopGpuStress, () => IsGpuStressing);
        ClearCommand = new RelayCommand(() => Results.Clear(), () => Results.Count > 0);

        _stressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _stressTimer.Tick += (_, _) => SampleStress();
        _gpuTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _gpuTimer.Tick += (_, _) => SampleGpu();
    }

    public ObservableCollection<BenchmarkOutcome> Results { get; } = new();

    private const int HistoryCap = 120; // ~2 min at 1s
    public ObservableCollection<double> TempHistory { get; } = new();
    public ObservableCollection<double> LoadHistory { get; } = new();

    public ICommand RunCpuCommand { get; }
    public ICommand RunRenderCommand { get; }
    public ICommand RunPiCommand { get; }
    public ICommand RunCompressionCommand { get; }
    public ICommand RunRamSuiteCommand { get; }
    public ICommand RunDiskSuiteCommand { get; }
    public ICommand RunAllCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand StartStressCommand { get; }
    public ICommand StopStressCommand { get; }
    public ICommand StartGpuStressCommand { get; }
    public ICommand StopGpuStressCommand { get; }
    public ICommand ClearCommand { get; }

    public ObservableCollection<double> GpuTempHistory { get; } = new();
    public ObservableCollection<double> GpuLoadHistory { get; } = new();

    public int Cores => Environment.ProcessorCount;
    public bool CanRun => !IsRunning && !IsStressing && !IsGpuStressing;

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set { if (SetProperty(ref _isRunning, value)) OnPropertyChanged(nameof(CanRun)); }
    }

    private string? _currentTask;
    public string? CurrentTask { get => _currentTask; private set => SetProperty(ref _currentTask, value); }

    private double _progress;
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }

    private string _status = "Prêt. Lancez un test.";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    // ---- stress state ----
    private bool _isStressing;
    public bool IsStressing
    {
        get => _isStressing;
        private set { if (SetProperty(ref _isStressing, value)) OnPropertyChanged(nameof(CanRun)); }
    }

    private string _stressElapsed = "00:00";
    public string StressElapsed { get => _stressElapsed; private set => SetProperty(ref _stressElapsed, value); }

    private string _stressTemp = "—";
    public string StressTemp { get => _stressTemp; private set => SetProperty(ref _stressTemp, value); }

    private string _stressMaxTemp = "—";
    public string StressMaxTemp { get => _stressMaxTemp; private set => SetProperty(ref _stressMaxTemp, value); }

    private string _stressClock = "—";
    public string StressClock { get => _stressClock; private set => SetProperty(ref _stressClock, value); }

    private string _stressLoad = "—";
    public string StressLoad { get => _stressLoad; private set => SetProperty(ref _stressLoad, value); }

    private double _stressTempValue;
    public double StressTempValue { get => _stressTempValue; private set => SetProperty(ref _stressTempValue, value); }

    private bool _throttleWarning;
    public bool ThrottleWarning { get => _throttleWarning; private set => SetProperty(ref _throttleWarning, value); }

    private double _maxTempValue;

    // ---- GPU stress state ----
    private bool _isGpuStressing;
    public bool IsGpuStressing
    {
        get => _isGpuStressing;
        private set { if (SetProperty(ref _isGpuStressing, value)) OnPropertyChanged(nameof(CanRun)); }
    }

    private string _gpuElapsed = "00:00";
    public string GpuElapsed { get => _gpuElapsed; private set => SetProperty(ref _gpuElapsed, value); }

    private string _gpuTemp = "—";
    public string GpuTemp { get => _gpuTemp; private set => SetProperty(ref _gpuTemp, value); }

    private string _gpuMaxTemp = "—";
    public string GpuMaxTemp { get => _gpuMaxTemp; private set => SetProperty(ref _gpuMaxTemp, value); }

    private string _gpuClock = "—";
    public string GpuClock { get => _gpuClock; private set => SetProperty(ref _gpuClock, value); }

    private string _gpuLoad = "—";
    public string GpuLoad { get => _gpuLoad; private set => SetProperty(ref _gpuLoad, value); }

    private double _gpuTempValue;
    public double GpuTempValue { get => _gpuTempValue; private set => SetProperty(ref _gpuTempValue, value); }

    private bool _suiteCanceled;

    private async Task<BenchmarkOutcome?> RunOne(Func<IProgress<double>, CancellationToken, Task<BenchmarkOutcome>> bench, string name)
    {
        if (!CanRun) return null;
        _cts = new CancellationTokenSource();
        IsRunning = true;
        CurrentTask = name;
        Progress = 0;
        Status = $"Test {name} en cours…";
        var progress = new Progress<double>(p => Progress = p * 100);
        try
        {
            BenchmarkOutcome outcome = await bench(progress, _cts.Token);
            Results.Insert(0, outcome);
            Status = $"{name} terminé — {outcome.ScoreValue} {outcome.ScoreUnit}";
            return outcome;
        }
        catch (OperationCanceledException)
        {
            Status = $"{name} annulé.";
            _suiteCanceled = true;
            return null;
        }
        catch (Exception ex)
        {
            Status = $"Erreur {name}: {ex.Message}";
            return null;
        }
        finally
        {
            IsRunning = false;
            CurrentTask = null;
            Progress = 0;
            _cts = null;
        }
    }

    // Full benchmark suite: CPU (render/pi/compression) + RAM suite + disk suite,
    // then a weighted "Indice CORELESS" composite (PCMark/Novabench-style single number).
    private async Task RunSuite()
    {
        _suiteCanceled = false;
        var outcomes = new List<BenchmarkOutcome>();

        (Func<IProgress<double>, CancellationToken, Task<BenchmarkOutcome>> fn, string name)[] steps =
        {
            (RenderBenchmark.RunAsync, "Rendu 3D"),
            (PiBenchmark.RunAsync, "Calcul Pi"),
            (CompressionBenchmark.RunAsync, "Compression"),
            (RamSuiteBenchmark.RunAsync, "Mémoire (suite)"),
            (DiskSuiteBenchmark.RunAsync, "Stockage (suite)"),
        };

        foreach ((var fn, string name) in steps)
        {
            BenchmarkOutcome? o = await RunOne(fn, name);
            if (_suiteCanceled) { Status = "Suite annulée."; return; }
            if (o is not null) outcomes.Add(o);
        }

        BenchmarkOutcome composite = BuildComposite(outcomes);
        Results.Insert(0, composite);
        Status = $"Suite terminée — Indice CORELESS {composite.ScoreValue}";
    }

    // Normalize each result against a rough mid-range-desktop reference (= index 1000),
    // then average the available components. Reference values are arbitrary but stable,
    // so scores are comparable between machines running the same CORELESS build.
    private static BenchmarkOutcome BuildComposite(List<BenchmarkOutcome> outcomes)
    {
        (string label, string match, double reference)[] refs =
        {
            ("Rendu 3D (CPU)", "Rendu 3D",    30.0),   // Mpx/s
            ("Calcul Pi",      "Pi",          18_000), // chiffres/s
            ("Compression",    "Compression", 250),    // Mo/s
            ("Mémoire",        "Mémoire",     12.0),    // Go/s copie
            ("Stockage",       "Stockage",    3500),   // Mo/s SEQ
        };

        var parts = new List<InfoItem>();
        double sum = 0; int n = 0;
        foreach ((string label, string match, double reference) in refs)
        {
            BenchmarkOutcome? o = outcomes.FirstOrDefault(x => x.Title.Contains(match) && x.Score > 0);
            if (o is null) continue;
            double idx = 1000.0 * o.Score / reference;
            sum += idx; n++;
            parts.Add(new InfoItem(label, $"{Math.Round(idx):N0} pts"));
        }

        double global = n > 0 ? sum / n : 0;
        return new BenchmarkOutcome
        {
            Title = "★ Indice CORELESS (global)",
            ScoreLabel = "Score global",
            ScoreValue = Math.Round(global).ToString("N0"),
            ScoreUnit = "pts",
            Score = global,
            Category = BenchCategory.Composite,
            Details = parts.Count > 0 ? parts : new List<InfoItem> { new("Aucun résultat", "—") }
        };
    }

    private void StartStress()
    {
        _stress.Start();
        IsStressing = true;
        ThrottleWarning = false;
        _stressStart = DateTime.Now;
        _observedMaxClock = 0;
        _maxTempValue = 0;
        TempHistory.Clear();
        LoadHistory.Clear();
        Status = $"Stress test en cours sur {Cores} threads…";
        _stressTimer.Start();
    }

    private void StopStress()
    {
        _stress.Stop();
        _stressTimer.Stop();
        IsStressing = false;
        Status = $"Stress test arrêté. Temp max {StressMaxTemp}.";
    }

    private void StartGpuStress()
    {
        // Actual GPU load is generated by the view (heavy animated compositing)
        // which reacts to IsGpuStressing. Here we drive timing + live monitoring.
        IsGpuStressing = true;
        _gpuStart = DateTime.Now;
        _gpuMaxTempValue = 0;
        GpuTempHistory.Clear();
        GpuLoadHistory.Clear();
        Status = "Stress GPU en cours (charge graphique)…";
        _gpuTimer.Start();
    }

    private void StopGpuStress()
    {
        _gpuTimer.Stop();
        IsGpuStressing = false;
        Status = $"Stress GPU arrêté. Temp max {GpuMaxTemp}.";
    }

    private void SampleGpu()
    {
        GpuElapsed = (DateTime.Now - _gpuStart).ToString(@"mm\:ss");

        ComponentViewModel? gpu = _gpuAccessor();
        float? temp = gpu?.TempHeadline?.Raw;
        float? clock = gpu?.ClockHeadline?.Raw;
        float? load = gpu?.LoadHeadline?.Raw;

        if (temp is float t && !float.IsNaN(t))
        {
            GpuTemp = $"{t:0.0} °C";
            GpuTempValue = t;
            if (t > _gpuMaxTempValue) { _gpuMaxTempValue = t; GpuMaxTemp = $"{t:0.0} °C"; }
            Push(GpuTempHistory, t);
        }
        if (clock is float c && !float.IsNaN(c)) GpuClock = $"{c:0} MHz";
        if (load is float l && !float.IsNaN(l))
        {
            GpuLoad = $"{l:0} %";
            Push(GpuLoadHistory, l);
        }
    }

    private void SampleStress()
    {
        StressElapsed = (DateTime.Now - _stressStart).ToString(@"mm\:ss");

        ComponentViewModel? cpu = _cpuAccessor();
        float? temp = cpu?.TempHeadline?.Raw;
        float? clock = cpu?.ClockHeadline?.Raw;
        float? load = cpu?.LoadHeadline?.Raw;

        if (temp is float t && !float.IsNaN(t))
        {
            StressTemp = $"{t:0.0} °C";
            StressTempValue = t;
            if (t > _maxTempValue) { _maxTempValue = t; StressMaxTemp = $"{t:0.0} °C"; }
            Push(TempHistory, t);
        }
        if (load is float l && !float.IsNaN(l))
        {
            StressLoad = $"{l:0} %";
            Push(LoadHistory, l);
        }
        if (clock is float c && !float.IsNaN(c))
        {
            StressClock = $"{c:0} MHz";
            if (c > _observedMaxClock) _observedMaxClock = c;
            // throttle: sustained clock dropped notably below peak while under load
            if (_observedMaxClock > 0 && c < _observedMaxClock * 0.85 && (load ?? 0) > 50)
                ThrottleWarning = true;
        }
    }

    private static void Push(ObservableCollection<double> history, double value)
    {
        history.Add(value);
        while (history.Count > HistoryCap) history.RemoveAt(0);
    }
}
