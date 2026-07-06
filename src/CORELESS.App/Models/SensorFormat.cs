using LibreHardwareMonitor.Hardware;

namespace Coreless.Models;

/// <summary>Formatting helpers turning raw sensor values into human-friendly strings + units.</summary>
public static class SensorFormat
{
    public static string Unit(SensorType type) => type switch
    {
        SensorType.Voltage => "V",
        SensorType.Current => "A",
        SensorType.Power => "W",
        SensorType.Clock => "MHz",
        SensorType.Temperature => "°C",
        SensorType.Load => "%",
        SensorType.Frequency => "Hz",
        SensorType.Fan => "RPM",
        SensorType.Flow => "L/h",
        SensorType.Control => "%",
        SensorType.Level => "%",
        SensorType.Factor => "",
        SensorType.Data => "GB",
        SensorType.SmallData => "MB",
        SensorType.Throughput => "MB/s",
        SensorType.Energy => "mWh",
        SensorType.Noise => "dBA",
        SensorType.TimeSpan => "s",
        _ => ""
    };

    public static string Format(SensorType type, float? value)
    {
        if (value is null || float.IsNaN(value.Value)) return "—";
        float v = value.Value;

        return type switch
        {
            SensorType.Throughput => (v / 1_000_000f).ToString("0.0"), // B/s -> MB/s
            SensorType.Clock => v.ToString("0"),
            SensorType.Fan => v.ToString("0"),
            SensorType.Load or SensorType.Control or SensorType.Level => v.ToString("0.0"),
            SensorType.Temperature => v.ToString("0.0"),
            SensorType.Voltage => v.ToString("0.000"),
            SensorType.Power => v.ToString("0.0"),
            SensorType.Data => v.ToString("0.00"),
            SensorType.SmallData => v.ToString("0"),
            _ => v.ToString("0.0")
        };
    }

    /// <summary>A short group label per sensor type, used to organise the detailed sensor list.</summary>
    public static string GroupLabel(SensorType type) => type switch
    {
        SensorType.Temperature => "Températures",
        SensorType.Load => "Charge",
        SensorType.Clock => "Fréquences",
        SensorType.Voltage => "Tensions",
        SensorType.Power => "Puissance",
        SensorType.Current => "Courant",
        SensorType.Fan => "Ventilateurs",
        SensorType.Control => "Contrôle",
        SensorType.Data => "Données",
        SensorType.SmallData => "Données",
        SensorType.Throughput => "Débit",
        SensorType.Level => "Niveaux",
        SensorType.Frequency => "Fréquences",
        SensorType.Factor => "Facteurs",
        SensorType.Energy => "Énergie",
        SensorType.Noise => "Bruit",
        SensorType.TimeSpan => "Durée",
        SensorType.Flow => "Flux",
        _ => "Autres"
    };
}
