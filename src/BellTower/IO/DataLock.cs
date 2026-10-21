namespace BellTower.IO;

public sealed class DataLock : IDisposable
{
    private readonly Mutex mutex;
    public DataLock(DataPaths paths, string area)
    {
        mutex = new Mutex(false, paths.LockName(area));
        try
        {
            if (!mutex.WaitOne(TimeSpan.FromSeconds(5)))
            {
                mutex.Dispose();
                throw new IOException("Bell Tower state is busy.");
            }
        }
        catch (AbandonedMutexException)
        { /* Previous owner exited; atomic files remain authoritative. */
        }
    }

    public void Dispose()
    {
        mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
