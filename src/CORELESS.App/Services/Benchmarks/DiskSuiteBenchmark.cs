using System.Diagnostics;
using System.IO;
using Microsoft.Win32.SafeHandles;
using Coreless.Models;

namespace Coreless.Services.Benchmarks;

/// <summary>
/// CrystalDiskMark-style storage suite on the temp drive. Measures four classic profiles:
/// SEQ1M Q8T1, SEQ1M Q1T1, RND4K Q32T1, RND4K Q1T1 (read + a sequential write pass).
/// Queue depth is emulated with overlapped asynchronous positioned I/O via <see cref="RandomAccess"/>.
/// Writes then deletes a temp file.
/// </summary>
public static class DiskSuiteBenchmark
{
    private const long FileSize = 512L * 1024 * 1024; // 512 MB working set
    private const int Seq = 1024 * 1024;              // 1 MiB sequential block
    private const int Rnd = 4 * 1024;                 // 4 KiB random block
    private const int RandomMs = 3000;

    private static long _sink;

    public static async Task<BenchmarkOutcome> RunAsync(IProgress<double> progress, CancellationToken ct)
    {
        string dir = Path.GetTempPath();
        string path = Path.Combine(dir, $"coreless_cdm_{Guid.NewGuid():N}.tmp");
        string drive = Path.GetPathRoot(dir) ?? dir;

        double seqWrite, seqReadQ8, seqReadQ1, rnd4kQ1Mb, rnd4kQ32Mb;
        long rnd4kQ1Iops, rnd4kQ32Iops;

        try
        {
            seqWrite = await SeqWrite(path, ct, progress);
            progress.Report(0.35);

            seqReadQ8 = await SeqRead(path, queueDepth: 8, ct);
            progress.Report(0.55);

            seqReadQ1 = await SeqRead(path, queueDepth: 1, ct);
            progress.Report(0.70);

            (rnd4kQ32Iops, rnd4kQ32Mb) = await RandomRead(path, queueDepth: 32, ct);
            progress.Report(0.88);

            (rnd4kQ1Iops, rnd4kQ1Mb) = await RandomRead(path, queueDepth: 1, ct);
            progress.Report(1.0);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
        }

        return new BenchmarkOutcome
        {
            Title = "Stockage — suite CDM",
            ScoreLabel = "SEQ1M Q8T1 lecture",
            ScoreValue = seqReadQ8.ToString("N0"),
            ScoreUnit = "Mo/s",
            Score = seqReadQ8,
            Category = BenchCategory.Disk,
            Details =
            {
                new InfoItem("SEQ1M Q8T1 — lecture", $"{seqReadQ8:N0} Mo/s"),
                new InfoItem("SEQ1M Q1T1 — lecture", $"{seqReadQ1:N0} Mo/s"),
                new InfoItem("SEQ1M Q1T1 — écriture", $"{seqWrite:N0} Mo/s"),
                new InfoItem("RND4K Q32T1 — lecture", $"{rnd4kQ32Mb:N0} Mo/s ({rnd4kQ32Iops:N0} IOPS)"),
                new InfoItem("RND4K Q1T1 — lecture", $"{rnd4kQ1Mb:N1} Mo/s ({rnd4kQ1Iops:N0} IOPS)"),
                new InfoItem("Volume / lecteur", $"{FileSize / (1024 * 1024)} Mo — {drive}"),
            }
        };
    }

    private static async Task<double> SeqWrite(string path, CancellationToken ct, IProgress<double> progress)
    {
        byte[] buf = new byte[Seq];
        new Random().NextBytes(buf);
        long chunks = FileSize / Seq;

        var sw = Stopwatch.StartNew();
        using (SafeFileHandle h = File.OpenHandle(path, FileMode.Create, FileAccess.Write, FileShare.None,
                   FileOptions.Asynchronous, FileSize))
        {
            for (long i = 0; i < chunks; i++)
            {
                ct.ThrowIfCancellationRequested();
                await RandomAccess.WriteAsync(h, buf.AsMemory(0, Seq), i * Seq, ct);
                if ((i & 0x1F) == 0) progress.Report(0.35 * (i / (double)chunks));
            }
            RandomAccess.FlushToDisk(h);
        }
        sw.Stop();
        return MbPerSec(FileSize, sw.Elapsed);
    }

    private static async Task<double> SeqRead(string path, int queueDepth, CancellationToken ct)
    {
        long chunks = FileSize / Seq;
        using SafeFileHandle h = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            FileOptions.Asynchronous);

        var buffers = new byte[queueDepth][];
        for (int i = 0; i < queueDepth; i++) buffers[i] = new byte[Seq];

        var sw = Stopwatch.StartNew();
        // Keep `queueDepth` reads in flight over successive 1 MiB regions.
        var inflight = new List<Task<int>>(queueDepth);
        long next = 0;
        for (int i = 0; i < queueDepth && next < chunks; i++, next++)
            inflight.Add(ReadAt(h, next * Seq, buffers[i], ct));

        while (inflight.Count > 0)
        {
            Task<int> done = await Task.WhenAny(inflight);
            int idx = inflight.IndexOf(done);
            _sink += done.Result;
            if (next < chunks)
            {
                inflight[idx] = ReadAt(h, next * Seq, buffers[idx], ct);
                next++;
            }
            else
            {
                inflight.RemoveAt(idx);
            }
            ct.ThrowIfCancellationRequested();
        }
        sw.Stop();
        return MbPerSec(FileSize, sw.Elapsed);
    }

    private static async Task<(long iops, double mbps)> RandomRead(string path, int queueDepth, CancellationToken ct)
    {
        long maxOffset = FileSize - Rnd;
        var rng = new Random();
        using SafeFileHandle h = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            FileOptions.Asynchronous | FileOptions.RandomAccess);

        var buffers = new byte[queueDepth][];
        for (int i = 0; i < queueDepth; i++) buffers[i] = new byte[Rnd];

        var sw = Stopwatch.StartNew();
        long ops = 0;
        var inflight = new List<Task<int>>(queueDepth);
        for (int i = 0; i < queueDepth; i++)
            inflight.Add(ReadAt(h, RandOffset(rng, maxOffset), buffers[i], ct));

        while (sw.ElapsedMilliseconds < RandomMs && !ct.IsCancellationRequested)
        {
            Task<int> done = await Task.WhenAny(inflight);
            int idx = inflight.IndexOf(done);
            _sink += done.Result;
            ops++;
            inflight[idx] = ReadAt(h, RandOffset(rng, maxOffset), buffers[idx], ct);
        }
        sw.Stop();

        double sec = sw.Elapsed.TotalSeconds;
        long iops = sec > 0 ? (long)(ops / sec) : 0;
        double mbps = sec > 0 ? (double)ops * Rnd / sec / (1024 * 1024) : 0;
        return (iops, mbps);
    }

    private static ValueTask<int> ReadAtCore(SafeFileHandle h, long offset, byte[] buffer, CancellationToken ct)
        => RandomAccess.ReadAsync(h, buffer, offset, ct);

    // Wrap the ValueTask in a Task so it can live in a List and be awaited by Task.WhenAny.
    private static Task<int> ReadAt(SafeFileHandle h, long offset, byte[] buffer, CancellationToken ct)
        => ReadAtCore(h, offset, buffer, ct).AsTask();

    private static long RandOffset(Random rng, long maxOffset)
        => (long)(rng.NextDouble() * maxOffset) & ~0xFFFL;

    private static double MbPerSec(long bytes, TimeSpan t)
        => t.TotalSeconds <= 0 ? 0 : bytes / t.TotalSeconds / (1024 * 1024);
}
