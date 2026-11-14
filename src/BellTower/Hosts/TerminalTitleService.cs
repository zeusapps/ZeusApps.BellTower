using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed class TerminalTitleService(DataPaths paths, IConsoleTitleWriter writer)
{
    private bool NativeEnabled() => new ConfigStore(paths).Read().NativePresentation;

    public bool Apply(DesiredTerminalPresentation desired)
    {
        if (string.IsNullOrEmpty(desired.Title) || desired.Title.Length > 512 || desired.Title.Any(char.IsControl) ||
            string.IsNullOrEmpty(desired.AttachmentId) || desired.AttachmentId.Length > 256 || desired.Agent is null || desired.Shell is null ||
            desired.Background is not null && !Core.Palette.IsHex(desired.Background))
        {
            return false;
        }
        using var gate = new DataLock(paths, "presentation");
        var stored = new AttachmentStore(paths).Get(desired.AttachmentId);
        if (stored is null)
        {
            return false;
        }
        // The setting is read again under the lock, so turning it off stops writes even for a request computed earlier.
        var native = NativePresentation.GrantsPresentation(stored, NativeEnabled());
        var attachment = NativePresentation.Effective(stored, native);
        if (attachment is not { ManagedTitle: true, EndedAt: null } || attachment.Health == "unavailable" ||
            attachment.Provider != desired.Provider || attachment.RegisteredAt != desired.RegisteredAt ||
            attachment.Process != desired.Agent || attachment.Host.Shell != desired.Shell ||
            attachment.Host.Kind is not ("vscode" or "windows-terminal") ||
            desired.Background is not null && !attachment.ManagedBackground ||
            ProcessProbe.Inspect(desired.Agent) != ProcessLiveness.Live || ProcessProbe.Inspect(desired.Shell) != ProcessLiveness.Live)
        {
            return false;
        }
        return native ? WriteNative(desired) : writer.Write(desired.Shell, desired.Agent, desired.Title, desired.Background);
    }

    // One record per shell. The prior title is read once, before the first write to that shell, and every later write by any session in
    // the shell reuses it, so the record can always give back the original title. The record names the last writer as its owner and
    // keeps what that writer wrote; Colored stays true across title-only rewrites.
    private bool WriteNative(DesiredTerminalPresentation desired)
    {
        var records = new TitleRestoreStore(paths);
        var existing = records.Get(desired.Shell);
        var previous = existing?.Previous ?? writer.ReadTitle(desired.Shell, desired.Agent);
        if (previous is null)
        {
            return false;
        }
        records.Save(new TitleRestore(desired.AttachmentId, desired.Shell, previous, desired.Title) { Colored = desired.Background is not null || existing is { Colored: true } });
        return writer.Write(desired.Shell, desired.Agent, desired.Title, desired.Background);
    }

    /// <summary>
    /// Gives back what Bell Tower changed on every shell whose record this attachment owns (it was the last writer). Writes only to the
    /// recorded shell, identified by process identifier and creation time, while it is live, and only while its console still shows
    /// Bell Tower's title. A dead or reused shell discards the record without a write. The record stays when the shell is live but its
    /// console cannot be verified. An attachment that owns no record has nothing to restore.
    /// </summary>
    public bool Restore(string attachmentId)
    {
        using var gate = new DataLock(paths, "presentation");
        var records = new TitleRestoreStore(paths);
        // After a resume the attachment can own a record in each shell it wrote to; each is restored independently, so a failure
        // keeps only its own record and never blocks the others.
        var restored = true;
        foreach (var record in records.OwnedBy(attachmentId))
        {
            if (ProcessProbe.Inspect(record.Shell) != ProcessLiveness.Live)
            {
                records.Remove(record.Shell);
            }
            else if (!writer.RestoreTitle(record.Shell, record.Written, record.Previous) || record.Colored && !writer.ResetColors(record.Shell))
            {
                restored = false;
            }
            else
            {
                records.Remove(record.Shell);
            }
        }
        return restored;
    }

    // Compatibility path for older callers. The tray uses only precomputed desired values.
    public bool Apply(string key)
    {
        using var gate = new DataLock(paths, "presentation");
        if (!new StateStore(paths).Read().Sessions.TryGetValue(key, out var session) || session.Ended || session.AttachmentHealth == "unavailable")
        {
            return false;
        }
        var stored = new AttachmentStore(paths).Get(session.AttachmentId);
        var native = stored is not null && NativePresentation.GrantsPresentation(stored, NativeEnabled());
        var attachment = stored is null ? null : NativePresentation.Effective(stored, native);
        if (attachment is not { ManagedTitle: true, EndedAt: null, Process: { } agent, Host.Shell: { } shell } ||
            attachment.Health == "unavailable" ||
            attachment.Host.Kind is not ("vscode" or "windows-terminal") ||
            ProcessProbe.Identify(agent.Pid) != agent || ProcessProbe.Identify(shell.Pid) != shell)
        {
            return false;
        }
        var identity = new IdentityStore(paths).Get(session.Key, session.Directory);
        var title = Core.SessionPresentation.Title(identity.Name, new GitReader().Read(session.Directory));
        if (native)
        {
            return WriteNative(new(attachment.Id, attachment.Provider, attachment.RegisteredAt, agent, shell, title, identity.Palette.Background));
        }
        return writer.Write(shell, agent, title, attachment.ManagedBackground ? identity.Palette.Background : null);
    }
}
