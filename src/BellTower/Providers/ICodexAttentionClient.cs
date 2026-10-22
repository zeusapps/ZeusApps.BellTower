namespace BellTower.Providers;

public interface ICodexAttentionClient : IDisposable
{
    Task ConnectAsync(string endpoint, string token, CancellationToken cancellation);
    Task<bool?> ReadPermissionPendingAsync(string thread, CancellationToken cancellation);
}
