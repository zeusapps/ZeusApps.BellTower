namespace BellTower.Providers;

public interface ICodexNameClient : IDisposable
{
    Task ConnectAsync(string endpoint, string token, CancellationToken cancellation);
    Task<string?> ReadNameAsync(string thread, CancellationToken cancellation);
    Task SetNameAsync(string thread, string name, CancellationToken cancellation);
}
