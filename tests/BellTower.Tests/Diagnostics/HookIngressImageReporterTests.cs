using System.Text.Json;
using BellTower.Diagnostics;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Diagnostics;

public sealed class HookIngressImageReporterTests
{
    private static readonly string Selected = new('a', 64);
    private static readonly string Older = new('b', 64);

    private static string Ingress(TestWorkspace workspace, string hash) =>
        Path.Combine(workspace.Paths.Root, "integrations", "provider-ingress", "1", hash, "bell-tower-ingress.exe");

    private static string Command(TestWorkspace workspace, string provider, string hash, CodexHookShell shell = CodexHookShell.PowerShell) =>
        new HookCommandBuilder("bell-tower.exe", workspace.Paths, Ingress(workspace, hash), shell).Build("event", provider);

    private static string SettingsJson(params string[] commands) =>
        Json.Encode(new { hooks = new { Stop = new[] { new { hooks = commands.Select(command => new { type = "command", command }).ToArray() } } } });

    /// <summary>Records the manifest history and writes provider settings holding the same commands (the common case).</summary>
    private static void Record(TestWorkspace workspace, string claudeHome, string codexHome, string? claude, string? codex)
    {
        RecordHistory(workspace, claudeHome, codexHome, claude is null ? [] : [claude], codex is null ? [] : [codex]);
        WriteSettings(claudeHome, codexHome, claude is null ? null : SettingsJson(claude), codex is null ? null : SettingsJson(codex));
    }

    private static void RecordHistory(TestWorkspace workspace, string claudeHome, string codexHome, string[] claude, string[] codex)
    {
        var commands = new Dictionary<string, List<string>>();
        if (claude.Length > 0)
        {
            commands[Path.Combine(claudeHome, "settings.json")] = [.. claude];
        }
        if (codex.Length > 0)
        {
            commands[Path.Combine(codexHome, "hooks.json")] = [.. codex];
        }
        AtomicFile.Write(Path.Combine(workspace.Paths.Root, "integration.json"), Json.Encode(new IntegrationOwnership(commands, [])));
    }

    private static void WriteSettings(string claudeHome, string codexHome, string? claude, string? codex)
    {
        foreach (var (directory, name, content) in new[] { (claudeHome, "settings.json", claude), (codexHome, "hooks.json", codex) })
        {
            var path = Path.Combine(directory, name);
            if (content is null)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                continue;
            }
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, content);
        }
    }

    private static void RecordLauncher(TestWorkspace workspace)
    {
        var launcher = Path.Combine(workspace.Root, "install", "bell-tower.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(launcher)!);
        File.WriteAllText(launcher, "launcher");
        HookTrayStart.RecordLauncher(workspace.Paths.Root, launcher);
    }

    private static JsonElement Property(JsonElement element, string name) =>
        element.GetProperty(element.EnumerateObject().Single(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)).Name);

    private static (string Status, string? Advice) Image(JsonElement start, string provider)
    {
        var image = Property(Property(start, "ingressImage"), provider);
        var advice = Property(image, "advice");
        return (Property(image, "status").GetString()!, advice.ValueKind == JsonValueKind.Null ? null : advice.GetString());
    }

    private static JsonDocument Start(TestWorkspace workspace, string claudeHome, string codexHome, string? selected) =>
        JsonDocument.Parse(Json.Encode(new HookStartReporter(workspace.Paths, () => selected, claudeHome, codexHome).Report()));

    [Theory]
    [InlineData(CodexHookShell.PowerShell)]
    [InlineData(CodexHookShell.Cmd)]
    [InlineData(CodexHookShell.Bash)]
    public void EqualHashIsCurrentForBothProvidersInEveryShellForm(CodexHookShell shell)
    {
        using var workspace = new TestWorkspace();
        var claudeHome = Path.Combine(workspace.Root, "claude-home");
        var codexHome = Path.Combine(workspace.Root, "codex-home");
        Record(workspace, claudeHome, codexHome, Command(workspace, "claude", Selected), Command(workspace, "codex", Selected, shell));
        RecordLauncher(workspace);

        using var document = Start(workspace, claudeHome, codexHome, Selected);
        Assert.Equal((HookIngressImageReporter.Current, null), Image(document.RootElement, "claude"));
        Assert.Equal((HookIngressImageReporter.Current, null), Image(document.RootElement, "codex"));
        Assert.Equal("available", Property(document.RootElement, "availability").GetString());
    }

    [Theory]
    [InlineData(CodexHookShell.PowerShell)]
    [InlineData(CodexHookShell.Cmd)]
    [InlineData(CodexHookShell.Bash)]
    public void DifferentHashIsStaleWithAdviceForBothProvidersAndAvailabilityNeedsRefresh(CodexHookShell shell)
    {
        using var workspace = new TestWorkspace();
        var claudeHome = Path.Combine(workspace.Root, "claude-home");
        var codexHome = Path.Combine(workspace.Root, "codex-home");
        Record(workspace, claudeHome, codexHome, Command(workspace, "claude", Older), Command(workspace, "codex", Older, shell));
        RecordLauncher(workspace);

        using var document = Start(workspace, claudeHome, codexHome, Selected);
        foreach (var provider in new[] { "claude", "codex" })
        {
            var (status, advice) = Image(document.RootElement, provider);
            Assert.Equal(HookIngressImageReporter.Stale, status);
            Assert.Equal(HookIngressImageReporter.StaleAdvice, advice);
        }
        Assert.Contains("rerun setup --host-bridge --apply", Property(document.RootElement, "availability").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void OneStaleProviderIsStaleOnlyForThatProvider()
    {
        using var workspace = new TestWorkspace();
        var claudeHome = Path.Combine(workspace.Root, "claude-home");
        var codexHome = Path.Combine(workspace.Root, "codex-home");
        Record(workspace, claudeHome, codexHome, Command(workspace, "claude", Selected), Command(workspace, "codex", Older));

        using var document = Start(workspace, claudeHome, codexHome, Selected);
        Assert.Equal(HookIngressImageReporter.Current, Image(document.RootElement, "claude").Status);
        Assert.Equal(HookIngressImageReporter.Stale, Image(document.RootElement, "codex").Status);
    }

    [Fact]
    public void UnparseableMissingOrUnselectedIsUnknownNeverStale()
    {
        using var workspace = new TestWorkspace();
        var claudeHome = Path.Combine(workspace.Root, "claude-home");
        var codexHome = Path.Combine(workspace.Root, "codex-home");
        RecordLauncher(workspace);

        // No manifest at all.
        using (var none = Start(workspace, claudeHome, codexHome, Selected))
        {
            Assert.Equal((HookIngressImageReporter.Unknown, null), Image(none.RootElement, "claude"));
            Assert.Equal((HookIngressImageReporter.Unknown, null), Image(none.RootElement, "codex"));
            Assert.Equal("available", Property(none.RootElement, "availability").GetString());
        }

        // A legacy encoded PowerShell command and a path without a hash segment cannot be compared.
        var legacy = new HookCommandBuilder("bell-tower.exe", workspace.Paths).Build("event", "claude");
        Record(workspace, claudeHome, codexHome, legacy, "\"D:/Other/bell-tower-ingress.exe\" --provider codex");
        using (var unparseable = Start(workspace, claudeHome, codexHome, Selected))
        {
            Assert.Equal((HookIngressImageReporter.Unknown, null), Image(unparseable.RootElement, "claude"));
            Assert.Equal((HookIngressImageReporter.Unknown, null), Image(unparseable.RootElement, "codex"));
        }

        // No usable selected image hash (a development CLI) leaves a parseable command unknown too.
        Record(workspace, claudeHome, codexHome, Command(workspace, "claude", Older), Command(workspace, "codex", Older));
        foreach (var selected in new string?[] { null, "not-a-hash" })
        {
            using var unselected = Start(workspace, claudeHome, codexHome, selected);
            Assert.Equal((HookIngressImageReporter.Unknown, null), Image(unselected.RootElement, "claude"));
            Assert.Equal((HookIngressImageReporter.Unknown, null), Image(unselected.RootElement, "codex"));
            Assert.Equal("available", Property(unselected.RootElement, "availability").GetString());
        }
    }

    [Theory]
    [InlineData(CodexHookShell.PowerShell)]
    [InlineData(CodexHookShell.Cmd)]
    [InlineData(CodexHookShell.Bash)]
    public void OnlyTheLiveSettingsCommandsDecideNotTheAppendOnlyManifestHistory(CodexHookShell shell)
    {
        using var workspace = new TestWorkspace();
        var claudeHome = Path.Combine(workspace.Root, "claude-home");
        var codexHome = Path.Combine(workspace.Root, "codex-home");
        RecordLauncher(workspace);
        RecordHistory(workspace, claudeHome, codexHome,
            [Command(workspace, "claude", Older), Command(workspace, "claude", Selected)],
            [Command(workspace, "codex", Older, shell), Command(workspace, "codex", Selected, shell)]);

        // After the setup refresh the settings hold only the selected image: current, availability available.
        WriteSettings(claudeHome, codexHome, SettingsJson(Command(workspace, "claude", Selected)), SettingsJson(Command(workspace, "codex", Selected, shell)));
        using (var refreshed = Start(workspace, claudeHome, codexHome, Selected))
        {
            Assert.Equal((HookIngressImageReporter.Current, null), Image(refreshed.RootElement, "claude"));
            Assert.Equal((HookIngressImageReporter.Current, null), Image(refreshed.RootElement, "codex"));
            Assert.Equal("available", Property(refreshed.RootElement, "availability").GetString());
        }

        // The settings still call the older image: stale for both providers.
        WriteSettings(claudeHome, codexHome, SettingsJson(Command(workspace, "claude", Older)), SettingsJson(Command(workspace, "codex", Older, shell)));
        using var outdated = Start(workspace, claudeHome, codexHome, Selected);
        Assert.Equal(HookIngressImageReporter.Stale, Image(outdated.RootElement, "claude").Status);
        Assert.Equal(HookIngressImageReporter.Stale, Image(outdated.RootElement, "codex").Status);
        Assert.Contains("rerun setup --host-bridge --apply", Property(outdated.RootElement, "availability").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NoOwnedCommandInSettingsOrMissingOrUnusableSettingsIsUnknown()
    {
        using var workspace = new TestWorkspace();
        var claudeHome = Path.Combine(workspace.Root, "claude-home");
        var codexHome = Path.Combine(workspace.Root, "codex-home");
        RecordLauncher(workspace);
        RecordHistory(workspace, claudeHome, codexHome, [Command(workspace, "claude", Older)], [Command(workspace, "codex", Older)]);

        void AssertUnknown(string? claude, string? codex)
        {
            WriteSettings(claudeHome, codexHome, claude, codex);
            using var document = Start(workspace, claudeHome, codexHome, Selected);
            Assert.Equal((HookIngressImageReporter.Unknown, null), Image(document.RootElement, "claude"));
            Assert.Equal((HookIngressImageReporter.Unknown, null), Image(document.RootElement, "codex"));
            Assert.Equal("available", Property(document.RootElement, "availability").GetString());
        }

        AssertUnknown(null, null);
        AssertUnknown(SettingsJson("someone-elses-hook"), SettingsJson("someone-elses-hook"));
        AssertUnknown("{ not json", "[1, 2");
        var padding = new string(' ', 1024 * 1024 + 16);
        AssertUnknown(SettingsJson(Command(workspace, "claude", Older)) + padding, SettingsJson(Command(workspace, "codex", Older)) + padding);
    }

    [Fact]
    public void AvailabilityStaysUnavailableWithoutLauncherAndHashComparisonIgnoresCase()
    {
        Assert.Equal(HookIngressImageReporter.Current, HookIngressImageReporter.Evaluate(["'D:/x/provider-ingress/1/" + Selected.ToUpperInvariant() + "/bell-tower-ingress.exe'"], Selected).Status);
        using var workspace = new TestWorkspace();
        using var document = Start(workspace, Path.Combine(workspace.Root, "c"), Path.Combine(workspace.Root, "x"), Selected);
        Assert.StartsWith("unavailable", Property(document.RootElement, "availability").GetString(), StringComparison.Ordinal);
    }
}
