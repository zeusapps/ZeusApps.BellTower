using System.Text.Json;
using BellTower.Diagnostics;
using BellTower.IO;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Diagnostics;

public sealed class HookDeliveryReporterTests
{
    private static JsonElement Read(TestWorkspace workspace, string claudeHome, string codexHome, out JsonDocument document)
    {
        document = JsonDocument.Parse(Json.Encode(new HookDeliveryReporter(workspace.Paths, claudeHome, codexHome).Report()));
        return document.RootElement;
    }

    private static JsonElement Property(JsonElement element, string name) =>
        element.GetProperty(element.EnumerateObject().Single(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)).Name);

    [Fact]
    public void DoctorReportsMatchedProviderConfigurationAndDeliveryWithoutHookContents()
    {
        using var workspace = new TestWorkspace();
        var claudeHome = Path.Combine(workspace.Root, "claude-home");
        var codexHome = Path.Combine(workspace.Root, "codex-home");
        Directory.CreateDirectory(claudeHome);
        Directory.CreateDirectory(codexHome);
        File.WriteAllText(Path.Combine(claudeHome, "settings.json"), """
            { "hooks": { "SessionStart": [{ "hooks": [{ "type": "command", "command": "& 'private/bell-tower-ingress.exe' --provider claude" }] }] } }
            """);
        File.WriteAllText(Path.Combine(codexHome, "hooks.json"), """
            { "hooks": { "SessionStart": [{ "hooks": [{ "type": "command", "command": "& 'private/bell-tower-ingress.exe' --provider codex" }, { "type": "command", "command": "other" }] }] } }
            """);
        var claudeCommand = "& 'private/bell-tower-ingress.exe' --provider claude";
        var codexCommand = "& 'private/bell-tower-ingress.exe' --provider codex";
        AtomicFile.Write(Path.Combine(workspace.Paths.Root, "integration.json"), Json.Encode(new IntegrationOwnership(
            new Dictionary<string, List<string>>
            {
                [Path.Combine(claudeHome, "settings.json")] = [claudeCommand],
                [Path.Combine(codexHome, "hooks.json")] = [codexCommand]
            }, [])
        { SelectedCodexHookShell = CodexHookShell.PowerShell.ToString() }));

        var before = Read(workspace, claudeHome, codexHome, out var beforeDocument);
        using (beforeDocument)
        {
            var claude = Property(before, "claude");
            var codex = Property(before, "codex");
            Assert.True(Property(claude, "configured").GetBoolean());
            Assert.True(Property(codex, "configured").GetBoolean());
            Assert.Equal(1, Property(claude, "configuredHookCount").GetInt32());
            Assert.Equal(1, Property(codex, "configuredHookCount").GetInt32());
            Assert.Equal("unverified", Property(codex, "status").GetString());
            Assert.False(Property(codex, "delivered").GetBoolean());
            Assert.Equal(0, Property(codex, "knownBrokenOwnedCommandCount").GetInt32());
            Assert.Equal(JsonValueKind.Null, Property(codex, "migrationAction").ValueKind);
            var shell = Property(codex, "selectedShell");
            Assert.Equal("PowerShell", Property(shell, "expected").GetString());
            Assert.StartsWith("unverified;", Property(shell, "status").GetString(), StringComparison.Ordinal);
        }

        var deliveredAt = DateTimeOffset.UtcNow;
        AtomicFile.Write(Path.Combine(workspace.Paths.Root, "codex-observed.json"), Json.Encode(new { At = deliveredAt, Hook = "Register" }));
        var after = Read(workspace, claudeHome, codexHome, out var afterDocument);
        using (afterDocument)
        {
            var codex = Property(after, "codex");
            Assert.Equal("delivered", Property(codex, "status").GetString());
            Assert.True(Property(codex, "delivered").GetBoolean());
            Assert.Equal(deliveredAt, Property(codex, "lastDeliveredAt").GetDateTimeOffset());
        }

        Assert.DoesNotContain("private/bell-tower-ingress", BellTower.IO.Json.Encode(new HookDeliveryReporter(workspace.Paths, claudeHome, codexHome).Report()));
    }

    [Fact]
    public void DoctorIdentifiesOnlyOwnedLegacyCodexCommandsAndShowsPreviewedMigration()
    {
        using var workspace = new TestWorkspace();
        var codexHome = Path.Combine(workspace.Root, "codex-home");
        Directory.CreateDirectory(codexHome);
        var settings = Path.Combine(codexHome, "hooks.json");
        var old = "\"D:/Bell Tower/bell-tower-ingress.exe\" --provider codex --data-dir \"D:/Bell Tower/data\"";
        var lookalike = "\"D:/Other/bell-tower-ingress.exe\" --provider codex --data-dir \"D:/Bell Tower/data\"";
        File.WriteAllText(settings, Json.Encode(new
        {
            hooks = new
            {
                SessionStart = new[] { new { hooks = new[] { new { command = old }, new { command = lookalike } } } }
            }
        }));
        AtomicFile.Write(Path.Combine(workspace.Paths.Root, "integration.json"), Json.Encode(new IntegrationOwnership(
            new Dictionary<string, List<string>> { [settings] = [old] }, [])
        { SelectedCodexHookShell = CodexHookShell.PowerShell.ToString() }));

        var root = Read(workspace, Path.Combine(workspace.Root, "claude-home"), codexHome, out var document);
        using (document)
        {
            var codex = Property(root, "codex");
            Assert.Equal(1, Property(codex, "configuredHookCount").GetInt32());
            Assert.Equal(1, Property(codex, "knownBrokenOwnedCommandCount").GetInt32());
            var action = Property(codex, "migrationAction").GetString();
            Assert.Contains("setup --clients codex --migrate-edited-hooks", action);
            Assert.Contains("review the changes", action);
            Assert.Contains("--apply", action);
            Assert.DoesNotContain(old, document.RootElement.GetRawText());
        }
    }

    [Fact]
    public void DoctorDoesNotMarkOwnedCmdCommandBrokenWhenCmdWasSelected()
    {
        using var workspace = new TestWorkspace();
        var codexHome = Path.Combine(workspace.Root, "codex-home");
        Directory.CreateDirectory(codexHome);
        var settings = Path.Combine(codexHome, "hooks.json");
        var command = "\"D:/Bell Tower/bell-tower-ingress.exe\" --provider codex --data-dir \"D:/Bell Tower/data\"";
        File.WriteAllText(settings, Json.Encode(new { hooks = new { SessionStart = new[] { new { hooks = new[] { new { command } } } } } }));
        AtomicFile.Write(Path.Combine(workspace.Paths.Root, "integration.json"), Json.Encode(new IntegrationOwnership(
            new Dictionary<string, List<string>> { [settings] = [command] }, [])
        { SelectedCodexHookShell = CodexHookShell.Cmd.ToString() }));

        var root = Read(workspace, Path.Combine(workspace.Root, "claude-home"), codexHome, out var document);
        using (document)
        {
            var codex = Property(root, "codex");
            Assert.Equal(0, Property(codex, "knownBrokenOwnedCommandCount").GetInt32());
            Assert.Equal("Cmd", Property(Property(codex, "selectedShell"), "expected").GetString());
        }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void InvalidObservedMarkerShapesStayUnverifiedAndAreNeverChangedOrReported(string provider)
    {
        using var workspace = new TestWorkspace();
        var claudeHome = Path.Combine(workspace.Root, "claude-home");
        var codexHome = Path.Combine(workspace.Root, "codex-home");
        Directory.CreateDirectory(claudeHome);
        Directory.CreateDirectory(codexHome);
        var settings = Path.Combine(provider == "claude" ? claudeHome : codexHome, provider == "claude" ? "settings.json" : "hooks.json");
        var command = "private-ingress --provider " + provider;
        File.WriteAllText(settings, Json.Encode(new
        {
            hooks = new
            {
                SessionStart = new[] { new { hooks = new[] { new { type = "command", command } } } }
            }
        }));
        AtomicFile.Write(Path.Combine(workspace.Paths.Root, "integration.json"), Json.Encode(new IntegrationOwnership(
            new Dictionary<string, List<string>> { [settings] = [command] }, [])));

        var markerPath = Path.Combine(workspace.Paths.Root, provider + "-observed.json");
        var invalidBodies = new[] { "[]", "{\"at\":1,\"private\":\"marker-secret\"}", "{}", "{\"at\":\"not-a-timestamp\"}" };
        foreach (var body in invalidBodies)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            File.WriteAllBytes(markerPath, bytes);
            var report = Read(workspace, claudeHome, codexHome, out var document);
            using (document)
            {
                var result = Property(report, provider);
                Assert.Equal("unverified", Property(result, "status").GetString());
                Assert.False(Property(result, "delivered").GetBoolean());
                Assert.Equal(JsonValueKind.Null, Property(result, "lastDeliveredAt").ValueKind);
                Assert.DoesNotContain("marker-secret", document.RootElement.GetRawText());
                Assert.DoesNotContain(command, document.RootElement.GetRawText());
            }
            Assert.Equal(bytes, File.ReadAllBytes(markerPath));
        }

        foreach (var body in new[]
        {
            "{\"at\":\"2026-10-27T10:20:30Z\"}",
            "{\"At\":\"2026-10-27T10:20:30Z\"}"
        })
        {
            File.WriteAllText(markerPath, body);
            var report = Read(workspace, claudeHome, codexHome, out var document);
            using (document)
            {
                var result = Property(report, provider);
                Assert.Equal("delivered", Property(result, "status").GetString());
                Assert.True(Property(result, "delivered").GetBoolean());
                Assert.Equal(DateTimeOffset.Parse("2026-10-27T10:20:30Z"), Property(result, "lastDeliveredAt").GetDateTimeOffset());
            }
        }
    }
}
