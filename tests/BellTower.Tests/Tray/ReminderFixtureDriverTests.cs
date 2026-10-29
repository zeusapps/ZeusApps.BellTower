using Xunit;

namespace BellTower.Tests.Tray;

public sealed class ReminderFixtureDriverTests
{
    [Fact]
    public async Task ReleasesHeldWorkOnTheDriverThreadBeforeReturning()
    {
        var callerThread = Environment.CurrentManagedThreadId;
        var workThread = 0;
        var releaseThread = 0;
        var released = 0;

        var result = await ReminderFixtureDriver.RunAsync(() =>
        {
            workThread = Environment.CurrentManagedThreadId;
            return 42;
        }, () =>
        {
            releaseThread = Environment.CurrentManagedThreadId;
            Interlocked.Exchange(ref released, 1);
        }, TimeSpan.FromSeconds(2));

        Assert.Equal(42, result);
        Assert.NotEqual(callerThread, workThread);
        Assert.Equal(workThread, releaseThread);
        Assert.Equal(1, Volatile.Read(ref released));
    }

    [Fact]
    public async Task PreservesWorkAndCleanupFailuresAndStillReleasesOnDriverThread()
    {
        var workThread = 0;
        var releaseThread = 0;

        var error = await Assert.ThrowsAsync<AggregateException>(() => ReminderFixtureDriver.RunAsync<int>(() =>
        {
            workThread = Environment.CurrentManagedThreadId;
            throw new InvalidOperationException("fixture assertion");
        }, () =>
        {
            releaseThread = Environment.CurrentManagedThreadId;
            throw new ApplicationException("fixture cleanup");
        }, TimeSpan.FromSeconds(2)));

        Assert.Equal(workThread, releaseThread);
        Assert.Contains(error.InnerExceptions, exception => exception is InvalidOperationException { Message: "fixture assertion" });
        Assert.Contains(error.InnerExceptions, exception => exception is ApplicationException { Message: "fixture cleanup" });
    }
}
