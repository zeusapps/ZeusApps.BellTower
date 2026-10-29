using System.Runtime.InteropServices;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Tray;

public sealed class WindowsUserIdleSourceTests
{
    private static readonly TimeSpan HealthyProbeTestBudget = TimeSpan.FromSeconds(5);

    private sealed class ManualClock(DateTimeOffset value)
    {
        private readonly object gate = new();
        private DateTimeOffset now = value;
        public DateTimeOffset Now() { lock (gate) { return now; } }
        public void Advance(TimeSpan by) { lock (gate) { now += by; } }
    }

    [Fact]
    public void NativeConnectionMappingTreatsActiveDisconnectedErrorsAndMalformedBuffersConservatively()
    {
        Assert.True(WindowsUserIdleSource.MapConnectionState(true, true, 0));
        Assert.False(WindowsUserIdleSource.MapConnectionState(true, true, 4));
        Assert.Null(WindowsUserIdleSource.MapConnectionState(false, true, 0));
        Assert.Null(WindowsUserIdleSource.MapConnectionState(true, false, 0));
        Assert.Null(WindowsUserIdleSource.MapConnectionState(true, true, null));
    }

    [Fact]
    public void LastInputInfoAbiMatchesTheTwoUintWindowsStructure()
    {
        var type = typeof(WindowsUserIdleSource).GetNestedType("LastInputInfo", System.Reflection.BindingFlags.NonPublic)!;
        Assert.Equal(8, Marshal.SizeOf(type));
        Assert.Equal(0, Marshal.OffsetOf(type, "Size").ToInt32());
        Assert.Equal(4, Marshal.OffsetOf(type, "Time").ToInt32());
        Assert.Equal(typeof(uint), type.GetField("Size")!.FieldType);
        Assert.Equal(typeof(uint), type.GetField("Time")!.FieldType);
    }

    [Fact]
    public async Task SourceRejectsFutureAndStaleObservationsAndAllowsOnlyOneInFlightProbe()
    {
        var clock = new ManualClock(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var calls = 0;
        var source = new WindowsUserIdleSource(() =>
        {
            Interlocked.Increment(ref calls);
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(3));
            return new(TimeSpan.FromMinutes(5), clock.Now(), "fixture");
        }, clock.Now);

        await ReminderFixtureDriver.RunAsync(() =>
        {
            Assert.Null(source.GetSnapshot().IdleFor);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
            for (var index = 0; index < 10; index++)
            {
                Assert.Null(source.GetSnapshot().IdleFor);
            }
            Assert.Equal(1, Volatile.Read(ref calls));
            clock.Advance(TimeSpan.FromMilliseconds(501));
            Assert.Null(source.GetSnapshot().IdleFor);
            Assert.Equal(1, Volatile.Read(ref calls));
            return true;
        }, release.Set, TimeSpan.FromSeconds(4));
        Assert.True(SpinWait.SpinUntil(() => source.GetSnapshot().IdleFor == TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(2)));

        using var futureReturned = new ManualResetEventSlim();
        var future = new WindowsUserIdleSource(() =>
        {
            var snapshot = new UserIdleSnapshot(TimeSpan.FromMinutes(5), clock.Now().AddMinutes(1), "future fixture");
            futureReturned.Set();
            return snapshot;
        }, clock.Now);
        Assert.Null(future.GetSnapshot().IdleFor);
        Assert.True(futureReturned.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(SpinWait.SpinUntil(() => future.GetSnapshot().IdleFor is null, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task QueryFailureSettlesToUnknownWithoutEscapingTheObserver()
    {
        using var started = new ManualResetEventSlim();
        var clock = new ManualClock(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
        var source = new WindowsUserIdleSource(() =>
        {
            started.Set();
            throw new InvalidOperationException("fixture query failure");
        }, clock.Now);

        await ReminderFixtureDriver.RunAsync(() =>
        {
            Assert.Null(source.GetSnapshot().IdleFor);
            Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
            return true;
        }, () => { }, TimeSpan.FromSeconds(4));
        Assert.True(SpinWait.SpinUntil(() => source.GetSnapshot().Reason.Contains("unavailable", StringComparison.Ordinal), TimeSpan.FromSeconds(2)));
        Assert.Null(source.GetSnapshot().IdleFor);
    }

    [Fact]
    public void PlaybackRefreshObservesInputThatChangedAfterCachedIdleSampleAndHealthyIdleStillWorks()
    {
        var clock = new ManualClock(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
        var currentIdle = TimeSpan.FromMinutes(20);
        var reads = 0;
        var source = new WindowsUserIdleSource(() =>
        {
            Interlocked.Increment(ref reads);
            return new(currentIdle, clock.Now(), "fixture");
        }, clock.Now, HealthyProbeTestBudget);

        Assert.Equal(TimeSpan.FromMinutes(20), source.GetFreshSnapshot().IdleFor);
        Assert.Equal(1, Volatile.Read(ref reads));
        currentIdle = TimeSpan.Zero;
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(TimeSpan.Zero, source.GetFreshSnapshot().IdleFor);
        Assert.Equal(2, Volatile.Read(ref reads));
    }

    [Fact]
    public async Task PlaybackRefreshTimeoutWithholdsCachedPositiveIdleAndConcurrentRequestsStaySingleFlight()
    {
        var clock = new ManualClock(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
        var currentIdle = TimeSpan.FromMinutes(20);
        var reads = 0;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var source = new WindowsUserIdleSource(() =>
        {
            Interlocked.Increment(ref reads);
            if (currentIdle == TimeSpan.FromMinutes(19))
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(3));
            }
            return new(currentIdle, clock.Now(), "fixture");
        }, clock.Now);

        await ReminderFixtureDriver.RunAsync(() =>
        {
            Assert.Equal(TimeSpan.FromMinutes(20), source.GetFreshSnapshot(HealthyProbeTestBudget).IdleFor);
            currentIdle = TimeSpan.FromMinutes(19);
            clock.Advance(TimeSpan.FromMilliseconds(250));
            Assert.Equal(TimeSpan.FromMinutes(20), source.GetSnapshot().IdleFor);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
            Assert.Equal(2, Volatile.Read(ref reads));
            // Production-default 80 ms rejects because this low-level observation is already running
            // and held. The result does not depend on observer startup scheduling.
            Assert.Null(source.GetFreshSnapshot().IdleFor);
            var concurrent = ReminderFixtureDriver.RunDedicatedConsumers(8, source.GetFreshSnapshot, TimeSpan.FromSeconds(2));
            Assert.All(concurrent, snapshot => Assert.Null(snapshot.IdleFor));
            Assert.Equal(2, Volatile.Read(ref reads));
            return true;
        }, release.Set, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PlaybackFreshRequestJoinsAnOlderPollThenRequiresASecondObservation()
    {
        var clock = new ManualClock(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
        using var firstStarted = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        var reads = 0;
        var source = new WindowsUserIdleSource(() =>
        {
            var read = Interlocked.Increment(ref reads);
            if (read == 1)
            {
                firstStarted.Set();
                releaseFirst.Wait(TimeSpan.FromSeconds(3));
            }
            return new(TimeSpan.FromMinutes(20), clock.Now(), "fixture");
        }, clock.Now, HealthyProbeTestBudget);

        Assert.Null(source.GetSnapshot().IdleFor);
        Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(2)));
        var freshRequest = Task.Run(source.GetFreshSnapshot);
        releaseFirst.Set();
        Assert.Equal(TimeSpan.FromMinutes(20), (await freshRequest).IdleFor);
        Assert.Equal(2, Volatile.Read(ref reads));
    }

    [Fact]
    public void TickMappingKeepsShortWrapAndRejectsFutureAmbiguousAndOverBoundValues()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(21), WindowsUserIdleSource.Elapsed((ulong)uint.MaxValue + 11, uint.MaxValue - 10));
        Assert.Equal(TimeSpan.FromHours(24), WindowsUserIdleSource.Elapsed(100_000_000, 13_600_000));
        Assert.Equal(TimeSpan.FromHours(24).Add(TimeSpan.FromMilliseconds(500)), WindowsUserIdleSource.Elapsed(100_000_000, 13_599_500));
        Assert.Equal(TimeSpan.FromHours(25), WindowsUserIdleSource.Elapsed(100_000_000, 10_000_000));
        Assert.Null(WindowsUserIdleSource.Elapsed(10_000, 11_000));
        Assert.Null(WindowsUserIdleSource.Elapsed(4_294_977_296, 11_000));
        Assert.Null(WindowsUserIdleSource.Elapsed((ulong)uint.MaxValue + 1, 1_800_000));
    }

    [Fact]
    public void SnapshotAllowsIdleBeyondConfiguredMaximumButRejectsClockRollback()
    {
        var now = DateTimeOffset.Parse("2030-01-01T00:00:00Z");
        Assert.True(new UserIdleSnapshot(TimeSpan.FromHours(25), now, "fixture").IsFresh(now, TimeSpan.FromMilliseconds(500)));
        Assert.False(new UserIdleSnapshot(TimeSpan.FromMinutes(5), now.AddTicks(1), "future fixture").IsFresh(now, TimeSpan.FromMilliseconds(500)));
    }
}
