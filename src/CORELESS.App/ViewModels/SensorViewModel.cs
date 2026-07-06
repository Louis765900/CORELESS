using System.Windows.Media;
using Coreless.Models;
using Coreless.Mvvm;
using LibreHardwareMonitor.Hardware;

namespace Coreless.ViewModels;

/// <summary>Live view of a single hardware sensor (temperature, clock, load…).</summary>
public sealed class SensorViewModel : ObservableObject
{
    private readonly ISensor _sensor;

    public SensorViewModel(ISensor sensor)
    {
        _sensor = sensor;
        Name = sensor.Name;
        Unit = SensorFormat.Unit(sensor.SensorType);
        Type = sensor.SensorType;
    }

    public string Name { get; }
    public string Unit { get; }
    public SensorType Type { get; }

    public float? Raw => _sensor.Value;

    private string _value = "—";
    public string Value { get => _value; private set => SetProperty(ref _value, value); }

    private string _min = "—";
    public string Min { get => _min; private set => SetProperty(ref _min, value); }

    private string _max = "—";
    public string Max { get => _max; private set => SetProperty(ref _max, value); }

    private static readonly Brush HotBrush = Freeze(Color.FromRgb(0xE4, 0x00, 0x2B));
    private static readonly Brush NormalBrush = Freeze(Color.FromRgb(0xF2, 0xF3, 0xF5));

    private Brush _valueBrush = NormalBrush;
    public Brush ValueBrush { get => _valueBrush; private set => SetProperty(ref _valueBrush, value); }

    public void Refresh()
    {
        Value = SensorFormat.Format(Type, _sensor.Value);
        Min = SensorFormat.Format(Type, _sensor.Min);
        Max = SensorFormat.Format(Type, _sensor.Max);
        // highlight hot temperatures in red
        bool hot = Type == SensorType.Temperature && _sensor.Value is float t && !float.IsNaN(t) && t >= 60f;
        ValueBrush = hot ? HotBrush : NormalBrush;
        OnPropertyChanged(nameof(Raw));
    }

    private static Brush Freeze(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
