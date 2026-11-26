namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// The one place the harness replaces the whole process environment (an in-process product call, such as the update transaction, then
/// starts children that inherit the sandbox values and nothing of the harness user's environment). The swap is process-wide, so it is
/// enforced rather than documented (ANALYSIS-171): a second swap, or any harness code that reads the environment to start a process on
/// another thread while one is active, throws instead of silently building a child environment from another sandbox's variables.
/// The swapping thread itself is never blocked (the update transaction starts the tray from that thread).
/// </summary>
public static class ProcessEnvironmentSwap
{
    private static int owner;

    /// <summary>True while a swap is active on any thread.</summary>
    public static bool Active => Volatile.Read(ref owner) != 0;

    /// <summary>Throws when a swap is active on another thread. Called before the harness reads the environment or starts a process.</summary>
    public static void ThrowIfForeign()
    {
        var current = Volatile.Read(ref owner);
        if (current != 0 && current != Environment.CurrentManagedThreadId)
        {
            throw new InvalidOperationException("The process environment is being swapped for an in-process call on another thread; start no process and read no environment until it returns.");
        }
    }

    /// <summary>
    /// Runs the action with the process environment replaced by <paramref name="replacement"/>, then restores the original exactly: values
    /// are put back and every variable the action created is removed.
    /// </summary>
    public static T Run<T>(IReadOnlyDictionary<string, string> replacement, Func<T> action)
    {
        if (Interlocked.CompareExchange(ref owner, Environment.CurrentManagedThreadId, 0) != 0)
        {
            throw new InvalidOperationException("Another in-process environment swap is active; swaps are single-threaded and cannot nest.");
        }
        var saved = Snapshot();
        try
        {
            foreach (var name in saved.Keys)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
            foreach (var (name, value) in replacement)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
            return action();
        }
        finally
        {
            foreach (var name in Snapshot().Keys)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
            foreach (var (name, value) in saved)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
            Volatile.Write(ref owner, 0);
        }
    }

    private static Dictionary<string, string?> Snapshot() =>
        Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value, StringComparer.OrdinalIgnoreCase);
}
