using System.Globalization;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed class VsCodeNavigator(DataPaths paths, VsCodeWindowOwnership ownership, WindowActivator activator, Func<TimeSpan, Task>? delay = null,
    Func<DateTimeOffset>? clock = null) : ITerminalNavigator
{
    /// <summary>A registration snapshot is reused for at most this long (about one tray tick) and never past its own freshness.</summary>
    public static readonly TimeSpan SnapshotLifetime = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan ReceiptSweepAge = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RegistrationFreshness = TimeSpan.FromSeconds(15);
    private readonly Func<DateTimeOffset> now = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly object snapshotGate = new();
    private (DateTimeOffset Taken, List<HostRegistration> Registrations)? snapshot;

    public VsCodeNavigator(DataPaths paths) : this(paths, VsCodeWindowOwnership.Native(), new WindowActivator(new NativeWindowActivationApi()))
    {
    }

    public string HostKind => "vscode";

    /// <summary>
    /// "exact terminal" only when the owning window is verified; "terminal only" when the terminal is
    /// registered but its window cannot be proven, so foreground activation would be refused.
    /// </summary>
    public string Precision(Attachment attachment)
    {
        // Called for every chip on every tray tick: use the per-tick snapshot instead of re-reading every file.
        var fresh = Snapshot();
        return Match(attachment, fresh) is not { } host ? "unavailable"
            : ownership.Verify(host, fresh, checkLineage: false).Verified ? "exact terminal" : "terminal only";
    }

    /// <summary>The verification state of a session destination for diagnostics: precision and reason.</summary>
    public (string Precision, string Reason) Explain(Attachment attachment)
    {
        var fresh = ReadFresh();
        if (Match(attachment, fresh) is not { } host)
        {
            return ("unavailable", "No single fresh VS Code bridge registration contains this terminal.");
        }

        var verdict = ownership.Verify(host, fresh, checkLineage: true);
        return verdict.Verified ? ("exact terminal", "verified") : ("terminal only", verdict.Reason);
    }

    /// <summary>Verification of every fresh registration, independent of any session.</summary>
    public IReadOnlyList<(string Instance, int Terminals, string State, string Reason)> Windows()
    {
        var fresh = ReadFresh();
        return fresh.Select(host =>
        {
            var verdict = ownership.Verify(host, fresh, checkLineage: true);
            var state = verdict.Verified ? "verified" : host.Protocol is null or < VsCodeWindowOwnership.WindowProtocol ? "partial" : "unverified";
            return (host.InstanceId[..Math.Min(8, host.InstanceId.Length)], host.TerminalPids.Count, state, verdict.Reason);
        }).ToList();
    }

    public async Task<FocusResult> FocusAsync(Attachment attachment)
    {
        var fresh = ReadFresh();
        var host = Match(attachment, fresh);
        if (host is null)
        {
            return new(false, "unavailable", "VS Code registration is unavailable or ambiguous.");
        }

        var verdict = ownership.Verify(host, fresh, checkLineage: true);
        // Activation is requested first, while the user's click still grants this process foreground rights.
        var activated = verdict.Verified && await activator.ActivateConfirmedAsync(new IntPtr(verdict.Handle), delay).ConfigureAwait(false);
        var reveal = await RevealAsync(host, attachment, activated ? verdict.Handle : null).ConfigureAwait(false);
        if (!verdict.Verified)
        {
            // A reveal alone, or a legacy bridge's own focus claim, never counts as foreground success.
            return new(false, reveal.Success ? "exact terminal" : "unavailable",
                (reveal.Success ? "The terminal was selected, but " : "") + verdict.Reason);
        }

        if (!activated)
        {
            return new(false, "exact terminal", "Windows did not bring the owning VS Code window to the foreground.");
        }

        if (!reveal.Success)
        {
            return reveal;
        }

        return activator.IsForeground(new IntPtr(verdict.Handle))
            ? new(true, "exact terminal", "The owning VS Code window and its terminal were brought forward.")
            : new(false, "exact terminal", "The VS Code window lost the foreground before navigation completed.");
    }

    private async Task<FocusResult> RevealAsync(HostRegistration host, Attachment attachment, long? activatedWindow)
    {
        SweepReceipts();
        var shell = attachment.Host.Shell!;
        var request = new FocusRequest(Guid.NewGuid().ToString("N"), host.InstanceId, attachment.Id, shell.Pid,
            shell.StartedUtcTicks.ToString(CultureInfo.InvariantCulture), now().AddSeconds(5), WindowHandle: activatedWindow);
        var pending = Path.Combine(paths.Requests, request.Id + ".json");
        AtomicFile.Write(pending, IO.Json.Encode(request));
        var receipt = Path.Combine(paths.Requests, request.Id + ".result");
        while (now() < request.ExpiresAt)
        {
            if (File.Exists(receipt))
            {
                FocusResult? answer = null;
                try
                {
                    answer = AtomicFile.Read<FocusResult>(receipt);
                }
                catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
                {
                    // An unreadable receipt is answered as invalid below and still cleaned up.
                }
                finally
                {
                    Remove(receipt);
                    Remove(pending);
                }
                return answer ?? new(false, "unavailable", "Invalid focus acknowledgment.");
            }
            await Task.Delay(50).ConfigureAwait(false);
        }
        // Neither file may outlive the request; a late receipt is removed by the sweep of the next request.
        Remove(pending);
        Remove(receipt);

        return new(false, "unavailable", "The VS Code bridge did not acknowledge focus.");
    }

    private static void Remove(string file)
    {
        // The bridge may be reading the file at this moment; retry briefly. Cleanup is best effort and must never
        // turn a navigation result into an error.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                File.Delete(file);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(10);
            }
        }
    }

    /// <summary>Removes receipts orphaned by a request that timed out. The age is far above the five second expiry.</summary>
    private void SweepReceipts()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(paths.Requests, "*.result"))
            {
                if (now().UtcDateTime - File.GetLastWriteTimeUtc(file) > ReceiptSweepAge)
                {
                    Remove(file);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The folder may be gone or busy; the next request sweeps again.
        }
    }

    private static HostRegistration? Match(Attachment attachment, IReadOnlyList<HostRegistration> fresh)
    {
        if (attachment.Host.Shell is not { } shell || ProcessProbe.Identify(shell.Pid) != shell)
        {
            return null;
        }

        var matches = fresh.Where(registration => registration.TerminalPids.Contains(shell.Pid)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>
    /// The chip refresh runs every tray tick for every session. Reuse one read for about a tick; the 15 second
    /// freshness is re-applied to the cached registrations on every use, so a snapshot never outlives it.
    /// </summary>
    private List<HostRegistration> Snapshot()
    {
        lock (snapshotGate)
        {
            if (snapshot is not { } current || now() - current.Taken > SnapshotLifetime)
            {
                snapshot = (now(), ReadFresh());
                current = snapshot.Value;
            }

            return current.Registrations.Where(registration => now() - registration.UpdatedAt < RegistrationFreshness).ToList();
        }
    }

    private List<HostRegistration> ReadFresh()
    {
        var live = new List<HostRegistration>();
        foreach (var file in Directory.EnumerateFiles(paths.Hosts, "*.json"))
        {
            try
            {
                var registration = ReadRegistration(file);
                if (registration is not null && registration.Process is not null && registration.TerminalPids is not null &&
                    now() - registration.UpdatedAt < RegistrationFreshness &&
                    ProcessProbe.Identify(registration.Process.Pid) == registration.Process)
                {
                    live.Add(registration);
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
            {
                // A partial or stale registration does not identify a focus destination.
            }
        }
        return live;
    }

    /// <summary>
    /// Reads a bridge registration without blocking the bridge atomic replace: the file is opened with
    /// FileShare.Delete so a concurrent rename does not fail. Scoped to registrations; AtomicFile.Read is unchanged.
    /// </summary>
    public static HostRegistration? ReadRegistration(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > 64 * 1024)
        {
            throw new InvalidDataException("Registration exceeds limit.");
        }

        using var reader = new StreamReader(stream);
        return IO.Json.Decode<HostRegistration>(reader.ReadToEnd());
    }
}
