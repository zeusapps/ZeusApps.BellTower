using BellTower.FieldAcceptance.Evidence;
using BellTower.FieldAcceptance.Harness;
using BellTower.FieldAcceptance.Isolation;
using Xunit;

namespace BellTower.Tests.FieldAcceptance;

/// <summary>The interrupt, concurrency and exposure mechanisms of the harness. They start no process and touch no real task or tray.</summary>
public sealed class RunControlTests
{
    private static readonly (DateTimeOffset At, string Outcome, string Detail) Started = (DateTimeOffset.Parse("2026-11-25T10:00:00Z"), "Started", "started-via-shell");

    // ---- Exposure (ANALYSIS-122) -------------------------------------------------------------------------------

    [Fact]
    public void ACodexSessionWithoutNoDaemonMarksTheInstanceAndAClaudeOrNoDaemonSessionDoesNot()
    {
        var exposure = new RealEnvironmentExposure();
        exposure.NoteSessionStart(ProviderModel.Claude, []);
        exposure.NoteSessionStart(ProviderModel.Codex, ProviderModel.Codex.AgentArguments);
        Assert.False(exposure.SharedBackendCodexUsed);

        exposure.NoteSessionStart(ProviderModel.Codex, []);
        Assert.True(exposure.SharedBackendCodexUsed);
        Assert.False(exposure.HookStartedTrayUsed);
    }

    [Fact]
    public void AHookThatChangesTheAttemptRecordToStartedMarksTheInstance()
    {
        var exposure = new RealEnvironmentExposure();
        var throttled = (Started.At, "Throttled", "none");
        exposure.NoteHook(null, throttled);
        exposure.NoteHook(Started, Started); // an earlier hook's record, unchanged: not this hook
        Assert.False(exposure.HookStartedTrayUsed);

        exposure.NoteHook(null, Started);
        Assert.True(exposure.HookStartedTrayUsed);
        Assert.True(RealEnvironmentExposure.HookStartedATray(throttled, Started));
        Assert.False(RealEnvironmentExposure.HookStartedATray(Started, throttled));
        Assert.False(RealEnvironmentExposure.HookStartedATray(null, null));
    }

    [Fact]
    public void TheCombinationInOneInstanceThrowsInEitherOrderAndStaysStickyWhileTwoInstancesAreIndependent()
    {
        var hookFirst = new RealEnvironmentExposure();
        hookFirst.NoteHook(null, Started);
        Assert.Throws<IsolationException>(() => hookFirst.NoteSessionStart(ProviderModel.Codex, []));
        Assert.Throws<IsolationException>(() => hookFirst.NoteHook(null, Started)); // sticky: the instance stays refused

        var sessionFirst = new RealEnvironmentExposure();
        sessionFirst.NoteSessionStart(ProviderModel.Codex, []);
        Assert.Throws<IsolationException>(() => sessionFirst.NoteHook(null, Started));

        // The same pair in two instances (separate data directories) is fine.
        var shared = new RealEnvironmentExposure();
        var hookStarted = new RealEnvironmentExposure();
        shared.NoteSessionStart(ProviderModel.Codex, []);
        hookStarted.NoteHook(null, Started);
        Assert.True(shared.SharedBackendCodexUsed);
        Assert.True(hookStarted.HookStartedTrayUsed);
    }

    [Fact]
    public void ARefusedCombinationEndsTheInstanceTraysAtOnce()
    {
        var exposure = new RealEnvironmentExposure();
        var ended = 0;
        exposure.Guard(report => report.NoteSessionStart(ProviderModel.Codex, []), () => ended++);
        Assert.Equal(0, ended);

        Assert.Throws<IsolationException>(() => exposure.Guard(report => report.NoteHook(null, Started), () => ended++));
        Assert.Equal(1, ended);
    }

    [Fact]
    public void ARunWithBothUsesInSeparateInstancesNeverThrows()
    {
        // The shape of --scenario all: one scenario's instance has hook starts, another's a shared-backend Codex.
        var instances = Enumerable.Range(0, 2).Select(_ => new RealEnvironmentExposure()).ToArray();
        instances[0].NoteHook(null, Started);
        instances[1].NoteSessionStart(ProviderModel.Codex, []);
        Assert.True(instances[0].HookStartedTrayUsed && !instances[0].SharedBackendCodexUsed);
        Assert.True(instances[1].SharedBackendCodexUsed && !instances[1].HookStartedTrayUsed);
    }

    // ---- Tray replacement evidence (ANALYSIS-122) --------------------------------------------------------------

    [Fact]
    public void AReplacementThatStopsAndRestartsPassesAndOnlyThenTheDetailSaysRestarted()
    {
        var running = true;
        var calls = new List<string>();
        var result = TrayReplacement.Run(() => running, () => { calls.Add("exit"); running = false; }, () => { calls.Add("start"); running = true; }, Wait, () => calls.Add("end"));

        Assert.True(result.Passed);
        Assert.Equal(["exit", "start"], calls);
        Assert.Contains("restarted silently", result.Detail);
    }

    [Fact]
    public void AFailingExitFailsTheReplacementAndEndsTheTrayDirectly()
    {
        var running = true;
        var calls = new List<string>();
        var result = TrayReplacement.Run(() => running, () => calls.Add("exit"), () => { calls.Add("start"); running = true; }, Wait, () => { calls.Add("end"); running = false; });

        Assert.False(result.Passed);
        Assert.False(result.Stopped);
        Assert.Equal(["exit", "end", "start"], calls);
        Assert.Contains("did NOT stop", result.Detail);
    }

    [Fact]
    public void AFailingStartFailsTheReplacementAndNeverClaimsARestart()
    {
        var running = true;
        var result = TrayReplacement.Run(() => running, () => running = false, () => { }, Wait, () => { });

        Assert.False(result.Passed);
        Assert.True(result.Stopped);
        Assert.False(result.Restarted);
        Assert.DoesNotContain("restarted silently", result.Detail);
        Assert.Contains("did NOT produce a running tray", result.Detail);
    }

    [Fact]
    public void WaitForReportsATimeoutInsteadOfReturningSilently()
    {
        Assert.True(TowerReader.WaitFor(() => true, 1000, new RunAbort()));
        Assert.False(TowerReader.WaitFor(() => false, 50, new RunAbort()));
    }

    private static bool Wait(Func<bool> condition, int timeout) => condition();

    // ---- Interrupt (ANALYSIS-120) ------------------------------------------------------------------------------

    [Fact]
    public void AStopRequestMakesNewWorkThrowAndKeepsTheFirstReason()
    {
        var abort = new RunAbort();
        abort.ThrowIfRequested();
        Assert.True(abort.Request("first"));
        Assert.False(abort.Request("second"));

        var exception = Assert.Throws<RunInterruptedException>(abort.ThrowIfRequested);
        Assert.Equal("first", exception.Message);
        Assert.Throws<RunInterruptedException>(() => TowerReader.WaitFor(() => false, 5000, abort));
    }

    [Fact]
    public void ReadersSeeSnapshotsWhileAnotherThreadAddsChecks()
    {
        // The report is written on the main thread while a handler thread may read it: no read may throw while adds are in flight.
        var report = new RunReport();
        using var stop = new CancellationTokenSource();
        var writer = new Thread(() =>
        {
            for (var index = 0; index < 400 && !stop.IsCancellationRequested; index++)
            {
                report.Checks.Add(new("demo", "check-" + index, ProviderNames.Claude, EvidenceLabel.InstalledHarness, true, "x"));
            }
        });
        writer.Start();
        for (var round = 0; round < 200; round++)
        {
            _ = report.ToJson();
            _ = report.Checks.Count(check => check.Passed);
        }
        writer.Join();
        Assert.Equal(400, report.Checks.Count);
    }

    [Fact]
    public void AnInterruptCheckFailsTheRunAndTheResultStillSerializes()
    {
        var report = new RunReport { Isolation = new([], [], [], [], [], true, "ok") };
        report.Add(new("demo", "one", ProviderNames.Claude, EvidenceLabel.InstalledHarness, true, "x"));
        report.Add(new("run", "interrupted", ProviderNames.Harness, EvidenceLabel.InstalledHarness, false, "interrupted"));

        Assert.False(report.Passed);
        Assert.Contains("\"id\": \"interrupted\"", report.ToJson());
    }

    // ---- Run lock (ANALYSIS-121) -------------------------------------------------------------------------------

    private static string LockName() => @"Local\BellTower.FieldAcceptance.Test." + Guid.NewGuid().ToString("N");

    [Fact]
    public void ASecondRunCannotTakeTheLockWhileTheFirstHoldsItAndCanAfterItIsReleased()
    {
        var name = LockName();
        // A mutex is owned by a thread, so each "run" is its own thread, as each harness run is its own process.
        var first = OnThread(() => RunLock.TryAcquire(name), keepAlive: true, out var firstRelease);
        Assert.NotNull(first);

        var second = OnThread(() => RunLock.TryAcquire(name), keepAlive: false, out _);
        Assert.Null(second);

        firstRelease();
        var third = OnThread(() => RunLock.TryAcquire(name), keepAlive: false, out _);
        Assert.NotNull(third);
    }

    [Fact]
    public void ALockAbandonedByAKilledRunIsAcquiredByTheNextRun()
    {
        var name = LockName();
        // The owner thread ends without releasing: what the kernel does with a killed process's mutex.
        var holder = new Thread(() => { _ = RunLock.TryAcquire(name); });
        holder.Start();
        holder.Join();

        var next = OnThread(() => RunLock.TryAcquire(name), keepAlive: false, out _);
        Assert.NotNull(next);
    }

    /// <summary>Runs <paramref name="acquire"/> on a fresh thread; with keepAlive the thread stays alive until the returned release action runs (and disposes the lock there).</summary>
    private static RunLock? OnThread(Func<RunLock?> acquire, bool keepAlive, out Action release)
    {
        RunLock? result = null;
        var acquired = new ManualResetEventSlim();
        var done = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            result = acquire();
            acquired.Set();
            if (keepAlive)
            {
                done.Wait();
            }
            result?.Dispose();
        });
        thread.Start();
        acquired.Wait();
        release = () =>
        {
            done.Set();
            thread.Join();
        };
        if (!keepAlive)
        {
            thread.Join();
        }
        return result;
    }
}
