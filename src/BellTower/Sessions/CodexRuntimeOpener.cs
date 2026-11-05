using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;

namespace BellTower.Sessions;

/// <summary>
/// How Bell Tower reaches the runtime of one Codex conversation. The record carries no credential: an owned runtime names the
/// token file, which is read only when connecting. <see cref="Key"/> identifies the runtime the connection reaches.
/// </summary>
public abstract record CodexAccess
{
    public abstract string Key { get; }

    /// <summary>A runtime Bell Tower started: authenticated loopback, with the recorded endpoint.</summary>
    public sealed record Owned(string Endpoint, string TokenPath) : CodexAccess
    {
        public override string Key => Endpoint;
    }

    /// <summary>An existing shared backend of the resolved Codex home, observed without taking ownership.</summary>
    public sealed record Shared(string Home) : CodexAccess
    {
        public override string Key => Home;
    }
}

/// <summary>
/// The one selection rule and the one opener for Codex runtime access, used by both the native-name service and the permission
/// pump. A conversation with a verified owned runtime record reaches it over authenticated loopback; a shared-backend
/// conversation reaches the backend of the resolved Codex home through <see cref="CodexSharedAccess"/>; anything else has no
/// access. Both paths return an already connected <see cref="CodexConnection"/>, so a service never connects a client itself.
/// A single instance is shared by the services so the per-home backoff and replacement tracking are shared too.
/// </summary>
public sealed class CodexRuntimeOpener(DataPaths paths, CodexSharedAccess? shared = null, Func<string>? home = null)
{
    private readonly CodexSharedAccess shared = shared ?? new();
    private readonly Func<string> home = home ?? CodexSharedEndpoint.DefaultHome;

    /// <summary>The access path for a live Codex conversation, or null when none is verified.</summary>
    public CodexAccess? Select(SessionState session)
    {
        if (session.Provider != "codex")
        {
            return null;
        }
        var attachment = new AttachmentStore(paths).Get(session.AttachmentId);
        if (attachment is not { EndedAt: null })
        {
            return null;
        }
        var runtimes = new CodexRuntimeStore(paths);
        if (runtimes.Get(session.AttachmentId) is { } runtime)
        {
            return attachment.Host.Shell == runtime.Shell ? new CodexAccess.Owned(runtime.Endpoint, runtimes.TokenPath(runtime.Attachment)) : null;
        }
        // Only a hook the classifier proved to come from a shared backend reaches the backend of the home; an unknown mode fails closed.
        return ConversationLifecycle.IsSharedConversation(attachment) && AttachmentEvidence.Of(attachment).Mode == AgentModes.SharedBackend
            ? new CodexAccess.Shared(home()) : null;
    }

    public async Task<CodexConnection> ConnectAsync(CodexAccess access, CancellationToken cancellation)
    {
        switch (access)
        {
            case CodexAccess.Owned owned:
                var connection = new CodexConnection();
                try
                {
                    await connection.ConnectAsync(owned.Endpoint, await File.ReadAllTextAsync(owned.TokenPath, cancellation).ConfigureAwait(false), cancellation).ConfigureAwait(false);
                }
                catch
                {
                    connection.Dispose();
                    throw;
                }
                return connection;
            case CodexAccess.Shared sharedAccess:
                var opened = await shared.OpenAsync(sharedAccess.Home, cancellation).ConfigureAwait(false);
                if (opened.Replaced)
                {
                    // A different backend process now serves the home; the next reads come from it. Recorded as a code only.
                    new LocalLog(paths).Write("codex-shared-backend-replaced");
                }
                return opened.Connection;
            default:
                throw new ArgumentException("Unsupported Codex access.");
        }
    }
}
