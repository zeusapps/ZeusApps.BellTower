namespace BellTower.IO;

public sealed class DataLock : IDisposable
{
    private readonly Mutex mutex;
    private bool ownsMutex = true;

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

    private DataLock(Mutex mutex)
    {
        this.mutex = mutex;
    }

    /// <summary>Attempts to acquire a data lock without waiting. Returns null when another owner holds it.</summary>
    public static DataLock? TryAcquire(DataPaths paths, string area)
    {
        var mutex = new Mutex(false, paths.LockName(area));
        try
        {
            if (!mutex.WaitOne(0))
            {
                mutex.Dispose();
                return null;
            }
        }
        catch (AbandonedMutexException)
        { /* The abandoned mutex is acquired by WaitOne; atomic files remain authoritative. */
        }
        catch
        {
            mutex.Dispose();
            throw;
        }

        return new DataLock(mutex);
    }

    public void Dispose()
    {
        if (ownsMutex)
        {
            ownsMutex = false;
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
