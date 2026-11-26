using System.Text.Json;
using System.Text.Json.Nodes;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// What one conversation shows on every surface at one moment, read from the installed product's own outputs: the identity from
/// <c>list</c>, the tray's presentation snapshot, the installed <c>statusline</c> command (badge), and the product functions that build the
/// tab title and the spoken phrase from those. Nothing here derives a name, color or context itself.
/// </summary>
public sealed record SurfaceSnapshot(
    string Key, string Name, string Accent, string ColorPin, string Change, string ManualName, string NativeTitle, string ActiveWork,
    string WorkFloorAt, string Checkout, string Branch, string Worktree, string Badge, string Tab, string ChipName, string ChipDetails,
    string Speech, string Provenance)
{
    /// <summary>The fields that must not change when nothing happened to the conversation (volatile timestamps and the checkout source excluded).</summary>
    public string Fingerprint => string.Join("\u001f", Name, Accent, ColorPin, Change, ManualName, NativeTitle, ActiveWork, WorkFloorAt, Checkout, Branch, Worktree, Badge, Tab, ChipName, ChipDetails, Speech, Provenance);
}

public static class ConversationSurfaces
{
    /// <summary>
    /// Reads one conversation's surfaces, or null when the tray's presentation snapshot for it is missing, stale or not yet at the current
    /// name (the caller waits and reads again).
    /// </summary>
    public static SurfaceSnapshot? Read(Instance instance, SessionRow row, string launchDirectory, AppState state)
    {
        var provider = row.Key[..row.Key.IndexOf(':')];
        var sessionId = row.Key[(row.Key.IndexOf(':') + 1)..];
        var paths = new DataPaths(instance.Layout.Data);
        var snapshot = new IdentityPresentationStore(paths).Read(row.Key);
        if (snapshot is null || snapshot.Title != row.Name || !state.Sessions.TryGetValue(row.Key, out var session))
        {
            return null;
        }
        var input = JsonSerializer.Serialize(new { session_id = sessionId, cwd = launchDirectory });
        var badge = instance.Run(instance.Layout.Launcher, ["statusline", "--provider", provider, "--no-color", "--data-dir", instance.Layout.Data], stdin: input, record: true);
        if (!badge.Succeeded)
        {
            throw new IOException("statusline failed: " + badge.Error.Trim());
        }
        var identity = Json.Decode<SessionIdentity>(row.Identity.ToJsonString());
        return new SurfaceSnapshot(
            row.Key, row.Name, TowerReader.Text(row.Identity, "palette", "accent"), TowerReader.Text(row.Identity, "colorPin"), TowerReader.Text(row.Identity, "change"),
            TowerReader.Text(row.Identity, "manualName"), TowerReader.Text(row.Identity, "nativeTitle"), TowerReader.Text(row.Identity, "activeWork"),
            TowerReader.Text(row.Identity, "workFloorAt"), TowerReader.Text(row.Identity, "workingCheckout"), snapshot.Git.Branch ?? "", snapshot.Git.Worktree ?? "",
            badge.Output.Trim(), SessionPresentation.Title(snapshot.Title, snapshot.Git),
            // The chip (shown only while a session waits for attention) is built from exactly these: the identity name and the git details string.
            identity.Name, snapshot.Git.ToString(), AlertPolicy.Phrase(session, identity), snapshot.Provenance);
    }

    /// <summary>
    /// Reads every listed conversation. Waits until each has a current presentation snapshot, then until two reads 1.5 s apart agree, so a
    /// tray tick in the middle of a change is never read as the settled state. Returns what it has when the wait ends.
    /// </summary>
    public static IReadOnlyDictionary<string, SurfaceSnapshot> ReadSettled(Instance instance, IReadOnlyCollection<string> keys, string launchDirectory, string candidateVersion, int timeoutMilliseconds = 60000)
    {
        RequireCandidate(instance, candidateVersion);
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        Dictionary<string, SurfaceSnapshot> previous = [];
        Dictionary<string, SurfaceSnapshot> current = [];
        while (DateTime.UtcNow < deadline)
        {
            instance.Abort.ThrowIfRequested();
            var rows = TowerReader.List(instance).ToDictionary(row => row.Key);
            var state = new StateStore(new DataPaths(instance.Layout.Data)).Read();
            current = [];
            foreach (var key in keys)
            {
                if (rows.TryGetValue(key, out var row) && Read(instance, row, launchDirectory, state) is { } snapshot)
                {
                    current[key] = snapshot;
                }
            }
            if (current.Count == keys.Count && previous.Count == keys.Count && keys.All(key => previous[key].Fingerprint == current[key].Fingerprint))
            {
                return current;
            }
            previous = current;
            Thread.Sleep(1500);
        }
        return current;
    }

    /// <summary>
    /// This reader decodes the installed state with the harness build's own types (the candidate's schema and policy) and reads the candidate's
    /// presentation store, so it supports only an installation whose launcher reports the candidate version (ANALYSIS-170, ANALYSIS-162). Against
    /// another version (the rolled-back 0.1.9) it fails with this message instead of judging that state with the candidate's policy; read such an
    /// installation through its own commands and the raw files.
    /// </summary>
    public static void RequireCandidate(Instance instance, string candidateVersion)
    {
        var installed = instance.Tower("--version").Output.Trim();
        if (installed != candidateVersion)
        {
            throw new InvalidOperationException("The surfaces reader supports only the candidate installation (" + candidateVersion + "); this instance reports " + installed +
                ". Read an older installation through its own commands, never with the candidate's policy.");
        }
    }

    public static JsonObject Identity(Instance instance, string key) => TowerReader.List(instance).Single(row => row.Key == key).Identity;
}
