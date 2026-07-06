using System.Diagnostics;
using Coreless.Models;

namespace Coreless.Services.Benchmarks;

/// <summary>
/// Memory bandwidth benchmark. Measures sequential write, read and copy throughput
/// over large arrays; reports best (peak) GB/s of several passes.
/// </summary>
public static class MemoryBenchmark
{
    private const int Count = 16 * 1024 * 1024; // 16M longs = 128 MB per array
    private const int Passes = 6;
    private const long Bytes = (long)Count * sizeof(long);

    private static long _sink;

    public static async Task<BenchmarkOutcome> RunAsync(IProgress<double> progress, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            long[] a = new long[Count];
            long[] b = new long[Count];
            for (int i = 0; i < Count; i++) a[i] = i;

            double write = 0, read = 0, copy = 0;
            var sw = new Stopwatch();

            for (int p = 0; p < Passes; p++)
            {
                ct.ThrowIfCancellationRequested();

                // WRITE
                sw.Restart();
                long v = p + 1;
                for (int i = 0; i < Count; i++) a[i] = v;
                sw.Stop();
                write = Math.Max(write, Gbps(Bytes, sw.Elapsed));

                // READ
                sw.Restart();
                long sum = 0;
                for (int i = 0; i < Count; i++) sum += a[i];
                sw.Stop();
                _sink += sum;
                read = Math.Max(read, Gbps(Bytes, sw.Elapsed));

                // COPY
                sw.Restart();
                Array.Copy(a, b, Count);
                sw.Stop();
                copy = Math.Max(copy, Gbps(Bytes, sw.Elapsed));

                progress.Report((p + 1) / (double)Passes);
            }

            return new BenchmarkOutcome
            {
                Title = "Mémoire vive",
                ScoreLabel = "Bande passante (copie)",
                ScoreValue = copy.ToString("0.0"),
                ScoreUnit = "Go/s",
                Details =
                {
                    new InfoItem("Lecture", $"{read:0.0} Go/s"),
                    new InfoItem("Écriture", $"{write:0.0} Go/s"),
                    new InfoItem("Copie", $"{copy:0.0} Go/s"),
                    new InfoItem("Bloc testé", $"{Bytes / (1024 * 1024)} Mo × 2"),
                    new InfoItem("Passes", Passes.ToString()),
                }
            };
        }, ct);
    }

    private static double Gbps(long bytes, TimeSpan t)
        => t.TotalSeconds <= 0 ? 0 : bytes / t.TotalSeconds / 1_000_000_000.0;
}
