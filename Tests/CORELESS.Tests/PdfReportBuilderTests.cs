using System.IO;
using Coreless.Models;
using Coreless.Services;
using Coreless.Services.Benchmarks;
using Coreless.Tests.Fakes;
using Coreless.ViewModels;
using LibreHardwareMonitor.Hardware;
using QuestPDF.Infrastructure;
using Xunit;

namespace Coreless.Tests;

public class PdfReportBuilderTests
{
    static PdfReportBuilderTests() => QuestPDF.Settings.License = LicenseType.Community;

    [Fact]
    public void Build_WithComponentsAndBenchmarks_ProducesNonEmptyPdf_NoCrash()
    {
        var hw = new FakeHardware("Intel Core i7", HardwareType.Cpu,
            new FakeSensor("Package", SensorType.Temperature, 72f, min: 40f, max: 85f),
            new FakeSensor("Total", SensorType.Load, 55f));
        var vm = new ComponentViewModel(hw);
        vm.Refresh();

        var benches = new List<BenchmarkOutcome>
        {
            new() { Title = "Rendu 3D (CPU)", ScoreLabel = "Score", ScoreValue = "32", ScoreUnit = "Mpx/s", Score = 32, Category = BenchCategory.Cpu },
            new() { Title = "★ Indice CORELESS (global)", ScoreLabel = "Score global", ScoreValue = "1024", ScoreUnit = "pts", Score = 1024, Category = BenchCategory.Composite },
        };

        string path = Path.Combine(Path.GetTempPath(), $"coreless-test-{Guid.NewGuid():N}.pdf");
        try
        {
            PdfReportBuilder.Build(path, "DESKTOP-TEST", "Windows 11",
                new[] { new InfoItem("Processeur", "Intel Core i7") }, new[] { vm }, benches);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
