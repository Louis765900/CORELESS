using Coreless.Tests.Fakes;
using Coreless.ViewModels;
using LibreHardwareMonitor.Hardware;
using Xunit;

namespace Coreless.Tests;

public class ComponentViewModelTests
{
    [Fact]
    public void HeadlineSensors_PickPreferredNameOverFirstMatch()
    {
        var hw = new FakeHardware("Intel Core i7", HardwareType.Cpu,
            new FakeSensor("Core #1", SensorType.Temperature, 50f),
            new FakeSensor("Package", SensorType.Temperature, 72f));

        var vm = new ComponentViewModel(hw);

        Assert.NotNull(vm.TempHeadline);
        Assert.Equal("Package", vm.TempHeadline!.Name);
    }

    [Fact]
    public void MissingSensorType_HeadlineIsNullAndHasFlagIsFalse_NoCrash()
    {
        var hw = new FakeHardware("Generic disk", HardwareType.Storage,
            new FakeSensor("Used space", SensorType.Load, 42f));

        var vm = new ComponentViewModel(hw);

        Assert.Null(vm.TempHeadline);
        Assert.False(vm.HasTemp);
        vm.Refresh(); // must not throw when a headline sensor is absent
    }

    [Fact]
    public void NoSensorsAndNoSubHardware_HasSensorsIsFalse()
    {
        var hw = new FakeHardware("Empty node", HardwareType.Cooler);

        var vm = new ComponentViewModel(hw);

        Assert.False(vm.HasSensors);
        vm.Refresh(); // must not throw with an empty sensor list
    }

    [Fact]
    public void SubHardwareSensors_AreAggregatedIntoSensorCount()
    {
        var child = new FakeHardware("GPU core", HardwareType.GpuNvidia,
            new FakeSensor("GPU Core", SensorType.Temperature, 65f));
        var parent = new FakeHardware("GPU", HardwareType.GpuNvidia,
            new FakeSensor("Hot Spot", SensorType.Temperature, 70f))
        {
            SubHardware = new IHardware[] { child }
        };

        var vm = new ComponentViewModel(parent);

        Assert.Equal(2, vm.SensorCount);
        Assert.Single(vm.SubComponents);
    }
}
