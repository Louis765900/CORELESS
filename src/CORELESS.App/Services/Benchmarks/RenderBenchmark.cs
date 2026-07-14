using System.Diagnostics;
using System.Numerics;
using Coreless.Models;

namespace Coreless.Services.Benchmarks;

/// <summary>
/// CPU rendering benchmark (Cinebench-style): a small recursive ray tracer with
/// reflective spheres and soft shadows. Renders full frames repeatedly for a fixed
/// window, single-threaded then across all cores; score = megapixels traced / second.
/// Pure managed math — no GPU, no external dependency.
/// </summary>
public static class RenderBenchmark
{
    private const int W = 320;
    private const int H = 240;
    private const int MaxDepth = 4;
    private const int PhaseMs = 5000;

    private static long _sink;

    private readonly record struct Sphere(Vector3 Center, float Radius, Vector3 Color, float Reflect);

    private static readonly Sphere[] Scene =
    {
        new(new(0, -1000.5f, -3), 1000f, new(0.35f, 0.35f, 0.38f), 0.15f), // floor
        new(new(-1.1f, 0, -3.2f), 0.5f, new(0.90f, 0.10f, 0.14f), 0.35f),  // red
        new(new(0.0f, 0, -2.6f), 0.5f, new(0.85f, 0.85f, 0.88f), 0.75f),   // mirror
        new(new(1.1f, 0, -3.2f), 0.5f, new(0.15f, 0.16f, 0.20f), 0.25f),   // dark
    };
    private static readonly Vector3 Light = Vector3.Normalize(new(-0.6f, 1f, 0.3f));

    public static async Task<BenchmarkOutcome> RunAsync(IProgress<double> progress, CancellationToken ct)
    {
        Task<long> single = Task.Run(() => RenderPhase(1, PhaseMs, ct), ct);
        await Track(single, PhaseMs, 0.0, 0.5, progress, ct);
        long singlePx = await single;

        int cores = Environment.ProcessorCount;
        Task<long> multi = Task.Run(() => RenderPhase(cores, PhaseMs, ct), ct);
        await Track(multi, PhaseMs, 0.5, 0.5, progress, ct);
        long multiPx = await multi;

        double sec = PhaseMs / 1000.0;
        double singleMps = singlePx / sec / 1e6;
        double multiMps = multiPx / sec / 1e6;
        double scaling = singleMps > 0 ? multiMps / singleMps : 0;

        return new BenchmarkOutcome
        {
            Title = "Rendu 3D (CPU)",
            ScoreLabel = "Débit multi-cœur",
            ScoreValue = multiMps.ToString("0.0"),
            ScoreUnit = "Mpx/s",
            Score = multiMps,
            Category = BenchCategory.Cpu,
            Details =
            {
                new InfoItem("Ray tracing mono-cœur", $"{singleMps:0.0} Mpx/s"),
                new InfoItem("Ray tracing multi-cœur", $"{multiMps:0.0} Mpx/s"),
                new InfoItem("Gain multi-cœur", $"×{scaling:0.0} sur {cores} threads"),
                new InfoItem("Scène", $"{Scene.Length} sphères, {MaxDepth} rebonds, {W}×{H}"),
                new InfoItem("Durée", $"{sec * 2:0} s (2 phases)"),
            }
        };
    }

    private static long RenderPhase(int threads, int ms, CancellationToken ct)
    {
        long totalPixels = 0;
        var sw = Stopwatch.StartNew();
        var opts = new ParallelOptions { MaxDegreeOfParallelism = threads };
        // Each worker renders whole frames, claiming scanlines from a shared counter.
        Parallel.For(0, threads, opts, _ =>
        {
            float sink = 0;
            long px = 0;
            while (sw.ElapsedMilliseconds < ms && !ct.IsCancellationRequested)
            {
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        float u = (x + 0.5f) / W * 2f - 1f;
                        float v = 1f - (y + 0.5f) / H * 2f;
                        u *= (float)W / H;
                        Vector3 dir = Vector3.Normalize(new(u, v, -1.4f));
                        Vector3 col = Trace(new(0, 0, 0), dir, 0);
                        sink += col.X + col.Y + col.Z;
                        px++;
                    }
                    if ((y & 15) == 0 && (sw.ElapsedMilliseconds >= ms || ct.IsCancellationRequested)) break;
                }
            }
            Interlocked.Add(ref totalPixels, px);
            Interlocked.Add(ref _sink, (long)sink);
        });
        return totalPixels;
    }

    private static Vector3 Trace(Vector3 origin, Vector3 dir, int depth)
    {
        if (!Hit(origin, dir, out float t, out int idx))
        {
            float sky = 0.5f * (dir.Y + 1f);
            return Vector3.Lerp(new(0.02f, 0.02f, 0.03f), new(0.10f, 0.12f, 0.18f), sky);
        }

        Sphere s = Scene[idx];
        Vector3 p = origin + dir * t;
        Vector3 n = Vector3.Normalize(p - s.Center);

        float diff = MathF.Max(0f, Vector3.Dot(n, Light));
        // soft shadow: single occlusion ray toward the light
        if (Hit(p + n * 0.001f, Light, out _, out _)) diff *= 0.25f;
        Vector3 local = s.Color * (0.12f + 0.88f * diff);

        if (s.Reflect > 0f && depth < MaxDepth)
        {
            Vector3 refl = dir - 2f * Vector3.Dot(dir, n) * n;
            Vector3 r = Trace(p + n * 0.001f, Vector3.Normalize(refl), depth + 1);
            local = Vector3.Lerp(local, r, s.Reflect);
        }
        return local;
    }

    private static bool Hit(Vector3 origin, Vector3 dir, out float tHit, out int hitIdx)
    {
        tHit = float.MaxValue; hitIdx = -1;
        for (int i = 0; i < Scene.Length; i++)
        {
            Vector3 oc = origin - Scene[i].Center;
            float b = Vector3.Dot(oc, dir);
            float c = Vector3.Dot(oc, oc) - Scene[i].Radius * Scene[i].Radius;
            float disc = b * b - c;
            if (disc < 0) continue;
            float sq = MathF.Sqrt(disc);
            float root = -b - sq;
            if (root < 0.001f) root = -b + sq;
            if (root > 0.001f && root < tHit) { tHit = root; hitIdx = i; }
        }
        return hitIdx >= 0;
    }

    private static async Task Track(Task task, int totalMs, double baseP, double span,
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
