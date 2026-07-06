namespace Coreless.Services.Benchmarks;

/// <summary>
/// Sustained all-core CPU load generator for thermal/stability testing.
/// Spawns one hot loop per logical core until stopped.
/// </summary>
public sealed class StressTest
{
    private CancellationTokenSource? _cts;
    private static double _sink;

    public bool IsRunning => _cts is not null;

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        CancellationToken ct = _cts.Token;
        int cores = Environment.ProcessorCount;
        for (int i = 0; i < cores; i++)
        {
            int id = i;
            Task.Factory.StartNew(() => Burn(id, ct), ct,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private static void Burn(int id, CancellationToken ct)
    {
        ulong s = (ulong)(id + 1) * 2654435761UL | 1;
        double acc = 0;
        while (!ct.IsCancellationRequested)
        {
            for (int i = 0; i < 500_000; i++)
            {
                s ^= s << 13; s ^= s >> 7; s ^= s << 17;
                double x = (s & 0xFFFF) * 1e-3 + 1.0;
                acc += Math.Sqrt(x) * Math.Sin(x) + x * 0.5;
            }
            _sink += acc;
        }
    }
}
