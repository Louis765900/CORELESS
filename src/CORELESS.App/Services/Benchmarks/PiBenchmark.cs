using System.Diagnostics;
using System.Numerics;
using Coreless.Models;

namespace Coreless.Services.Benchmarks;

/// <summary>
/// Arbitrary-precision compute benchmark (y-cruncher-style): computes π to a fixed number
/// of decimal digits with the Chudnovsky series via binary splitting on BigInteger, then a
/// Newton integer square root. Stresses the ALU with huge-integer multiplies. Single-core
/// by nature (one long dependent computation); score = digits per second.
/// </summary>
public static class PiBenchmark
{
    private const int Digits = 50_000; // ~3570 Chudnovsky terms

    public static async Task<BenchmarkOutcome> RunAsync(IProgress<double> progress, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            progress.Report(0.05);

            int terms = Digits / 14 + 1;
            (BigInteger P, BigInteger Q, BigInteger T) = Split(1, terms, ct, progress);
            progress.Report(0.75);

            // pi = (426880 * sqrt(10005) * (Q * 10^Digits)) / (T + 13591409 * Q)
            BigInteger one = BigInteger.Pow(10, Digits);
            BigInteger sqrtC = ISqrt(10005 * one * one);           // sqrt(10005) * 10^Digits
            BigInteger num = 426880 * sqrtC * Q;
            BigInteger den = 13591409 * Q + T;
            BigInteger pi = num / den;                             // π * 10^Digits
            progress.Report(0.98);

            sw.Stop();
            double sec = sw.Elapsed.TotalSeconds;
            double digitsPerSec = sec > 0 ? Digits / sec : 0;
            string head = PiHead(pi);
            bool ok = head.StartsWith("3.14159265358979");

            progress.Report(1.0);
            return new BenchmarkOutcome
            {
                Title = "Calcul Pi (précision arbitraire)",
                ScoreLabel = "Débit de calcul",
                ScoreValue = Math.Round(digitsPerSec).ToString("N0"),
                ScoreUnit = "chiffres/s",
                Score = digitsPerSec,
                Category = BenchCategory.Cpu,
                Details =
                {
                    new InfoItem("Chiffres calculés", $"{Digits:N0}"),
                    new InfoItem("Temps", $"{sec:0.00} s"),
                    new InfoItem("Algorithme", "Chudnovsky + binary splitting"),
                    new InfoItem("Vérification", ok ? $"OK — {head[..16]}…" : "ÉCHEC"),
                    new InfoItem("Termes de série", $"{terms:N0}"),
                }
            };
        }, ct);
    }

    // Binary splitting of the Chudnovsky series over terms [a, b).
    private static (BigInteger P, BigInteger Q, BigInteger T) Split(
        long a, long b, CancellationToken ct, IProgress<double> progress)
    {
        if (b - a == 1)
        {
            ct.ThrowIfCancellationRequested();
            BigInteger p, q;
            if (a == 0)
            {
                p = q = BigInteger.One;
            }
            else
            {
                p = (BigInteger)(6 * a - 5) * (2 * a - 1) * (6 * a - 1);
                q = (BigInteger)a * a * a * 640320 * 640320 * 640320 / 24;
            }
            BigInteger t = p * (13591409 + 545140134 * (BigInteger)a);
            if ((a & 1) == 1) t = -t;
            return (p, q, t);
        }

        long m = (a + b) / 2;
        (BigInteger Pl, BigInteger Ql, BigInteger Tl) = Split(a, m, ct, progress);
        (BigInteger Pr, BigInteger Qr, BigInteger Tr) = Split(m, b, ct, progress);
        return (Pl * Pr, Ql * Qr, Qr * Tl + Pl * Tr);
    }

    // Integer square root via Newton's method (floor(sqrt(n))).
    private static BigInteger ISqrt(BigInteger n)
    {
        if (n < 2) return n;
        BigInteger x = BigInteger.One << (int)((n.GetBitLength() + 1) / 2);
        while (true)
        {
            BigInteger y = (x + n / x) >> 1;
            if (y >= x) return x;
            x = y;
        }
    }

    private static string PiHead(BigInteger piScaled)
    {
        string s = piScaled.ToString();
        if (s.Length < 20) return s;
        return s[0] + "." + s[1..20];
    }
}
