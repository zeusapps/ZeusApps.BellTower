using System.Diagnostics;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Tray;
using BellTower.Updates;

namespace BellTower.Setup;

public sealed record IngressProvision(string Source, string Hash, string Target, string[] Providers, CodexHookShell? CodexShell = null, string? Launcher = null)
{
    public static IngressProvision? Plan(string executable, DataPaths paths, IEnumerable<string>? providers = null)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        if (!File.Exists(Path.Combine(root, "installation.json")))
        {
            return null; // Development CLI keeps its separately tested normalized event path.
        }
        var installation = new Installation(root);
        var payload = installation.Payload(installation.Selection().Version);
        var contract = DistributionJson.Read<ReleaseContract>(Path.Combine(payload, "release.json"));
        if (contract.Capture != CaptureProtocol.Version || contract.Files?.GetValueOrDefault("bell-tower-ingress.exe") is not { } hash)
        {
            throw new InvalidDataException("Selected package has no compatible fast ingress. Install a complete compatible package before setup.");
        }
        var source = Path.Combine(payload, "bell-tower-ingress.exe");
        if (!File.Exists(source) || Installation.Hash(source) != hash)
        {
            throw new InvalidDataException("Packaged ingress hash mismatch.");
        }
        var target = Path.Combine(paths.Root, "integrations", "provider-ingress", "1", hash, "bell-tower-ingress.exe");
        var selectedProviders = (providers ?? []).Distinct(StringComparer.Ordinal).ToArray();
        CodexHookShell? codexShell = OperatingSystem.IsWindows() && selectedProviders.Contains("codex", StringComparer.Ordinal)
            ? ProviderHookProbe.SelectCodexShell(executable, source)
            : null;
        return new(source, hash, target, selectedProviders, codexShell, installation.Launcher);
    }

    public void Apply(DataPaths paths)
    {
        if (new TrayController(paths).IsRunning())
        {
            var presence = DistributionJson.Read<TrayPresence>(UpdateTransaction.Presence(paths), 4096);
            if (presence.Capture != CaptureProtocol.Version)
            {
                throw new IOException("Restart monitoring with the selected version before enabling fast hooks; agent sessions can remain open.");
            }
        }
        Installation.EnsurePlainPath(Target);
        if (Installation.Hash(Source) != Hash)
        {
            throw new IOException("Ingress changed since preview; rerun setup.");
        }
        var createdTarget = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Target)!);
            if (!File.Exists(Target))
            {
                var temporary = Target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.Copy(Source, temporary);
                    if (Installation.Hash(temporary) != Hash)
                    {
                        throw new IOException("Ingress copy verification failed.");
                    }
                    File.Move(temporary, Target);
                    createdTarget = true;
                }
                finally
                {
                    if (File.Exists(temporary))
                    {
                        File.Delete(temporary);
                    }
                }
            }
            if (Installation.Hash(Target) != Hash)
            {
                throw new IOException("Retained ingress was modified; referenced images are never overwritten.");
            }

            // Validate the exact immutable helper path that will be written into provider settings.
            // Provider events still go to a disposable data directory until both provider probes pass.
            foreach (var provider in Providers)
            {
                ProviderHookProbe.Verify(provider, Source, Target, provider == "codex" ? CodexShell : null);
            }

            var start = new ProcessStartInfo(Target)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("--protocol-info");
            using var process = Process.Start(start) ?? throw new IOException("Ingress probe could not start.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(3000))
            {
                process.Kill(); // Only the disposable protocol-probe process belongs to this operation.
                throw new IOException("Ingress protocol probe timed out.");
            }
            if (process.ExitCode != 0 || error.GetAwaiter().GetResult().Length != 0 ||
                output.GetAwaiter().GetResult().Trim() != "{\"protocol\":1,\"minimumConsumer\":1,\"maximumConsumer\":1}")
            {
                throw new InvalidDataException("Ingress protocol probe failed.");
            }
            AtomicFile.Write(Path.Combine(paths.Root, "capture-protocol.json"), "{\"protocol\":1}");
            // The ingress starts a stopped tray only from this recorded stable launcher (never a versioned payload) and searches nowhere.
            if (Launcher is not null)
            {
                HookTrayStart.RecordLauncher(paths.Root, Launcher);
            }
        }
        catch
        {
            if (createdTarget && File.Exists(Target) && Installation.Hash(Target) == Hash)
            {
                File.Delete(Target);
            }
            throw;
        }
    }
}
