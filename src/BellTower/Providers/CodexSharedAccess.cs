using System.Net.Sockets;
using BellTower.Core;

namespace BellTower.Providers;

/// <summary>
/// Long-lived opener for non-owning shared-backend connections. It applies the existing failure backoff per Codex home so a
/// missing, refusing or slow backend is not retried in a loop, and it reports endpoint replacement (a different backend process
/// than the last one verified for the home) so the change is visible to the caller. The backend is derived from the connected
/// socket, so the caller supplies only the resolved Codex home. Backoff and replacement share one normalized home key
/// (<see cref="CodexSharedEndpoint.HomeKey"/>). Each open is one peer-verified connection; a different process that took over the
/// path is seen as the next open's differing peer (<c>Replaced</c>), not by probing the established connection.
/// It owns no backend: callers dispose connections, which closes only Bell Tower's side.
/// </summary>
public sealed class CodexSharedAccess(NativeNameSchedule? schedule = null, CodexTransportBudget? budget = null,
    CodexBackendCheck? check = null, Func<Socket, int?>? peerOf = null)
{
    private const string Attempt = "shared";

    // Zero steady-state interval: only a recorded failure delays the next attempt.
    private readonly NativeNameSchedule schedule = schedule ?? new(TimeSpan.Zero);
    private readonly Dictionary<string, ProcessIdentity> verified = new(StringComparer.Ordinal);
    private readonly object gate = new();
    // Per home: bumped under the gate on every recorded failure and every success, so an attempt compares the generation it
    // started in and an overlapping failure, or one that spans a success, is never counted twice.
    private readonly Dictionary<string, int> generations = new(StringComparer.Ordinal);

    /// <summary>
    /// Opens and initializes a connection to the backend of <paramref name="codexHome"/>. <c>Backend</c> is the verified
    /// identity of the process serving the socket. <c>Replaced</c> is true when it differs from the last backend successfully
    /// verified for this home; a failed attempt in between does not forget that backend. <paramref name="expected"/> is an
    /// optional extra comparison, never the source of the identity.
    /// </summary>
    public async Task<(CodexConnection Connection, bool Replaced, ProcessIdentity Backend)> OpenAsync(string codexHome, CancellationToken cancellation, ProcessIdentity? expected = null)
    {
        var key = CodexSharedEndpoint.HomeKey(codexHome);
        int started;
        lock (gate)
        {
            // The due check and the generation are one atomic step: overlapping attempts that fail in the same window count once.
            if (!schedule.Due(key, Attempt))
            {
                throw new CodexTransportException(CodexTransportFailure.BackedOff, "The shared Codex connection is backing off after a failure.");
            }
            started = generations.GetValueOrDefault(key);
        }
        var transport = new CodexSharedTransport(codexHome, expected, budget, check, peerOf);
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
                lock (gate)
                {
                    if (generations.GetValueOrDefault(key) == started)
                    {
                        schedule.Failed(key, Attempt);
                        generations[key] = started + 1;
                    }
                }
            }
            throw;
        }
        var backend = transport.Backend!;
        lock (gate)
        {
            schedule.Succeeded(key, Attempt);
            generations[key] = generations.GetValueOrDefault(key) + 1;
            var replaced = verified.TryGetValue(key, out var previous) && previous != backend;
            verified[key] = backend;
            return (connection, replaced, backend);
        }
    }

    public int Failures(string codexHome) => schedule.Failures(CodexSharedEndpoint.HomeKey(codexHome));
}
