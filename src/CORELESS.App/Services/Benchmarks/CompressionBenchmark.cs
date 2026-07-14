using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Coreless.Models;

namespace Coreless.Services.Benchmarks;

/// <summary>
/// Data-compression benchmark (7-Zip-style): generates a realistic, semi-compressible
/// dataset then compresses and decompresses it in parallel across all cores with Deflate.
/// Score = aggregate compression throughput in MB/s (input bytes / wall time).
/// Built-in <see cref="DeflateStream"/> — no external dependency.
/// </summary>
public static class CompressionBenchmark
{
    private const int ChunkBytes = 4 * 1024 * 1024; // 4 MB per work unit

    public static async Task<BenchmarkOutcome> RunAsync(IProgress<double> progress, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            int cores = Environment.ProcessorCount;
            int chunks = Math.Max(cores * 2, 8);
            long totalBytes = (long)chunks * ChunkBytes;

            // Build one shared, semi-compressible chunk (mix of structured text + noise).
            byte[] source = BuildDataset(ChunkBytes);
            progress.Report(0.10);

            var opts = new ParallelOptions { MaxDegreeOfParallelism = cores };
            byte[][] compressed = new byte[chunks][];
            int doneC = 0;

            // COMPRESS
            var sw = Stopwatch.StartNew();
            Parallel.For(0, chunks, opts, i =>
            {
                ct.ThrowIfCancellationRequested();
                using var ms = new MemoryStream(ChunkBytes / 2);
                using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                    ds.Write(source, 0, source.Length);
                compressed[i] = ms.ToArray();
                int d = Interlocked.Increment(ref doneC);
                progress.Report(0.10 + 0.55 * (d / (double)chunks));
            });
            sw.Stop();
            double compMbps = totalBytes / sw.Elapsed.TotalSeconds / (1024 * 1024);
            long compressedBytes = compressed.Sum(c => (long)c.Length);
            double ratio = totalBytes / (double)compressedBytes;

            // DECOMPRESS
            int doneD = 0;
            sw.Restart();
            Parallel.For(0, chunks, opts, i =>
            {
                ct.ThrowIfCancellationRequested();
                byte[] outBuf = new byte[ChunkBytes];
                using var ms = new MemoryStream(compressed[i]);
                using var ds = new DeflateStream(ms, CompressionMode.Decompress);
                int off = 0, r;
                while (off < outBuf.Length && (r = ds.Read(outBuf, off, outBuf.Length - off)) > 0) off += r;
                int d = Interlocked.Increment(ref doneD);
                progress.Report(0.65 + 0.35 * (d / (double)chunks));
            });
            sw.Stop();
            double decompMbps = totalBytes / sw.Elapsed.TotalSeconds / (1024 * 1024);

            progress.Report(1.0);
            return new BenchmarkOutcome
            {
                Title = "Compression (Deflate)",
                ScoreLabel = "Débit de compression",
                ScoreValue = compMbps.ToString("N0"),
                ScoreUnit = "Mo/s",
                Score = compMbps,
                Category = BenchCategory.Cpu,
                Details =
                {
                    new InfoItem("Compression", $"{compMbps:N0} Mo/s"),
                    new InfoItem("Décompression", $"{decompMbps:N0} Mo/s"),
                    new InfoItem("Taux de compression", $"{ratio:0.00}×"),
                    new InfoItem("Données traitées", $"{totalBytes / (1024 * 1024)} Mo sur {cores} threads"),
                    new InfoItem("Niveau", "Deflate — Optimal"),
                }
            };
        }, ct);
    }

    // Semi-compressible: repeating dictionary words + pseudo-random bytes, ~2-3x ratio,
    // representative of mixed real-world data rather than pure text or pure noise.
    private static byte[] BuildDataset(int size)
    {
        byte[] buf = new byte[size];
        string[] words = { "core", "less", "hardware", "monitor", "benchmark", "thermal",
                           "throttle", "kernel", "sensor", "voltage", "frequency", "cache" };
        var rng = new Random(1337);
        int pos = 0;
        while (pos < size)
        {
            if (rng.Next(3) == 0)
            {
                // noise run
                int n = Math.Min(rng.Next(16, 64), size - pos);
                for (int i = 0; i < n; i++) buf[pos++] = (byte)rng.Next(256);
            }
            else
            {
                byte[] w = System.Text.Encoding.ASCII.GetBytes(words[rng.Next(words.Length)] + " ");
                int n = Math.Min(w.Length, size - pos);
                Array.Copy(w, 0, buf, pos, n);
                pos += n;
            }
        }
        return buf;
    }
}
