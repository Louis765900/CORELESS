using System.Diagnostics;
using Coreless.Models;

namespace Coreless.Services.Benchmarks;

/// <summary>
/// Memory suite (AIDA64-style): peak read / write / copy bandwidth, access latency via a
/// dependent pointer-chase, and a pattern integrity test (write / verify well-known bit
/// patterns to detect errors). The integrity test is limited to a user-space buffer —
/// it is NOT a full-RAM test like MemTest86 (which must run outside the OS).
/// </summary>
public static class RamSuiteBenchmark
{
    private const int Count = 32 * 1024 * 1024;       // 32M longs = 256 MB per array
    private const long Bytes = (long)Count * sizeof(long);
    private const int Passes = 5;
    private const int LatencyHops = 64 * 1024 * 1024;  // dependent accesses to average over
    private const int PatternMb = 512;                 // integrity-test buffer size

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

                sw.Restart();
                long v = p + 1;
                for (int i = 0; i < Count; i++) a[i] = v;
                sw.Stop();
                write = Math.Max(write, Gbps(Bytes, sw.Elapsed));

                sw.Restart();
                long sum = 0;
                for (int i = 0; i < Count; i++) sum += a[i];
                sw.Stop();
                _sink += sum;
                read = Math.Max(read, Gbps(Bytes, sw.Elapsed));

                sw.Restart();
                Array.Copy(a, b, Count);
                sw.Stop();
                copy = Math.Max(copy, Gbps(Bytes, sw.Elapsed));

                progress.Report(0.55 * (p + 1) / Passes);
            }

            double latencyNs = MeasureLatency(ct);
            progress.Report(0.78);

            (long errors, long testedBytes) = PatternTest(ct, progress);
            progress.Report(1.0);

            return new BenchmarkOutcome
            {
                Title = "Mémoire — suite complète",
                ScoreLabel = "Bande passante (copie)",
                ScoreValue = copy.ToString("0.0"),
                ScoreUnit = "Go/s",
                Score = copy,
                Category = BenchCategory.Memory,
                Details =
                {
                    new InfoItem("Lecture", $"{read:0.0} Go/s"),
                    new InfoItem("Écriture", $"{write:0.0} Go/s"),
                    new InfoItem("Copie", $"{copy:0.0} Go/s"),
                    new InfoItem("Latence d'accès", $"{latencyNs:0.0} ns"),
                    new InfoItem("Test d'intégrité", errors == 0
                        ? $"0 erreur — {testedBytes / (1024 * 1024)} Mo OK"
                        : $"⚠ {errors:N0} ERREURS sur {testedBytes / (1024 * 1024)} Mo"),
                }
            };
        }, ct);
    }

    // Dependent pointer-chase over a randomized cycle: each read address depends on the
    // previous value, defeating prefetch — the classic memory-latency measurement.
    private static double MeasureLatency(CancellationToken ct)
    {
        int n = 8 * 1024 * 1024; // 64 MB of indices, larger than any L3
        int[] next = new int[n];
        for (int i = 0; i < n; i++) next[i] = i;
        var rng = new Random(9973);
        for (int i = n - 1; i > 0; i--) // Fisher-Yates to build one big permutation
        {
            int j = rng.Next(i + 1);
            (next[i], next[j]) = (next[j], next[i]);
        }

        // warm up
        int idx = 0;
        for (int i = 0; i < n; i++) idx = next[idx];
        _sink += idx;

        ct.ThrowIfCancellationRequested();
        var sw = Stopwatch.StartNew();
        idx = 0;
        for (int i = 0; i < LatencyHops; i++) idx = next[idx];
        sw.Stop();
        _sink += idx;
        return sw.Elapsed.TotalMilliseconds * 1_000_000.0 / LatencyHops; // ns per hop
    }

    // Write / read-back well-known patterns; count mismatches. Zero on healthy RAM.
    private static (long errors, long testedBytes) PatternTest(CancellationToken ct, IProgress<double> progress)
    {
        int longs = PatternMb * 1024 * 1024 / sizeof(long);
        long[] buf = new long[longs];
        ulong[] patterns = { 0x0000000000000000UL, 0xFFFFFFFFFFFFFFFFUL,
                             0xAAAAAAAAAAAAAAAAUL, 0x5555555555555555UL,
                             0xDEADBEEFCAFEBABEUL };
        long errors = 0;
        for (int p = 0; p < patterns.Length; p++)
        {
            ct.ThrowIfCancellationRequested();
            long pat = unchecked((long)patterns[p]);
            for (int i = 0; i < longs; i++) buf[i] = pat;
            for (int i = 0; i < longs; i++) if (buf[i] != pat) errors++;
            progress.Report(0.78 + 0.22 * (p + 1) / patterns.Length);
        }
        _sink += buf[0];
        return (errors, (long)longs * sizeof(long) * patterns.Length);
    }

    private static double Gbps(long bytes, TimeSpan t)
        => t.TotalSeconds <= 0 ? 0 : bytes / t.TotalSeconds / 1_000_000_000.0;
}
