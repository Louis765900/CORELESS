using LibreHardwareMonitor.Hardware;

namespace Coreless.Tests.Fakes;

/// <summary>Minimal IHardware/ISensor stand-ins so ViewModels can be tested without real sensors.</summary>
public sealed class FakeHardware : IHardware
{
    public FakeHardware(string name, HardwareType type, params FakeSensor[] sensors)
    {
        Name = name;
        HardwareType = type;
        Sensors = sensors;
        foreach (FakeSensor s in sensors) s.Hardware = this;
    }

    public string Name { get; set; }
    public Identifier Identifier { get; } = new("fake");
    public HardwareType HardwareType { get; }
    public IHardware? Parent { get; set; }
    public ISensor[] Sensors { get; }
    public IHardware[] SubHardware { get; set; } = Array.Empty<IHardware>();
    public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

    public string GetReport() => string.Empty;
    public void Update() { }
    public void Accept(IVisitor visitor) { }
    public void Traverse(IVisitor visitor) { }

    public event SensorEventHandler? SensorAdded;
    public event SensorEventHandler? SensorRemoved;
}

public sealed class FakeSensor : ISensor
{
    public FakeSensor(string name, SensorType type, float? value, float? min = null, float? max = null)
    {
        Name = name;
        SensorType = type;
        Value = value;
        Min = min ?? value;
        Max = max ?? value;
    }

    public IControl? Control => null;
    public IHardware Hardware { get; set; } = null!;
    public Identifier Identifier { get; } = new("fake-sensor");
    public int Index { get; set; }
    public bool IsDefaultHidden { get; set; }
    public float? Max { get; set; }
    public float? Min { get; set; }
    public string Name { get; set; }
    public IReadOnlyList<IParameter> Parameters { get; } = Array.Empty<IParameter>();
    public SensorType SensorType { get; }
    public float? Value { get; set; }
    public IEnumerable<SensorValue> Values { get; } = Array.Empty<SensorValue>();
    public TimeSpan ValuesTimeWindow { get; set; }

    public void ResetMin() => Min = Value;
    public void ResetMax() => Max = Value;
    public void ClearValues() { }
    public void Accept(IVisitor visitor) { }
    public void Traverse(IVisitor visitor) { }
}
