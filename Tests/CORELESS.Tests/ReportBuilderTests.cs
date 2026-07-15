using Coreless.Models;
using Coreless.Services;
using Coreless.Tests.Fakes;
using Coreless.ViewModels;
using LibreHardwareMonitor.Hardware;
using Xunit;

namespace Coreless.Tests;

public class ReportBuilderTests
{
    [Fact]
    public void BuildText_NoComponents_DoesNotCrashAndIncludesHeader()
    {
        string report = ReportBuilder.BuildText("DESKTOP-TEST", "Windows 11", Array.Empty<InfoItem>(),
            Array.Empty<ComponentViewModel>());

        Assert.Contains("DESKTOP-TEST", report);
        Assert.Contains("CORELESS", report);
    }

    [Fact]
    public void BuildText_ComponentWithSensors_ListsNameValueAndUnit()
    {
        var hw = new FakeHardware("Intel Core i7", HardwareType.Cpu,
            new FakeSensor("Package", SensorType.Temperature, 72f, min: 40f, max: 80f));
        var vm = new ComponentViewModel(hw);
        vm.Refresh();

        string report = ReportBuilder.BuildText("DESKTOP-TEST", "Windows 11",
            new[] { new InfoItem("Processeur", "Intel Core i7") }, new[] { vm });

        Assert.Contains("Package", report);
        Assert.Contains(72.0f.ToString("0.0"), report);
        Assert.Contains("°C", report);
    }
}
