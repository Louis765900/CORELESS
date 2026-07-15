using System.Diagnostics;
using Coreless.Models;

namespace Coreless.Services.Benchmarks;

/// <summary>
/// CPU throughput benchmark. Runs a fixed FP+integer kernel single-threaded then across
/// all logical cores for a fixed window; score = kernel blocks completed per second.
/// </summary>
public static class CpuBenchmark
{
    private const int BlockSize = 200_000;   // FP/int ops per block
    private const int PhaseMs = 5000;        // duration per phase

    // Static sink prevents the JIT from eliminating the kernel as dead code.
    private static double _sink;

    public static async Task<BenchmarkOutcome> RunAsync(IProgress<double> progress, CancellationToken ct)
    {
        int cores = Environment.ProcessorCount;

        Task<long> single = Task.Run(() => RunPhase(1, PhaseMs, ct), ct);
        await Monitor(single, PhaseMs, 0.0, 0.5, progress, ct);
        long singleBlocks = await single;

        Task<long> multi = Task.Run(() => RunPhase(cores, PhaseMs, ct), ct);
        await Monitor(multi, PhaseMs, 0.5, 0.5, progress, ct);
        long multiBlocks = await multi;

        double seconds = PhaseMs / 1000.0;
        double singleScore = singleBlocks / seconds;
        double multiScore = multiBlocks / seconds;
        double scaling = singleScore > 0 ? multiScore / singleScore : 0;

        return new BenchmarkOutcome
        {
            Title = "CPU débit (ALU/FPU)",
            ScoreLabel = "Score multi-cœur",
            ScoreValue = Math.Round(multiScore).ToString("N0"),
            ScoreUnit = "blocs/s",
            Details =
            {
                new InfoItem("Score mono-cœur", $"{Math.Round(singleScore):N0} blocs/s"),
                new InfoItem("Score multi-cœur", $"{Math.Round(multiScore):N0} blocs/s"),
                new InfoItem("Gain multi-cœur", $"×{scaling:0.0} sur {cores} threads"),
                new InfoItem("Efficacité/cœur", $"{scaling / cores * 100:0} %"),
                new InfoItem("Durée", $"{seconds * 2:0} s (2 phases)"),
            }
        };
    }

    private static long RunPhase(int threads, int ms, CancellationToken ct)
    {
        long total = 0;
        var sw = Stopwatch.StartNew();
        var opts = new ParallelOptions { MaxDegreeOfParallelism = threads };
        Parallel.For(0, threads, opts, t =>
        {
            ulong seed = (ulong)(t + 1) * 2654435761UL;
            long local = 0;
            double acc = 0;
            while (sw.ElapsedMilliseconds < ms && !ct.IsCancellationRequested)
            {
                acc += Kernel(seed + (ulong)local);
                local++;
            }
            _sink += acc;
            Interlocked.Add(ref total, local);
        });
        return total;
    }

    private static double Kernel(ulong seed)
    {
        double acc = 0;
        ulong s = seed | 1;
        for (int i = 0; i < BlockSize; i++)
        {
            // integer mixing (xorshift) feeds FP work — exercises ALU + FPU
            s ^= s << 13; s ^= s >> 7; s ^= s << 17;
            double x = (s & 0xFFFF) * 1e-3 + 1.0;
            acc += Math.Sqrt(x) * Math.Sin(x) + x * 0.5;
        }
        return acc;
    }

    private static async Task Monitor(Task task, int totalMs, double baseP, double span,
                                      IProgress<double> progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            double frac = Math.Min(1.0, sw.ElapsedMilliseconds / (double)totalMs);
            progress.Report(baseP + span * frac);
            try { await Task.Delay(100, ct); } catch (OperationCanceledException) { break; }
        }
        progress.Report(baseP + span);
    }
}
