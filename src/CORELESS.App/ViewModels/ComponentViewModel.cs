using System.Collections.ObjectModel;
using System.Windows.Media;
using Coreless.Models;
using Coreless.Mvvm;
using Coreless.Services;
using LibreHardwareMonitor.Hardware;

namespace Coreless.ViewModels;

/// <summary>One hardware node (CPU, GPU, disk…) with its live sensors and sub-nodes.</summary>
public sealed class ComponentViewModel : ObservableObject
{
    private readonly IHardware _hw;
    private readonly List<SensorViewModel> _all = new();

    public ComponentViewModel(IHardware hw)
    {
        _hw = hw;
        Name = hw.Name;

        (Category, ShortCode, string colorHex) = Describe(hw.HardwareType);
        ColorHex = colorHex;
        BadgeBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        BadgeBrush.Freeze();

        // Build sensor view models once; values refresh in place each tick. Fan (RPM) sensors
        // are paired with their sibling Control (%) sensor of the same name so SensorViewModel
        // can tell "genuinely at 0 RPM" apart from "driver never reports the tachometer".
        foreach (ISensor s in hw.Sensors.OrderBy(s => s.SensorType).ThenBy(s => s.Name))
        {
            ISensor? pairedControl = s.SensorType == SensorType.Fan
                ? hw.Sensors.FirstOrDefault(o => o.SensorType == SensorType.Control && o.Name == s.Name)
                : null;
            _all.Add(new SensorViewModel(s, pairedControl));
        }

        Groups = new ObservableCollection<SensorGroupViewModel>(
            _all.GroupBy(s => SensorFormat.GroupLabel(s.Type))
                .Select(g => new SensorGroupViewModel(g.Key, g)));

        SubComponents = new ObservableCollection<ComponentViewModel>(
            hw.SubHardware.Select(sub => new ComponentViewModel(sub)));

        TempHeadline = Pick(SensorType.Temperature, "Package", "Tctl", "Tdie", "Core", "GPU", "CPU");
        LoadHeadline = Pick(SensorType.Load, "Total", "GPU Core", "Memory", "Used");
        ClockHeadline = Pick(SensorType.Clock, "Core", "GPU Core");

        SensorCount = _all.Count + SubComponents.Sum(c => c.SensorCount);

        _isNetwork = hw.HardwareType == HardwareType.Network;
        _isVirtualNetworkName = _isNetwork && NetworkInterfaceFilter.IsVirtualByName(Name);
    }

    private readonly bool _isNetwork;
    private readonly bool _isVirtualNetworkName;
    private bool _everActive;

    /// <summary>
    /// True for network adapters that are either recognized virtual/filter-driver
    /// entries by name, or have shown no non-zero sensor value across the whole
    /// session so far — the noise flagged in the functional audit (Npcap/QoS/WFP
    /// duplicates, kernel debugger adapters, all stuck at 0,0).
    /// </summary>
    public bool IsSuspectedInactiveNetwork => _isNetwork && (_isVirtualNetworkName || !_everActive);

    public string Name { get; }
    public string Category { get; }
    public string ShortCode { get; }
    public string ColorHex { get; }
    public SolidColorBrush BadgeBrush { get; }
    public int SensorCount { get; }

    public ObservableCollection<SensorGroupViewModel> Groups { get; }
    public ObservableCollection<ComponentViewModel> SubComponents { get; }

    public SensorViewModel? TempHeadline { get; }
    public SensorViewModel? LoadHeadline { get; }
    public SensorViewModel? ClockHeadline { get; }

    private const int SparkCap = 40;
    public ObservableCollection<double> TempSpark { get; } = new();
    public ObservableCollection<double> LoadSpark { get; } = new();
    public ObservableCollection<double> ClockSpark { get; } = new();

    public bool HasTemp => TempHeadline is not null;
    public bool HasLoad => LoadHeadline is not null;
    public bool HasClock => ClockHeadline is not null;
    public bool HasSensors => _all.Count > 0 || SubComponents.Count > 0;

    public void Refresh()
    {
        foreach (SensorViewModel s in _all) s.Refresh();
        foreach (ComponentViewModel c in SubComponents) c.Refresh();

        PushSpark(TempSpark, TempHeadline?.Raw);
        PushSpark(LoadSpark, LoadHeadline?.Raw);
        PushSpark(ClockSpark, ClockHeadline?.Raw);

        if (_isNetwork && !_everActive && _all.Any(s => s.Raw is float v && !float.IsNaN(v) && v != 0f))
        {
            _everActive = true;
            OnPropertyChanged(nameof(IsSuspectedInactiveNetwork));
        }
    }

    private static void PushSpark(ObservableCollection<double> spark, float? value)
    {
        if (value is not float v || float.IsNaN(v)) return;
        spark.Add(v);
        while (spark.Count > SparkCap) spark.RemoveAt(0);
    }

    private SensorViewModel? Pick(SensorType type, params string[] preferred)
    {
        var candidates = _all.Where(s => s.Type == type).ToList();
        if (candidates.Count == 0) return null;
        foreach (string key in preferred)
        {
            var hit = candidates.FirstOrDefault(
                s => s.Name.Contains(key, StringComparison.OrdinalIgnoreCase));
            if (hit is not null) return hit;
        }
        return candidates[0];
    }

    private static (string category, string code, string color) Describe(HardwareType t) => t switch
    {
        HardwareType.Cpu => ("Processeur", "CPU", "#FF35C9F0"),
        HardwareType.GpuNvidia => ("Carte graphique", "GPU", "#FF3FD68B"),
        HardwareType.GpuAmd => ("Carte graphique", "GPU", "#FFFF5C5C"),
        HardwareType.GpuIntel => ("Carte graphique", "GPU", "#FF2E7DF7"),
        HardwareType.Memory => ("Mémoire", "RAM", "#FF7C5CFF"),
        HardwareType.Motherboard => ("Carte mère", "MB", "#FFFFB020"),
        HardwareType.SuperIO => ("Contrôleur E/S", "IO", "#FFFFB020"),
        HardwareType.Storage => ("Stockage", "DSK", "#FF2E7DF7"),
        HardwareType.Network => ("Réseau", "NET", "#FF35C9F0"),
        HardwareType.Cooler => ("Refroidissement", "FAN", "#FF3FD68B"),
        HardwareType.EmbeddedController => ("Contrôleur", "EC", "#FF8B98A9"),
        HardwareType.Psu => ("Alimentation", "PSU", "#FFFFB020"),
        HardwareType.Battery => ("Batterie", "BAT", "#FF3FD68B"),
        _ => ("Composant", "SYS", "#FF8B98A9"),
    };
}
