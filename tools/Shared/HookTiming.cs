namespace BellTower.Tools;

/// <summary>One timed hook call: its number, the elapsed milliseconds and whether it was accepted.</summary>
public sealed record TimingSample(int Sample, double Milliseconds, bool Success);

public sealed record SampleDuration(int Sample, double Milliseconds, bool Success);

public sealed record TimingSummary(string Mode, int Samples, int FirstSampleCount, int ParallelSamples, int Parallelism,
    double FirstSampleMilliseconds, double MeanMilliseconds, double MedianMilliseconds, double P95Milliseconds,
    double P99Milliseconds, double MaximumMilliseconds, int FailedCalls, int OverThreeSeconds, SampleDuration[] Timings);

/// <summary>
/// The hook timing sampling plan and statistics shared by <c>tools/BellTower.Ingress.Bench</c> and the field acceptance harness
/// (compiled into both, so there is one implementation): one serial first sample, then concurrent batches of the parallelism size
/// whose legs alternate, and the percentile rule (nearest rank, <c>ceil(q * n) - 1</c>).
/// </summary>
public static class HookTiming
{
    /// <summary>The sample numbers of each concurrent batch: batch <c>b</c> holds numbers <c>1 + b * parallelism</c> up to the parallelism size.</summary>
    public static IReadOnlyList<int[]> Batches(int parallelSamples, int parallelism) =>
        [.. Enumerable.Range(0, parallelSamples / parallelism).Select(batch => Enumerable.Range(1 + batch * parallelism, parallelism).ToArray())];

    /// <summary>True when the baseline leg goes first in this batch. The legs alternate so long-term machine drift cannot favor one of them.</summary>
    public static bool BaselineFirst(int batch, int legOffset) => (batch + legOffset) % 2 == 0;

    /// <summary>The legs of one batch in rotated order, so with more than two legs each leg also takes every position over the run (the three-leg form of <see cref="BaselineFirst"/>).</summary>
    public static IReadOnlyList<T> Rotate<T>(IReadOnlyList<T> legs, int batch, int legOffset)
    {
        var start = (batch + legOffset) % legs.Count;
        return [.. legs.Skip(start), .. legs.Take(start)];
    }

    /// <summary>The nearest-rank percentile of an ascending sequence.</summary>
    public static double Percentile(IReadOnlyList<double> ordered, double quantile) => ordered[Math.Max(0, (int)Math.Ceiling(quantile * ordered.Count) - 1)];

    public static TimingSummary Summarize(IEnumerable<TimingSample> samples, string mode, int parallelism, int parallelSamples)
    {
        var measured = samples.OrderBy(item => item.Sample).ToArray();
        var ordered = measured.Select(item => item.Milliseconds).Order().ToArray();
        return new TimingSummary(
            mode,
            measured.Length,
            1,
            parallelSamples,
            parallelism,
            measured[0].Milliseconds,
            ordered.Average(),
            Percentile(ordered, 0.5),
            Percentile(ordered, 0.95),
            Percentile(ordered, 0.99),
            ordered[^1],
            measured.Count(item => !item.Success),
            measured.Count(item => item.Milliseconds >= 3000),
            measured.Select(item => new SampleDuration(item.Sample, item.Milliseconds, item.Success)).ToArray());
    }
}
