using System.Net.Sockets;
using BellTower.Core;

namespace BellTower.Providers;

/// <summary>
/// Long-lived opener for non-owning shared-backend connections. It applies the existing failure backoff per Codex home so a
/// missing, refusing or slow backend is not retried in a loop, and it reports endpoint replacement (a different backend process
/// than the last one verified for the home) so the change is visible to the caller. The backend is derived from the connected
/// socket, so the caller supplies only the resolved Codex home.
/// It owns no backend: callers dispose connections, which closes only Bell Tower's side.
/// </summary>
public sealed class CodexSharedAccess(NativeNameSchedule? schedule = null, CodexTransportBudget? budget = null,
    Func<ProcessIdentity, bool>? isBackend = null, Func<Socket, int?>? peerOf = null)
{
    private const string Attempt = "shared";

    // Zero steady-state interval: only a recorded failure delays the next attempt.
    private readonly NativeNameSchedule schedule = schedule ?? new(TimeSpan.Zero);
    private readonly Dictionary<string, ProcessIdentity> verified = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();

    /// <summary>
    /// Opens and initializes a connection to the backend of <paramref name="codexHome"/>. <c>Backend</c> is the verified
    /// identity of the process serving the socket. <c>Replaced</c> is true when it differs from the last backend successfully
    /// verified for this home; a failed attempt in between does not forget that backend. <paramref name="expected"/> is an
    /// optional extra comparison, never the source of the identity.
    /// </summary>
    public async Task<(CodexConnection Connection, bool Replaced, ProcessIdentity Backend)> OpenAsync(string codexHome, CancellationToken cancellation, ProcessIdentity? expected = null)
    {
        if (!schedule.Due(codexHome, Attempt))
        {
            throw new CodexTransportException(CodexTransportFailure.BackedOff, "The shared Codex connection is backing off after a failure.");
        }
        var transport = new CodexSharedTransport(codexHome, expected, budget, isBackend, peerOf);
        var connection = new CodexConnection(transport);
        try
        {
            await connection.ConnectAsync(cancellation).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            connection.Dispose();
            if (exception is not OperationCanceledException || !cancellation.IsCancellationRequested)
            {
                schedule.Failed(codexHome, Attempt);
            }
            throw;
        }
        schedule.Succeeded(codexHome, Attempt);
        var backend = transport.Backend!;
        lock (gate)
        {
            var replaced = verified.TryGetValue(codexHome, out var previous) && previous != backend;
            verified[codexHome] = backend;
            return (connection, replaced, backend);
        }
    }

    public int Failures(string codexHome) => schedule.Failures(codexHome);
}
