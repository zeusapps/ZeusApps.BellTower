namespace BellTower.FieldAcceptance.Isolation;

/// <summary>
/// One harness run at a time. Concurrent runs cannot work: the preflight sweep of the second run ends the first run's trays and tasks,
/// and the production comparison of each run would count the other run's scheduled task as a production change. A named mutex held for
/// the whole run turns that into a clear refusal. Windows releases a mutex whose owner was killed (the next acquisition sees it as
/// abandoned and succeeds), so a forced kill never locks later runs out.
/// </summary>
public sealed class RunLock : IDisposable
{
    public const string DefaultName = @"Local\BellTower.FieldAcceptance";

    private Mutex? mutex;

    private RunLock(Mutex mutex) => this.mutex = mutex;

    /// <summary>Takes the lock on the calling thread (release it on the same thread), or returns null when another run holds it.</summary>
    public static RunLock? TryAcquire(string name = DefaultName)
    {
        var mutex = new Mutex(false, name);
        try
        {
            try
            {
                if (!mutex.WaitOne(0))
                {
                    mutex.Dispose();
                    return null;
                }
            }
            catch (AbandonedMutexException)
            {
                // The previous owner was killed; the mutex is ours now.
            }
            return new RunLock(mutex);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        var held = Interlocked.Exchange(ref mutex, null);
        if (held is null)
        {
            return;
        }
        try
        {
            held.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Released from another thread or already released; the handle close below ends ownership with the process anyway.
        }
        held.Dispose();
    }
}
