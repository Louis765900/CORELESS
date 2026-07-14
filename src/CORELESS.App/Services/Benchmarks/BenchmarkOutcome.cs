using Coreless.Models;

namespace Coreless.Services.Benchmarks;

/// <summary>Which subsystem a benchmark exercises (used for the global index).</summary>
public enum BenchCategory { Cpu, Memory, Disk, Gpu, Composite }

/// <summary>Result of one benchmark run: a headline score plus detail rows.</summary>
public sealed class BenchmarkOutcome
{
    public required string Title { get; init; }
    public required string ScoreLabel { get; init; }
    public required string ScoreValue { get; init; }
    public required string ScoreUnit { get; init; }
    public List<InfoItem> Details { get; init; } = new();
    public DateTime Timestamp { get; } = DateTime.Now;

    /// <summary>Raw comparable number fed into the global CORELESS index (0 = not scored).</summary>
    public double Score { get; init; }
    public BenchCategory Category { get; init; } = BenchCategory.Composite;
}
