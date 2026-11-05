namespace BellTower.Providers;

public interface ICodexNameClient : IDisposable
{
    Task<string?> ReadNameAsync(string thread, CancellationToken cancellation);
    Task SetNameAsync(string thread, string name, CancellationToken cancellation);
}
