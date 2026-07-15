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
    public void NetworkAdapter_WithVirtualDriverName_IsSuspectedInactive()
    {
        var hw = new FakeHardware("Ethernet-Npcap Packet Driver", HardwareType.Network,
            new FakeSensor("Débit", SensorType.Throughput, 0f));

        var vm = new ComponentViewModel(hw);

        Assert.True(vm.IsSuspectedInactiveNetwork);
    }

    [Fact]
    public void NetworkAdapter_NeverNonZero_IsSuspectedInactive()
    {
        var sensor = new FakeSensor("Débit", SensorType.Throughput, 0f);
        var hw = new FakeHardware("Realtek PCIe GbE", HardwareType.Network, sensor);
        var vm = new ComponentViewModel(hw);

        vm.Refresh();
        vm.Refresh();

        Assert.True(vm.IsSuspectedInactiveNetwork);
    }

    [Fact]
    public void NetworkAdapter_SeenNonZeroOnce_IsNoLongerSuspectedInactive()
    {
        var sensor = new FakeSensor("Débit", SensorType.Throughput, 0f);
        var hw = new FakeHardware("Realtek PCIe GbE", HardwareType.Network, sensor);
        var vm = new ComponentViewModel(hw);

        vm.Refresh();
        sensor.Value = 5_000_000f;
        vm.Refresh();

        Assert.False(vm.IsSuspectedInactiveNetwork);
    }

    [Fact]
    public void NonNetworkComponent_IsNeverSuspectedInactive()
    {
        var hw = new FakeHardware("Intel Core i7", HardwareType.Cpu,
            new FakeSensor("Package", SensorType.Temperature, 0f));

        var vm = new ComponentViewModel(hw);

        Assert.False(vm.IsSuspectedInactiveNetwork);
    }

    [Fact]
    public void GpuFanStuckAtZero_WithControlDemandingSpin_ShowsNotAvailable()
    {
        var hw = new FakeHardware("GTX 1650", HardwareType.GpuNvidia,
            new FakeSensor("GPU Fan", SensorType.Fan, 0f),
            new FakeSensor("GPU Fan", SensorType.Control, 34f));

        var vm = new ComponentViewModel(hw);
        vm.Refresh();

        SensorViewModel fanSensor = vm.Groups.SelectMany(g => g.Sensors).Single(s => s.Type == SensorType.Fan);
        Assert.Equal("N/A", fanSensor.Value);
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
