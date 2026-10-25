using System.Diagnostics;
using System.Text.Json;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Setup;
using BellTower.Tests.Testing;
using BellTower.Updates;
using Xunit;

namespace BellTower.Tests.Updates;

public sealed class HookMaintenanceTests
{
    [Fact]
    public async Task CleanupHashingDoesNotBlockGeneratedClaudeCodexOrMixedLegacyHooks()
    {
        using var workspace = new TestWorkspace();
        var root = ProjectRoot();
        var installationRoot = Path.Combine(workspace.Root, "install");
        var dataRoot = Path.Combine(workspace.Root, "data");
        Directory.CreateDirectory(installationRoot);
        Directory.CreateDirectory(dataRoot);
        CopyDirectory(Path.Combine(root, "src", "BellTower.Launcher", "bin", "Release", "net10.0-windows"), installationRoot);

        var selected = AddPayload(root, installationRoot, "0.1.8");
        _ = AddPayload(root, installationRoot, "0.1.9001");
        DistributionJson.Write(Path.Combine(installationRoot, "installation.json"), new InstallationManifest());
        DistributionJson.Write(Path.Combine(installationRoot, "current.json"), new VersionSelection("0.1.8"));
        var installation = new Installation(installationRoot);
        var paths = new DataPaths(dataRoot);
        paths.Ensure();

        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        UpdateTransaction.PayloadRemovalTestCheckpoint = (candidateRoot, version) =>
        {
            if (candidateRoot.Equals(installationRoot, StringComparison.OrdinalIgnoreCase) && version == "0.1.9001")
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(30)))
                {
                    throw new TimeoutException("Test did not release the slow cleanup checkpoint.");
                }
            }
        };

        Task cleanup = Task.CompletedTask;
        Exception? primaryFailure = null;
        Exception? cleanupFailure = null;
        try
        {
            cleanup = Task.Run(() => UpdateTransaction.Cleanup(installation));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)), "Cleanup did not reach its slow payload verification phase.");

            var commands = new HookCommandBuilder(Path.Combine(installationRoot, "bell-tower.exe"), paths);
            var claude = await RunGeneratedHook(commands.Build("event", "claude"), "claude-one", "claude");
            Assert.True(claude.Succeeded, claude.Output);
            var codex = await RunGeneratedHook(commands.Build("event", "codex"), "codex-one", "codex");
            Assert.True(codex.Succeeded, codex.Output);
            var mixed = await Task.WhenAll(
                RunGeneratedHook(commands.Build("event", "claude"), "claude-mixed", "claude"),
                RunGeneratedHook(commands.Build("event", "codex"), "codex-mixed", "codex"));
            Assert.All(mixed, result => Assert.True(result.Succeeded, result.Output));
            Assert.False(cleanup.IsCompleted, "The slow maintenance phase should still be blocked while hooks complete.");
            Assert.True(Directory.Exists(Path.Combine(installationRoot, "versions", "0.1.9001")), "Cleanup must retain the payload whose verification is in progress.");

            var state = new StateStore(paths).Drain();
            Assert.Contains("claude:claude-one", state.Sessions.Keys);
            Assert.Contains("codex:codex-one", state.Sessions.Keys);
            Assert.Contains("claude:claude-mixed", state.Sessions.Keys);
            Assert.Contains("codex:codex-mixed", state.Sessions.Keys);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
        }
        finally
        {
            release.Set();
            UpdateTransaction.PayloadRemovalTestCheckpoint = null;
            try
            {
                await cleanup.WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (Exception exception)
            {
                cleanupFailure = exception;
            }
        }

        if (primaryFailure is not null && cleanupFailure is not null)
        { throw new AggregateException("The hook assertion and cleanup teardown both failed.", primaryFailure, cleanupFailure); }
        if (primaryFailure is not null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primaryFailure).Throw(); }
        if (cleanupFailure is not null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupFailure).Throw(); }
        Assert.True(Directory.Exists(selected));
        Assert.False(Directory.Exists(Path.Combine(installationRoot, "versions", "0.1.9001")));
    }

    private static async Task<HookResult> RunGeneratedHook(string command, string session, string provider)
    {
        var encoded = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", encoded })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { session_id = session, hook_event_name = "Stop", cwd = "D:/maintenance/" + provider }));
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(true);
            await process.WaitForExitAsync();
            var timedOutOutput = await output;
            var timedOutError = await error;
            return new HookResult(false, $"Generated launcher hook exceeded its three-second budget; exit={process.ExitCode}; stdout={timedOutOutput}; stderr={timedOutError}");
        }

        return new HookResult(process.ExitCode == 0, $"exit={process.ExitCode}; stdout={await output}; stderr={await error}");
    }

    private static string AddPayload(string projectRoot, string installationRoot, string version)
    {
        var payload = Path.Combine(installationRoot, "versions", version);
        Directory.CreateDirectory(payload);
        CopyDirectory(Path.Combine(projectRoot, "src", "BellTower", "bin", "Release", "net10.0-windows"), payload);
        File.Copy(Path.Combine(projectRoot, "src", "BellTower.Ingress", "bin", "Release", "net10.0-windows", "win-x64", "bell-tower-ingress.exe"),
            Path.Combine(payload, "bell-tower-ingress.exe"));
        File.WriteAllText(Path.Combine(payload, $"bell-tower-focus-{version}.vsix"), "hook maintenance fixture");
        var files = Directory.GetFiles(payload, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(payload, path).Replace('\\', '/'), Installation.Hash, StringComparer.Ordinal);
        DistributionJson.Write(Path.Combine(payload, "release.json"), new ReleaseContract(version, Capture: 1, AttachmentHealth: 1, Files: files));
        return payload;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        }
        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    private static string ProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ZeusApps.BellTower.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Project root missing.");
    }

    private sealed record HookResult(bool Succeeded, string Output);
}
