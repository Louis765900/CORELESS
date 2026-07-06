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

    public void Refresh()
    {
        Value = SensorFormat.Format(Type, _sensor.Value);
        Min = SensorFormat.Format(Type, _sensor.Min);
        Max = SensorFormat.Format(Type, _sensor.Max);
        OnPropertyChanged(nameof(Raw));
    }
}
