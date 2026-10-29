using System.Runtime.InteropServices;

namespace BellTower.Tray;

public sealed class WindowsUserIdleSource : IUserIdleSource
{
    private static readonly TimeSpan PlaybackRefreshBudget = TimeSpan.FromMilliseconds(80);
    private readonly Func<UserIdleSnapshot> observe;
    private readonly Func<DateTimeOffset> clock;
    private readonly TimeSpan freshReadBudget;
    private readonly object gate = new();
    private UserIdleSnapshot snapshot = UserIdleSnapshot.Unknown("Current interactive-session input state has not been observed.", DateTimeOffset.MinValue);
    private Task<UserIdleSnapshot>? query;
    private DateTimeOffset lastStartedAt = DateTimeOffset.MinValue;
    private long querySequence;
    private long completedSequence;

    public WindowsUserIdleSource() : this(Observe, () => DateTimeOffset.UtcNow)
    {
    }

    internal WindowsUserIdleSource(Func<UserIdleSnapshot> observe, Func<DateTimeOffset>? clock = null, TimeSpan? freshReadBudget = null)
    {
        this.observe = observe;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.freshReadBudget = freshReadBudget ?? PlaybackRefreshBudget;
    }

    public UserIdleSnapshot GetSnapshot()
    {
        lock (gate)
        {
            HarvestCompletedQuery();

            var now = clock();
            if (query is null && now >= lastStartedAt && now - lastStartedAt >= TimeSpan.FromMilliseconds(250))
            {
                StartQuery();
            }

            return snapshot.IsObservationFresh(now, TimeSpan.FromMilliseconds(500))
                ? snapshot
                : UserIdleSnapshot.Unknown("Current interactive-session input observation is stale.", now);
        }
    }

    public UserIdleSnapshot GetFreshSnapshot() => GetFreshSnapshot(freshReadBudget);

    internal UserIdleSnapshot GetFreshSnapshot(TimeSpan budget)
    {
        if (budget < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(budget));
        }

        var requestedAt = clock();
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        long baselineSequence;
        lock (gate)
        {
            HarvestCompletedQuery();
            baselineSequence = querySequence;
            if (query is null)
            {
                StartQuery();
            }
        }

        while (true)
        {
            Task<UserIdleSnapshot> current;
            lock (gate)
            {
                if (completedSequence > baselineSequence)
                {
                    return snapshot.IsFresh(clock(), TimeSpan.FromMilliseconds(500)) && snapshot.ObservedAt >= requestedAt
                        ? snapshot
                        : UserIdleSnapshot.Unknown("A fresh current-session input observation was unavailable.", clock());
                }

                if (query is null)
                {
                    StartQuery();
                }
                current = query!;
            }

            var remaining = budget - System.Diagnostics.Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero || !WaitBounded(current, remaining))
            {
                return UserIdleSnapshot.Unknown("A fresh current-session input observation did not complete within the playback budget.", clock());
            }

            lock (gate)
            {
                if (ReferenceEquals(query, current))
                {
                    HarvestCompletedQuery();
                }
                // Any query already in flight when this call began predates the request. Once
                // it completes, the next loop starts a new single-flight observation.
            }
        }
    }

    private void StartQuery()
    {
        query = Task.Run(observe);
        lastStartedAt = clock();
        querySequence++;
    }

    private void HarvestCompletedQuery()
    {
        if (query?.IsCompleted != true)
        {
            return;
        }

        try
        {
            snapshot = query.GetAwaiter().GetResult();
            var observedAt = clock();
            if (!snapshot.IsObservationFresh(observedAt, TimeSpan.FromMilliseconds(500)) ||
                snapshot.IdleFor is { } idle && (idle < TimeSpan.Zero || idle > UserIdleSnapshot.MaximumIdleFor))
            {
                snapshot = UserIdleSnapshot.Unknown("Windows returned a future, stale or out-of-range input observation.", observedAt);
            }
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
        {
            snapshot = UserIdleSnapshot.Unknown("Current interactive-session input state is unavailable.", clock());
        }

        query = null;
        completedSequence = querySequence;
    }

    private static bool WaitBounded(Task task, TimeSpan timeout)
    {
        try
        {
            return task.Wait(timeout);
        }
        catch (AggregateException)
        {
            return true;
        }
    }

    internal static TimeSpan? Elapsed(ulong uptimeMilliseconds, uint lastInputTick)
    {
        var elapsed = unchecked((uint)uptimeMilliseconds - lastInputTick);
        return elapsed <= uptimeMilliseconds && elapsed <= (uint)UserIdleSnapshot.MaximumIdleFor.TotalMilliseconds
            ? TimeSpan.FromMilliseconds(elapsed)
            : null;
    }

    internal static bool? MapConnectionState(bool querySucceeded, bool bufferValid, int? connectionState) =>
        !querySucceeded || !bufferValid || connectionState is null ? null : connectionState == (int)WtsConnectState.Active;

    private static UserIdleSnapshot Observe()
    {
        var now = DateTimeOffset.UtcNow;
        if (!OperatingSystem.IsWindows() || !Environment.UserInteractive)
        {
            return UserIdleSnapshot.Unknown("Input inactivity is unavailable outside an interactive Windows session.", now);
        }

        if (!ProcessIdToSessionId((uint)Environment.ProcessId, out var sessionId))
        {
            return UserIdleSnapshot.Unknown("The current interactive session could not be identified.", now);
        }

        if (!WTSQuerySessionInformationW(IntPtr.Zero, sessionId, WtsInfoClass.ConnectState, out var connectionState, out var bytes))
        {
            return UserIdleSnapshot.Unknown("The current interactive-session connection state could not be verified.", now);
        }

        try
        {
            var connected = MapConnectionState(true, connectionState != IntPtr.Zero && bytes >= sizeof(int),
                connectionState == IntPtr.Zero || bytes < sizeof(int) ? null : Marshal.ReadInt32(connectionState));
            if (connected != true)
            {
                return UserIdleSnapshot.Unknown("The current interactive session is disconnected or inactive.", now);
            }
        }
        finally
        {
            WTSFreeMemory(connectionState);
        }

        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            return UserIdleSnapshot.Unknown("Windows could not read current-session input inactivity.", now);
        }

        var elapsed = Elapsed(GetTickCount64(), info.Time);
        return elapsed is { } idle
            ? new(idle, now, "Windows GetLastInputInfo observed the current interactive session.")
            : UserIdleSnapshot.Unknown("Windows returned an inconsistent last-input tick.", now);
    }

    private enum WtsInfoClass
    {
        ConnectState = 8
    }

    private enum WtsConnectState
    {
        Active = 0
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo lastInputInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern ulong GetTickCount64();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQuerySessionInformationW(IntPtr server, uint sessionId, WtsInfoClass infoClass, out IntPtr buffer, out uint bytesReturned);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);
}
