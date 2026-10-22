using System.Security.Cryptography;
using System.Text;

namespace BellTower.Distribution;

public sealed class InstallationGate : IDisposable
{
    private readonly Mutex mutex;
    public InstallationGate(string root, string area = "launch", int timeoutMilliseconds = 10000)
    {
        mutex = new Mutex(false, "Local\\BellTower-install-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root).ToUpperInvariant() + area))));
        try
        {
            if (!mutex.WaitOne(timeoutMilliseconds))
            { mutex.Dispose(); throw new IOException("Another installation operation is busy; retry later."); }
        }
        catch (AbandonedMutexException) { }
    }
    public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
}
