namespace BellTower.FieldAcceptance.Harness;

/// <summary>What replacing a hook-started tray actually achieved (each part is observed, never assumed).</summary>
public sealed record TrayReplacementResult(bool Stopped, bool Restarted)
{
    public bool Passed => Stopped && Restarted;

    /// <summary>States only what was observed; the restart sentence appears only after the restart succeeded.</summary>
    public string Detail =>
        (Stopped ? "The hook-started tray (real profile environment) stopped after exit" : "The hook-started tray did NOT stop after exit; its processes were ended directly") +
        (Restarted ? "; restarted silently under the sandbox environment and the tray is running." : "; the silent restart under the sandbox environment did NOT produce a running tray.");
}

/// <summary>
/// A tray started by a hook runs with the real profile environment by product design. The harness exits it and starts a sandbox-environment
/// tray in its place; this class makes that replacement observable so a failing <c>exit</c> or <c>start</c> fails a check instead of
/// leaving a real-environment tray running under a recorded success.
/// </summary>
public static class TrayReplacement
{
    /// <param name="trayRunning">Whether the instance's tray is running.</param>
    /// <param name="exit">Asks the tray to exit.</param>
    /// <param name="startSilent">Starts the tray silently under the sandbox environment.</param>
    /// <param name="waitFor">Waits for a condition and reports whether it came true before the timeout.</param>
    /// <param name="endTrays">Ends the instance's tray processes directly (used only when <paramref name="exit"/> did not stop the tray).</param>
    /// <param name="timeoutMilliseconds">The bound for each wait.</param>
    public static TrayReplacementResult Run(Func<bool> trayRunning, Action exit, Action startSilent, Func<Func<bool>, int, bool> waitFor, Action endTrays, int timeoutMilliseconds = 20000)
    {
        exit();
        var stopped = waitFor(() => !trayRunning(), timeoutMilliseconds);
        if (!stopped)
        {
            endTrays();
        }
        startSilent();
        var restarted = waitFor(trayRunning, timeoutMilliseconds);
        return new(stopped, restarted);
    }
}
