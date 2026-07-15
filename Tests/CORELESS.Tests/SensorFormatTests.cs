using Coreless.Models;
using LibreHardwareMonitor.Hardware;
using Xunit;

namespace Coreless.Tests;

// Format() uses the current culture's number formatting (decimal separator varies:
// "74.0" on en-US, "74,0" on fr-CA), so expectations are built the same way rather
// than hardcoded, keeping the tests valid regardless of the machine's locale.
public class SensorFormatTests
{
    [Theory]
    [InlineData(SensorType.Temperature, 74.0f, "0.0")]
    [InlineData(SensorType.Load, 33.0f, "0.0")]
    [InlineData(SensorType.Clock, 3800.0f, "0")]
    [InlineData(SensorType.Fan, 1200.0f, "0")]
    [InlineData(SensorType.Voltage, 1.234f, "0.000")]
    public void Format_RendersExpectedPrecisionPerSensorType(SensorType type, float value, string numberFormat)
    {
        Assert.Equal(value.ToString(numberFormat), SensorFormat.Format(type, value));
    }

    [Fact]
    public void Format_NullValue_ReturnsEmDash()
    {
        Assert.Equal("—", SensorFormat.Format(SensorType.Temperature, null));
    }

    [Fact]
    public void Format_NaNValue_ReturnsEmDash()
    {
        Assert.Equal("—", SensorFormat.Format(SensorType.Temperature, float.NaN));
    }

    [Fact]
    public void Format_Throughput_ConvertsBytesPerSecondToMegabytesPerSecond()
    {
        Assert.Equal(1.0f.ToString("0.0"), SensorFormat.Format(SensorType.Throughput, 1_000_000f));
    }

    [Theory]
    [InlineData(SensorType.Fan, "RPM")]
    [InlineData(SensorType.Temperature, "°C")]
    [InlineData(SensorType.Load, "%")]
    public void Unit_ReturnsExpectedSymbol(SensorType type, string expected)
    {
        Assert.Equal(expected, SensorFormat.Unit(type));
    }
}
