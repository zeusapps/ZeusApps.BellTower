using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.IO;

public sealed class DataLockTests
{
    [Fact]
    public void TryAcquireDoesNotWaitForAnOwnedLockAndSucceedsAfterItIsReleased()
    {
        using var workspace = new TestWorkspace();
        using var acquiredSignal = new ManualResetEventSlim();
        using var releaseSignal = new ManualResetEventSlim();
        Exception? workerError = null;
        var worker = new Thread(() =>
        {
            try
            {
                using var held = new DataLock(workspace.Paths, "rollback-test");
                acquiredSignal.Set();
                releaseSignal.Wait();
            }
            catch (Exception error)
            { workerError = error; }
        })
        { IsBackground = true };
        worker.Start();
        Assert.True(acquiredSignal.Wait(TimeSpan.FromSeconds(5)));
        Assert.Null(DataLock.TryAcquire(workspace.Paths, "rollback-test"));

        releaseSignal.Set();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(workerError);
        using var acquired = DataLock.TryAcquire(workspace.Paths, "rollback-test");
        Assert.NotNull(acquired);
    }
}
