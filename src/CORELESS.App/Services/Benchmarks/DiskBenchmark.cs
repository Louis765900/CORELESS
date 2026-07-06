using System.Diagnostics;
using System.IO;
using Coreless.Models;

namespace Coreless.Services.Benchmarks;

/// <summary>
/// Storage benchmark on the temp-path drive: sequential write, sequential read,
/// and 4K random-read IOPS. Writes then deletes a temporary test file.
/// </summary>
public static class DiskBenchmark
{
    private const long FileSize = 256L * 1024 * 1024; // 256 MB
    private const int ChunkSize = 1 * 1024 * 1024;    // 1 MB sequential chunk
    private const int RandomBlock = 4 * 1024;         // 4 KB random block
    private const int RandomMs = 3000;

    private static long _sink;

    public static async Task<BenchmarkOutcome> RunAsync(IProgress<double> progress, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            string dir = Path.GetTempPath();
            string path = Path.Combine(dir, $"coreless_bench_{Guid.NewGuid():N}.tmp");
            string drive = Path.GetPathRoot(dir) ?? dir;
            var rng = new Random();
            var sw = new Stopwatch();

            double writeMb, readMb;
            long iops;

            try
            {
                byte[] buf = new byte[ChunkSize];
                rng.NextBytes(buf);
                long chunks = FileSize / ChunkSize;

                // SEQUENTIAL WRITE
                sw.Restart();
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, ChunkSize))
                {
                    for (long i = 0; i < chunks; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        fs.Write(buf, 0, ChunkSize);
                        if ((i & 0x1F) == 0) progress.Report(0.45 * (i / (double)chunks));
                    }
                    fs.Flush(true); // force to physical disk
                }
                sw.Stop();
                writeMb = MbPerSec(FileSize, sw.Elapsed);
                progress.Report(0.45);

                // SEQUENTIAL READ
                sw.Restart();
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, ChunkSize,
                           FileOptions.SequentialScan))
                {
                    int read; long done = 0;
                    while ((read = fs.Read(buf, 0, ChunkSize)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        _sink += buf[0];
                        done += read;
                        if ((done & (32L * 1024 * 1024 - 1)) < ChunkSize)
                            progress.Report(0.45 + 0.35 * (done / (double)FileSize));
                    }
                }
                sw.Stop();
                readMb = MbPerSec(FileSize, sw.Elapsed);
                progress.Report(0.80);

                // 4K RANDOM READ IOPS
                byte[] small = new byte[RandomBlock];
                long ops = 0;
                long maxOffset = FileSize - RandomBlock;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, RandomBlock,
                           FileOptions.RandomAccess))
                {
                    sw.Restart();
                    while (sw.ElapsedMilliseconds < RandomMs)
                    {
                        ct.ThrowIfCancellationRequested();
                        long off = (long)(rng.NextDouble() * maxOffset) & ~0xFFF;
                        fs.Seek(off, SeekOrigin.Begin);
                        _sink += fs.Read(small, 0, RandomBlock);
                        ops++;
                    }
                    sw.Stop();
                }
                iops = (long)(ops / sw.Elapsed.TotalSeconds);
                progress.Report(1.0);
            }
            finally
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
            }

            return new BenchmarkOutcome
            {
                Title = "Stockage",
                ScoreLabel = "Lecture séquentielle",
                ScoreValue = readMb.ToString("N0"),
                ScoreUnit = "Mo/s",
                Details =
                {
                    new InfoItem("Lecture séquentielle", $"{readMb:N0} Mo/s"),
                    new InfoItem("Écriture séquentielle", $"{writeMb:N0} Mo/s"),
                    new InfoItem("IOPS aléatoire 4K", $"{iops:N0} IOPS"),
                    new InfoItem("Volume testé", $"{FileSize / (1024 * 1024)} Mo"),
                    new InfoItem("Lecteur", drive),
                }
            };
        }, ct);
    }

    private static double MbPerSec(long bytes, TimeSpan t)
        => t.TotalSeconds <= 0 ? 0 : bytes / t.TotalSeconds / (1024 * 1024);
}
