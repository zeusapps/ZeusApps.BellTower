namespace BellTower.Providers;

public interface ICodexAttentionClient : IDisposable
{
    Task ConnectAsync(string endpoint, string token, CancellationToken cancellation);
    Task<bool?> ReadPermissionPendingAsync(string thread, CancellationToken cancellation);
    // Owning-runtime question dialog status (waitingOnUserInput); null when the status is unknown. Confirmation evidence only.
    Task<bool?> ReadUserInputPendingAsync(string thread, CancellationToken cancellation) => Task.FromResult<bool?>(null);
}
