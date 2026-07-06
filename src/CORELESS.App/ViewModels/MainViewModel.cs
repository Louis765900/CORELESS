using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;
using Coreless.Models;
using Coreless.Mvvm;
using Coreless.Services;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace Coreless.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly HardwareMonitorService _service;
    private readonly DispatcherTimer _timer;

    public MainViewModel()
    {
        _service = new HardwareMonitorService();
        Components = new ObservableCollection<ComponentViewModel>();
        QuickStats = new ObservableCollection<InfoItem>();

        ShowOverviewCommand = new RelayCommand(() => GoTo(Section.Overview));
        ShowBenchmarksCommand = new RelayCommand(() => GoTo(Section.Benchmarks));
        RefreshCommand = new RelayCommand(Tick);
        SelectComponentCommand = new RelayCommand(o => { if (o is ComponentViewModel c) SelectedComponent = c; });
        ExportTxtCommand = new RelayCommand(ExportTxt);
        ExportPdfCommand = new RelayCommand(ExportPdf);

        Initialize();

        Benchmark = new BenchmarkViewModel(
            () => Components.FirstOrDefault(c => c.ShortCode == "CPU"),
            () => Components.FirstOrDefault(c => c.ShortCode == "GPU"));

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public enum Section { Overview, Component, Benchmarks }

    public ObservableCollection<ComponentViewModel> Components { get; }
    public ObservableCollection<InfoItem> QuickStats { get; }
    public BenchmarkViewModel Benchmark { get; private set; } = null!;

    public ICommand ShowOverviewCommand { get; }
    public ICommand ShowBenchmarksCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SelectComponentCommand { get; }
    public ICommand ExportTxtCommand { get; }
    public ICommand ExportPdfCommand { get; }

    private Section _section = Section.Overview;
    public Section CurrentSection
    {
        get => _section;
        private set
        {
            if (SetProperty(ref _section, value)) RaiseSectionFlags();
        }
    }

    private ComponentViewModel? _selectedComponent;
    public ComponentViewModel? SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            if (!SetProperty(ref _selectedComponent, value)) return;
            if (value is not null) CurrentSection = Section.Component;
            RaiseSectionFlags();
        }
    }

    private void GoTo(Section section)
    {
        SelectedComponent = null;
        CurrentSection = section;
    }

    private void RaiseSectionFlags()
    {
        OnPropertyChanged(nameof(ShowOverview));
        OnPropertyChanged(nameof(ShowDetail));
        OnPropertyChanged(nameof(ShowBenchmarks));
    }

    public bool ShowOverview => CurrentSection == Section.Overview;
    public bool ShowDetail => CurrentSection == Section.Component && _selectedComponent is not null;
    public bool ShowBenchmarks => CurrentSection == Section.Benchmarks;

    private string _machineName = Environment.MachineName;
    public string MachineName { get => _machineName; private set => SetProperty(ref _machineName, value); }

    private string _osDescription = "Windows";
    public string OsDescription { get => _osDescription; private set => SetProperty(ref _osDescription, value); }

    private string _status = "Initialisation…";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    private string _lastUpdated = "—";
    public string LastUpdated { get => _lastUpdated; private set => SetProperty(ref _lastUpdated, value); }

    private void Initialize()
    {
        try
        {
            _service.Open();
            _service.Update();
        }
        catch (Exception ex)
        {
            Status = "Pilote capteurs indisponible — lancez en administrateur. (" + ex.Message + ")";
        }

        foreach (IHardware hw in _service.Hardware
                     .OrderBy(h => CategoryOrder(h.HardwareType))
                     .ThenBy(h => h.Name))
        {
            Components.Add(new ComponentViewModel(hw));
        }

        // second update so first displayed values aren't blank
        try { _service.Update(); } catch { /* ignore */ }
        foreach (ComponentViewModel c in Components) c.Refresh();

        BuildSystemSummary();
        Status = Components.Count > 0
            ? $"{Components.Count} composants détectés"
            : "Aucun capteur — vérifiez les droits administrateur";
    }

    private void Tick()
    {
        try
        {
            _service.Update();
            foreach (ComponentViewModel c in Components) c.Refresh();
            LastUpdated = DateTime.Now.ToString("HH:mm:ss");
        }
        catch (Exception ex)
        {
            Status = "Erreur de lecture: " + ex.Message;
        }
    }

    private void BuildSystemSummary()
    {
        OsDescription = ReadFriendlyOsName();

        ComponentViewModel? cpu = Components.FirstOrDefault(c => c.ShortCode == "CPU");
        ComponentViewModel? gpu = Components.FirstOrDefault(c => c.ShortCode == "GPU");
        ComponentViewModel? board = Components.FirstOrDefault(c => c.ShortCode == "MB");

        QuickStats.Clear();
        if (cpu is not null) QuickStats.Add(new InfoItem("Processeur", cpu.Name));
        QuickStats.Add(new InfoItem("Cœurs logiques", Environment.ProcessorCount.ToString()));
        if (gpu is not null) QuickStats.Add(new InfoItem("Carte graphique", gpu.Name));

        double? ramGb = ReadTotalMemoryGb();
        if (ramGb is not null) QuickStats.Add(new InfoItem("Mémoire vive", $"{ramGb:0.0} Go"));
        if (board is not null) QuickStats.Add(new InfoItem("Carte mère", board.Name));
        QuickStats.Add(new InfoItem("Système", OsDescription));
        QuickStats.Add(new InfoItem("Architecture", RuntimeInformation.OSArchitecture.ToString()));
    }

    private double? ReadTotalMemoryGb()
    {
        IHardware? mem = _service.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);
        if (mem is null) return null;
        float used = mem.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Used"))?.Value ?? 0;
        float avail = mem.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Available"))?.Value ?? 0;
        double total = used + avail;
        return total > 0 ? total : null;
    }

    private static string ReadFriendlyOsName()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string? product = key?.GetValue("ProductName") as string;
            string? display = key?.GetValue("DisplayVersion") as string;
            string? build = key?.GetValue("CurrentBuild") as string;
            if (!string.IsNullOrEmpty(product))
            {
                // Windows 11 still reports "Windows 10" in ProductName; correct via build.
                if (int.TryParse(build, out int b) && b >= 22000)
                    product = product.Replace("Windows 10", "Windows 11");
                string extra = string.IsNullOrEmpty(display) ? "" : $" {display}";
                string bld = string.IsNullOrEmpty(build) ? "" : $" (build {build})";
                return $"{product}{extra}{bld}";
            }
        }
        catch { /* fall through */ }
        return RuntimeInformation.OSDescription;
    }

    private static int CategoryOrder(HardwareType t) => t switch
    {
        HardwareType.Cpu => 0,
        HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => 1,
        HardwareType.Memory => 2,
        HardwareType.Motherboard => 3,
        HardwareType.Storage => 4,
        HardwareType.Network => 5,
        HardwareType.Cooler => 6,
        HardwareType.Battery => 7,
        HardwareType.Psu => 8,
        _ => 9,
    };

    private void ExportTxt()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Exporter le rapport",
            FileName = $"CORELESS_{MachineName}_{DateTime.Now:yyyyMMdd_HHmm}.txt",
            Filter = "Fichier texte (*.txt)|*.txt",
            DefaultExt = ".txt",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            string report = ReportBuilder.BuildText(MachineName, OsDescription, QuickStats, Components);
            File.WriteAllText(dlg.FileName, report, System.Text.Encoding.UTF8);
            Status = "Rapport TXT exporté";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Échec de l'export : " + ex.Message, "CORELESS",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void ExportPdf()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Exporter le rapport PDF",
            FileName = $"CORELESS_{MachineName}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
            Filter = "Document PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            PdfReportBuilder.Build(dlg.FileName, MachineName, OsDescription, QuickStats,
                Components, Benchmark.Results);
            Status = "Rapport PDF exporté";
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); }
            catch { /* opening is best-effort */ }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Échec de l'export PDF : " + ex.Message, "CORELESS",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _service.Dispose();
    }
}
