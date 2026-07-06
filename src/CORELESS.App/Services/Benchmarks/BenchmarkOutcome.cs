using Coreless.Models;

namespace Coreless.Services.Benchmarks;

/// <summary>Result of one benchmark run: a headline score plus detail rows.</summary>
public sealed class BenchmarkOutcome
{
    public required string Title { get; init; }
    public required string ScoreLabel { get; init; }
    public required string ScoreValue { get; init; }
    public required string ScoreUnit { get; init; }
    public List<InfoItem> Details { get; init; } = new();
    public DateTime Timestamp { get; } = DateTime.Now;
}
