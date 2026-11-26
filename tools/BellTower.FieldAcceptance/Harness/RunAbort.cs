namespace BellTower.FieldAcceptance.Harness;

/// <summary>Thrown on the main thread when a run was asked to stop (Ctrl+C or a closed console): the run unwinds through its cleanup instead of starting new work.</summary>
public sealed class RunInterruptedException(string reason) : Exception(reason);

/// <summary>
/// The run-wide stop request. Interrupt handlers only call <see cref="Request"/> (and end in-flight sandbox children); every place that
/// starts new work calls <see cref="ThrowIfRequested"/>, so the one main thread unwinds through its own cleanup, production comparison and
/// result file instead of racing a second thread that does the same.
/// </summary>
public sealed class RunAbort
{
    private volatile string? reason;

    /// <summary>The abort of the running harness process.</summary>
    public static RunAbort Current { get; } = new();

    public bool Requested => reason is not null;
    public string? Reason => reason;

    /// <summary>Records the stop request; the first reason wins. Returns true for the first request.</summary>
    public bool Request(string why) => Interlocked.CompareExchange(ref reason, why, null) is null;

    public void ThrowIfRequested()
    {
        if (reason is { } why)
        {
            throw new RunInterruptedException(why);
        }
    }
}
