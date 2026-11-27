using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BellTower.Core;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Setup;

public sealed class FastHookSetupTests
{
    [Fact]
    public void CompletedCodexRunnerAcceptsValidatedCaptureMissedByFinalPoll()
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow;
        using var input = JsonDocument.Parse("""{"session_id":"probe-session","cwd":"D:/probe","hook_event_name":"SessionStart","source":"startup"}""");
        var attachment = new Attachment("probe-attachment", "codex", null, new(), "D:/probe", at);
        var envelope = CaptureFactory.Create("codex", input.RootElement, attachment, at)!;
        var path = CaptureSpool.Write(workspace.Paths.Root, envelope);

        // Models a short-lived runner writing a valid capture and exiting after the last in-loop poll.
        Assert.True(ProviderHookProbe.CaptureConfirmsCompletedProbe(
            observedWhileRunning: false, timedOut: false, runnerExited: true, data: workspace.Paths, provider: "codex"));
        Assert.False(ProviderHookProbe.CaptureConfirmsCompletedProbe(
            observedWhileRunning: false, timedOut: true, runnerExited: true, data: workspace.Paths, provider: "codex"));
        Assert.False(ProviderHookProbe.CaptureConfirmsCompletedProbe(
            observedWhileRunning: false, timedOut: false, runnerExited: true, data: workspace.Paths, provider: "claude"));

        File.Delete(path);
        var inbox = Path.Combine(workspace.Paths.Root, CaptureProtocol.InboxName);
        File.WriteAllText(Path.Combine(inbox, "invalid.json"), "{}");
        Assert.False(ProviderHookProbe.CaptureConfirmsCompletedProbe(
            observedWhileRunning: false, timedOut: false, runnerExited: true, data: workspace.Paths, provider: "codex"));
        Assert.False(ProviderHookProbe.CaptureConfirmsCompletedProbe(
            observedWhileRunning: false, timedOut: false, runnerExited: false, data: workspace.Paths, provider: "codex"));
    }

    [Fact]
    public void MissingCodexCliReportsItselfInsteadOfTheShellSelectionFailure()
    {
        var failure = Assert.Throws<IOException>(() => ProviderHookProbe.RequireCodex(null));
        Assert.Contains("Codex CLI is not available", failure.Message, StringComparison.Ordinal);
        Assert.Equal("codex.exe", ProviderHookProbe.RequireCodex("codex.exe"));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void OptInNativeProbeConfirmsRealProviderHookDelivery(string provider)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("BELLTOWER_RUN_NATIVE_HOOK_PROBES"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var ingress = Environment.GetEnvironmentVariable("BELLTOWER_HOOK_PROBE_INGRESS");
        Assert.False(string.IsNullOrWhiteSpace(ingress), "Set BELLTOWER_HOOK_PROBE_INGRESS to a standalone packaged ingress executable.");
        Assert.True(File.Exists(ingress), "The standalone ingress candidate must exist.");
        using var workspace = new TestWorkspace();
        var targetDirectory = Path.Combine(workspace.Root, "Δ O'Brien helper");
        Directory.CreateDirectory(targetDirectory);
        var target = Path.Combine(targetDirectory, "bell-tower-ingress.exe");
        File.Copy(ingress!, target);
        ProviderHookProbe.Verify(provider, "bell-tower.exe", target);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void OptInNativeSetupProbeFailureLeavesProviderSettingsAndOwnershipBytesUnchanged(string provider)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("BELLTOWER_RUN_NATIVE_HOOK_PROBES"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var ingress = Environment.GetEnvironmentVariable("BELLTOWER_HOOK_PROBE_INGRESS");
        Assert.False(string.IsNullOrWhiteSpace(ingress));
        Assert.True(File.Exists(ingress));

        using (var passing = new TestWorkspace())
        {
            var (changes, originals) = SetupSettingsFixture(passing, provider);
            var provision = NativeProvision(ingress!, passing, provider, "passing");
            new SetupPlan(changes, ingress: provision).Apply(passing.Paths);
            foreach (var change in changes)
            {
                Assert.Equal(change.After, File.ReadAllText(change.Path));
                Assert.NotEqual(originals[change.Path], File.ReadAllBytes(change.Path));
            }
        }

        using (var failing = new TestWorkspace())
        {
            var (changes, originals) = SetupSettingsFixture(failing, provider);
            var invalidHelper = Path.Combine(failing.Root, "invalid-ingress.exe");
            File.WriteAllText(invalidHelper, "not a standalone executable");
            var provision = NativeProvision(invalidHelper, failing, provider, "failing");
            Assert.NotNull(Record.Exception(() => new SetupPlan(changes, ingress: provision).Apply(failing.Paths)));
            foreach (var change in changes)
            {
                Assert.Equal(originals[change.Path], File.ReadAllBytes(change.Path));
            }
            Assert.False(File.Exists(Path.Combine(failing.Paths.Root, "capture-protocol.json")));
        }
    }

    private static (List<FileChange> Changes, Dictionary<string, byte[]> Originals) SetupSettingsFixture(TestWorkspace workspace, string provider)
    {
        var settings = Path.Combine(workspace.Root, provider + (provider == "claude" ? "-settings.json" : "-hooks.json"));
        var ownership = Path.Combine(workspace.Paths.Root, "integration.json");
        var changes = new List<FileChange>
        {
            new(settings, "{\"existing\":\"settings\"}", "{\"updated\":\"settings\"}"),
            new(ownership, "{\"existing\":\"ownership\"}", "{\"updated\":\"ownership\"}")
        };
        foreach (var change in changes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(change.Path)!);
            File.WriteAllText(change.Path, change.Before!);
        }
        return (changes, changes.ToDictionary(change => change.Path, change => File.ReadAllBytes(change.Path), StringComparer.Ordinal));
    }

    private static IngressProvision NativeProvision(string source, TestWorkspace workspace, string provider, string scenario)
    {
        var target = Path.Combine(workspace.Paths.Root, "integrations", provider, scenario, "bell-tower-ingress.exe");
        return new(source, Installation.Hash(source), target, [provider]);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void FastCommandsQuoteUnicodeSpacesAndApostrophesWithoutPowerShell(string provider)
    {
        var paths = new DataPaths("D:/Δ O'Brien/%BELLTOWER_HOOK_PROBE_EXPANSION% ! & ^ (x)/data");
        var command = new HookCommandBuilder("unused", paths, "D:/Δ O'Brien/%BELLTOWER_HOOK_PROBE_EXPANSION% ! & ^ (x)/helper.exe").Build("event", provider);
        Assert.Contains("Δ", command);
        Assert.Contains("--provider " + provider, command);
        Assert.DoesNotContain("powershell", command, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EncodedCommand", command);
        Assert.DoesNotContain("unused", command);
        Assert.Contains(provider == "claude" || !OperatingSystem.IsWindows() ? "'\"'\"'" : "O''Brien", command);
        if (provider == "codex" && OperatingSystem.IsWindows())
        {
            Assert.StartsWith("& ", command, StringComparison.Ordinal);
            Assert.Contains("O''Brien", command, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CodexWindowsCommandsAreSpecificToTheShellAndKeepPercentTokensLiteral()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var paths = new DataPaths("D:/Δ O'Brien/%BELLTOWER_HOOK_PROBE_EXPANSION% ! & ^ (x)/data");
        const string helper = "D:/Δ O'Brien/%BELLTOWER_HOOK_PROBE_EXPANSION% ! & ^ (x)/helper.exe";
        var powershell = new HookCommandBuilder("unused", paths, helper, CodexHookShell.PowerShell).Build("event", "codex");
        var commandPrompt = new HookCommandBuilder("unused", paths, helper, CodexHookShell.Cmd).Build("event", "codex");
        var bash = new HookCommandBuilder("unused", paths, helper, CodexHookShell.Bash).Build("event", "codex");

        Assert.StartsWith("& 'D:/Δ O''Brien/%BELLTOWER_HOOK_PROBE_EXPANSION%", powershell, StringComparison.Ordinal);
        Assert.StartsWith("\"D:/Δ O'Brien/\"^%\"BELLTOWER_HOOK_PROBE_EXPANSION\"^%\"", commandPrompt, StringComparison.Ordinal);
        Assert.StartsWith("'D:/Δ O'\"'\"'Brien/%BELLTOWER_HOOK_PROBE_EXPANSION%", bash, StringComparison.Ordinal);
        Assert.Contains("! & ^ (x)", powershell, StringComparison.Ordinal);
        Assert.Contains("! & ^ (x)", commandPrompt, StringComparison.Ordinal);
        Assert.Contains("! & ^ (x)", bash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CodexWindowsFastCommandRunsInPowerShellAndPreservesArgumentBoundaries()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new TestWorkspace();
        var helperDirectory = Path.Combine(workspace.Root, "Δ O'Brien helper");
        Directory.CreateDirectory(helperDirectory);
        var result = Path.Combine(helperDirectory, "arguments.txt");
        var helper = Path.Combine(helperDirectory, "record-arguments.ps1");
        File.WriteAllText(helper, "[IO.File]::WriteAllText('" + result.Replace("'", "''") + "', ($args -join '|'))");

        var paths = new DataPaths(Path.Combine(workspace.Root, "Δ O'Brien %BELLTOWER_HOOK_PROBE_EXPANSION% ! & ^ (x) data"));
        var command = new HookCommandBuilder("unused", paths, helper).Build("event", "codex");
        var processInfo = new ProcessStartInfo("pwsh.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        processInfo.ArgumentList.Add("-NoLogo");
        processInfo.ArgumentList.Add("-NoProfile");
        processInfo.ArgumentList.Add("-NonInteractive");
        processInfo.ArgumentList.Add("-Command");
        processInfo.ArgumentList.Add(command);

        using var process = Process.Start(processInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            process.Kill(true);
            throw new TimeoutException("The PowerShell hook-command test timed out.");
        }

        Assert.True(process.ExitCode == 0, (await stdout + await stderr).Trim());
        var arguments = File.ReadAllText(result).Split('|');
        Assert.Equal(["--provider", "codex", "--data-dir", paths.Root], arguments);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void EditedHandlerRequiresExplicitPreviewedMigrationAndDoesNotDuplicate(string provider)
    {
        using var workspace = new TestWorkspace();
        var path = Path.Combine(workspace.Root, "settings.json");
        var initialOwnership = new IntegrationOwnership([], []);
        var legacy = new ProviderSettingsEditor(new HookCommandBuilder("legacy.exe", workspace.Paths));
        var settings = JsonNode.Parse(legacy.Edit(provider, path, null, new([], []), initialOwnership, false, false))!;
        var edited = settings["hooks"]!["Stop"]![0]!["hooks"]![0]!;
        edited["timeout"] = 3;
        edited["async"] = true;
        settings["keep"] = "unrelated";
        var notes = new List<string>();
        var fast = new ProviderSettingsEditor(new HookCommandBuilder("legacy.exe", workspace.Paths, "D:/fixture/helper.exe"));
        var next = new IntegrationOwnership(new(initialOwnership.Commands), []) { Handlers = new(initialOwnership.Handlers) };
        var before = settings.ToJsonString();
        var ordinary = fast.Edit(provider, path, before, initialOwnership, next, false, false, notes: notes);
        var retained = JsonNode.Parse(ordinary)!["hooks"]!["Stop"]!.AsArray();
        Assert.Single(retained);
        Assert.True(JsonNode.DeepEquals(edited, retained[0]!["hooks"]![0]));
        Assert.Contains(notes, note => note.Contains("--migrate-edited-hooks", StringComparison.Ordinal));
        var migratedOwnership = new IntegrationOwnership(new(next.Commands), []) { Handlers = new(next.Handlers) };
        var migrated = fast.Edit(provider, path, ordinary, next, migratedOwnership, false, false, migrateEdited: true, notes: notes);
        Assert.Contains("helper.exe", migrated);
        Assert.Contains("unrelated", migrated);
        var repeated = fast.Edit(provider, path, migrated, migratedOwnership,
            new(new(migratedOwnership.Commands), []) { Handlers = new(migratedOwnership.Handlers) }, false, false, migrateEdited: true);
        Assert.Single(JsonNode.Parse(repeated)!["hooks"]!["Stop"]!.AsArray());
        Assert.Equal(ordinary, fast.Edit(provider, path, before, initialOwnership,
            new(new(initialOwnership.Commands), []) { Handlers = new(initialOwnership.Handlers) }, false, false));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void FastHandlersAreAsyncExceptForSessionEnd(string provider)
    {
        using var workspace = new TestWorkspace();
        var ownership = new IntegrationOwnership([], []);
        var editor = new ProviderSettingsEditor(new HookCommandBuilder("unused", workspace.Paths, "D:/fixture/helper.exe"));
        var settings = JsonNode.Parse(editor.Edit(provider, "settings.json", null, new([], []), ownership, false, false))!;
        Assert.True(settings["hooks"]!["Stop"]![0]!["hooks"]![0]!["async"]!.GetValue<bool>());
        Assert.Null(settings["hooks"]!["SessionEnd"]![0]!["hooks"]![0]!["async"]);
        Assert.NotEmpty(ownership.Handlers["settings.json"]);
    }

    [Fact]
    public void EnabledCaptureProtocolRefusesIncompatibleConsumerButAllowsLegacyWriters()
    {
        using var workspace = new TestWorkspace();
        AtomicFile.Write(Path.Combine(workspace.Paths.Root, "capture-protocol.json"), "{\"protocol\":1}");
        Assert.Throws<InvalidDataException>(() => CaptureCompatibility.Ensure(workspace.Paths.Root, 0));
        CaptureCompatibility.Ensure(workspace.Paths.Root, 1);
        CaptureCompatibility.EnsureTrayInvocation(["event", "--data-dir", workspace.Paths.Root], 0);
        Assert.Throws<InvalidDataException>(() => CaptureCompatibility.EnsureTrayInvocation(["tray", "--data-dir", workspace.Paths.Root], 0));
        Assert.Throws<InvalidDataException>(() => CaptureCompatibility.EnsureTrayInvocation(["--data-dir", workspace.Paths.Root], 0));
        Assert.Throws<InvalidDataException>(() => CaptureCompatibility.EnsureTrayInvocation(["--silent", "--data-dir=" + workspace.Paths.Root], 0));
        CaptureCompatibility.EnsureTrayInvocation(["--data-dir", workspace.Paths.Root, "doctor"], 0);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void AppliedMigrationBacksUpEditedSettingsAndPreservesUnknownManifestFields(string provider)
    {
        using var workspace = new TestWorkspace();
        var home = Path.Combine(workspace.Root, "home");
        var setup = new IntegrationSetup(workspace.Paths, "D:/development/bell-tower.exe", home);
        setup.Plan(provider).Apply(workspace.Paths);
        var path = Path.Combine(home, "." + provider, provider == "claude" ? "settings.json" : "hooks.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!;
        node["hooks"]!["Stop"]![0]!["hooks"]![0]!["async"] = true;
        var edited = node.ToJsonString();
        File.WriteAllText(path, edited);
        var manifest = Path.Combine(workspace.Paths.Root, "integration.json");
        var ownership = JsonNode.Parse(File.ReadAllText(manifest))!;
        ownership["futureOwnership"] = 42;
        File.WriteAllText(manifest, ownership.ToJsonString());
        var preview = setup.Plan(provider, migrateEditedHooks: true);
        Assert.Equal(edited, File.ReadAllText(path));
        Assert.Contains(preview.Notes, note => note.Contains("Explicitly replace edited Stop", StringComparison.Ordinal));
        preview.Apply(workspace.Paths);
        Assert.Contains(Directory.GetFiles(Path.Combine(workspace.Paths.Root, "integration-backups"), "*.json", SearchOption.AllDirectories),
            backup => File.ReadAllText(backup) == edited);
        Assert.Equal(42, JsonNode.Parse(File.ReadAllText(manifest))!["futureOwnership"]!.GetValue<int>());
        setup.Plan(provider, migrateEditedHooks: true).Apply(workspace.Paths);
        Assert.Single(JsonNode.Parse(File.ReadAllText(path))!["hooks"]!["Stop"]!.AsArray());
    }
}
