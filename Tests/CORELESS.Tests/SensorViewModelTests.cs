using Coreless.Tests.Fakes;
using Coreless.ViewModels;
using LibreHardwareMonitor.Hardware;
using Xunit;

namespace Coreless.Tests;

public class SensorViewModelTests
{
    [Fact]
    public void FanReadsZero_WithControlDemandingSpin_ShowsNotAvailable()
    {
        var fan = new FakeSensor("GPU Fan", SensorType.Fan, 0f);
        var control = new FakeSensor("GPU Fan", SensorType.Control, 34f);

        var vm = new SensorViewModel(fan, control);
        vm.Refresh();

        Assert.Equal("N/A", vm.Value);
    }

    [Fact]
    public void FanReadsZero_WithControlAlsoZero_ShowsRealZero()
    {
        var fan = new FakeSensor("GPU Fan", SensorType.Fan, 0f);
        var control = new FakeSensor("GPU Fan", SensorType.Control, 0f);

        var vm = new SensorViewModel(fan, control);
        vm.Refresh();

        Assert.NotEqual("N/A", vm.Value);
    }

    [Fact]
    public void FanWithoutPairedControl_FormatsNormally()
    {
        var fan = new FakeSensor("CPU Fan", SensorType.Fan, 0f);

        var vm = new SensorViewModel(fan);
        vm.Refresh();

        Assert.NotEqual("N/A", vm.Value);
    }

    [Fact]
    public void FanSpinningNormally_IsUnaffectedByPairedControl()
    {
        var fan = new FakeSensor("GPU Fan", SensorType.Fan, 1500f);
        var control = new FakeSensor("GPU Fan", SensorType.Control, 40f);

        var vm = new SensorViewModel(fan, control);
        vm.Refresh();

        Assert.Equal(1500f.ToString("0"), vm.Value);
    }
}
