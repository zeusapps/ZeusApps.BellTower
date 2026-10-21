using System.Globalization;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed class VsCodeNavigator(DataPaths paths) : ITerminalNavigator
{
    public string HostKind => "vscode";
    public string Precision(Attachment attachment) => FindRegistration(attachment) is not null ? "exact terminal" : "unavailable";

    public async Task<FocusResult> FocusAsync(Attachment attachment)
    {
        var host = FindRegistration(attachment);
        if (host is null)
        {
            return new(false, "unavailable", "VS Code registration is unavailable or ambiguous.");
        }

        var shell = attachment.Host.Shell!;
        var request = new FocusRequest(Guid.NewGuid().ToString("N"), host.InstanceId, attachment.Id, shell.Pid,
            shell.StartedUtcTicks.ToString(CultureInfo.InvariantCulture), DateTimeOffset.UtcNow.AddSeconds(5));
        var pending = Path.Combine(paths.Requests, request.Id + ".json");
        AtomicFile.Write(pending, IO.Json.Encode(request));
        var receipt = Path.Combine(paths.Requests, request.Id + ".result");
        while (DateTimeOffset.UtcNow < request.ExpiresAt)
        {
            if (File.Exists(receipt))
            {
                var answer = AtomicFile.Read<FocusResult>(receipt);
                File.Delete(receipt);
                return answer ?? new(false, "unavailable", "Invalid focus acknowledgment.");
            }
            await Task.Delay(50).ConfigureAwait(false);
        }
        if (File.Exists(pending))
        {
            File.Delete(pending);
        }

        return new(false, "unavailable", "The VS Code bridge did not acknowledge focus.");
    }

    private HostRegistration? FindRegistration(Attachment attachment)
    {
        if (attachment.Host.Shell is not { } shell || ProcessProbe.Identify(shell.Pid) != shell)
        {
            return null;
        }

        var matches = new List<HostRegistration>();
        foreach (var file in Directory.EnumerateFiles(paths.Hosts, "*.json"))
        {
            try
            {
                var registration = AtomicFile.Read<HostRegistration>(file);
                if (registration is not null && registration.Process is not null && registration.TerminalPids is not null &&
                    DateTimeOffset.UtcNow - registration.UpdatedAt < TimeSpan.FromSeconds(15) &&
                    ProcessProbe.Identify(registration.Process.Pid) == registration.Process && registration.TerminalPids.Contains(shell.Pid))
                {
                    matches.Add(registration);
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                // A partial or stale registration does not identify a focus destination.
            }
        }
        return matches.Count == 1 ? matches[0] : null;
    }
}
