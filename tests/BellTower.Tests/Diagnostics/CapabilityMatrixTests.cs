using System.Text.Json;
using BellTower.Diagnostics;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Diagnostics;

// The capability matrix is the honest parity record shown by doctor and docs/compatibility.md: every surface states both
// providers, and a restriction is only allowed with the proof, fallback and regression evidence AGENTS.md requires.
public sealed class CapabilityMatrixTests
{
    private static readonly string[] Statuses = [CapabilityMatrix.Supported, CapabilityMatrix.Restricted, CapabilityMatrix.Unverified, CapabilityMatrix.NotApplicable];

    [Fact]
    public void EverySurfaceStatesBothProvidersWithAKnownStatus()
    {
        Assert.NotEmpty(CapabilityMatrix.Rows);
        Assert.Equal(CapabilityMatrix.Rows.Count, CapabilityMatrix.Rows.Select(row => row.Surface).Distinct().Count());
        foreach (var row in CapabilityMatrix.Rows)
        {
            foreach (var provider in new[] { row.Claude, row.Codex })
            {
                Assert.Contains(provider.Status, Statuses);
                Assert.False(string.IsNullOrWhiteSpace(provider.Detail), row.Surface);
            }
        }
    }

    [Fact]
    public void ARestrictionNeverAppearsWithoutProofFallbackAndRegressionCoverage()
    {
        foreach (var row in CapabilityMatrix.Rows)
        {
            var restricted = row.Claude.Status == CapabilityMatrix.Restricted || row.Codex.Status == CapabilityMatrix.Restricted;
            if (restricted)
            {
                Assert.False(string.IsNullOrWhiteSpace(row.Proof), row.Surface);
                Assert.False(string.IsNullOrWhiteSpace(row.Fallback), row.Surface);
                Assert.False(string.IsNullOrWhiteSpace(row.Regression), row.Surface);
            }
            else
            {
                Assert.Null(row.Proof);
            }
        }
        // Unknown and unimplemented are never reported as restrictions: the unverified rows say what is missing.
        Assert.All(CapabilityMatrix.Rows.SelectMany(row => new[] { row.Claude, row.Codex }).Where(item => item.Status == CapabilityMatrix.Unverified),
            item => Assert.DoesNotContain("restriction", System.Text.RegularExpressions.Regex.Replace(item.Detail, "not (?:a |by a )?(?:provider|proven) restriction", ""), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DoctorReportsTheMatrixTheDeferredClaudeNativeWriteAndTheIdentityDocument()
    {
        using var workspace = new TestWorkspace();
        using var report = JsonDocument.Parse(Json.Encode(new DiagnosticsReporter(workspace.Paths).Report()));
        var root = report.RootElement;
        var matrix = root.GetProperty(Name(root, "capabilityMatrix"));
        Assert.Equal(CapabilityMatrix.Rows.Count, matrix.GetProperty(Name(matrix, "rows")).GetArrayLength());
        Assert.Contains("Claude Code", matrix.GetProperty(Name(matrix, "claude")).GetString());
        Assert.Contains("Codex", matrix.GetProperty(Name(matrix, "codex")).GetString());
        var claude = root.GetProperty(Name(root, "claude"));
        var naming = claude.GetProperty(Name(claude, "nameSynchronization")).GetString()!;
        Assert.Contains("sync-native-session-titles", naming);
        Assert.Contains("not a provider restriction", naming);
        Assert.DoesNotContain(claude.EnumerateObject(), property => property.Name.Contains("helper", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("docs/session-identity.md", root.GetProperty(Name(root, "identityDocumentation")).GetString());
    }

    // The report's property naming policy is a serializer detail; match it case-insensitively.
    private static string Name(JsonElement element, string wanted) =>
        element.EnumerateObject().Single(property => string.Equals(property.Name, wanted, StringComparison.OrdinalIgnoreCase)).Name;

    [Fact]
    public void TheClaudeNativeTitleWriteIsUnfinishedNotRestrictedAndTheCodexSkillRowIsUnverified()
    {
        var write = CapabilityMatrix.Rows.Single(row => row.Surface.StartsWith("Native chat-name write", StringComparison.Ordinal));
        Assert.Equal(CapabilityMatrix.Unverified, write.Claude.Status);
        Assert.Contains("sync-native-session-titles", write.Claude.Detail);
        Assert.Contains("not a provider restriction", write.Claude.Detail);
        Assert.Equal(CapabilityMatrix.Supported, write.Codex.Status);

        var skill = CapabilityMatrix.Rows.Single(row => row.Surface.Contains("workflow-skill", StringComparison.Ordinal));
        Assert.Equal(CapabilityMatrix.Supported, skill.Claude.Status);
        Assert.Equal(CapabilityMatrix.Unverified, skill.Codex.Status);
        Assert.Contains("UserPromptSubmit", skill.Codex.Detail);
        Assert.Null(skill.Proof);
        Assert.DoesNotContain(CapabilityMatrix.Rows, row => row.Claude.Status == CapabilityMatrix.Restricted || row.Codex.Status == CapabilityMatrix.Restricted);
    }

    [Fact]
    public void TheHumanWaitRowNamesTheClaudeDisablingSettingAndTheDocumentedNotificationDelay()
    {
        var row = CapabilityMatrix.Rows.Single(item => item.Surface.StartsWith("Human-wait confirmation (permission", StringComparison.Ordinal));
        Assert.Contains("CLAUDE_CODE_DISABLE_PERMISSION_PROMPT_NOTIFY_HOOKS", row.Claude.Detail);
        Assert.Contains("documents about 6 s", row.Claude.Detail);
    }

    [Fact]
    public void HookEvidenceScopesCodexCmdFallbackAndLeavesExternalBashUnverified()
    {
        var row = CapabilityMatrix.Rows.Single(item => item.Surface == "Hook capture and lifecycle events");
        Assert.Equal(CapabilityMatrix.Supported, row.Codex.Status);
        Assert.Contains("both PowerShell directories denied in process-local MXC", row.Codex.Detail);
        Assert.Contains("HookShell matched its cmd.exe ancestor", row.Codex.Detail);
        Assert.Contains("no Bash selection branch", row.Codex.Detail);
        Assert.Contains("External-host Bash and other versions/path-priority combinations remain unverified", row.Codex.Detail);
    }

    private static readonly string[] ModeSurfaces =
    [
        "Native name and runtime status: Codex owned runtime",
        "Native name and runtime status: Codex shared backend",
        "Native name and runtime status: direct clients",
        "Native name and runtime status: shared conversations not yet reached",
        "Native name and runtime status: refused shared threads and backends",
        "Observer read content handling"
    ];

    private static CapabilityRow Row(string surface) => CapabilityMatrix.Rows.Single(row => row.Surface == surface);

    [Fact]
    public void AdoptedDirectSessionsAndPlainDefaultCodexAreStatedHonestlyPerProvider()
    {
        var navigation = Row("Terminal navigation: adopted direct sessions (plain claude, codex --no-daemon)");
        var presentation = Row("Title and color: adopted direct sessions (plain claude, codex --no-daemon)");
        foreach (var provider in new[] { navigation.Claude, navigation.Codex, presentation.Claude, presentation.Codex })
        {
            Assert.Equal(CapabilityMatrix.Supported, provider.Status);
        }
        Assert.Contains("window only", navigation.Claude.Detail);
        Assert.Contains("window only", navigation.Codex.Detail);
        Assert.Contains("Title and color native sessions", navigation.Claude.Detail);
        Assert.Contains("OSC 110/111", presentation.Claude.Detail);
        Assert.Contains("assumed until the 6.1 trial", presentation.Claude.Detail);
        Assert.Contains("assumed", presentation.Codex.Detail);
        Assert.DoesNotContain("pre-launch coloring for native", presentation.Claude.Detail.Replace("no pre-launch coloring for native sessions", ""));

        var tracking = Row("Plain default codex (shared backend): tracking, alerts, name sync and permission status");
        Assert.Equal(CapabilityMatrix.NotApplicable, tracking.Claude.Status);
        Assert.Equal(CapabilityMatrix.Supported, tracking.Codex.Status);
        var unfinished = Row("Plain default codex (shared backend): navigation, title and color");
        Assert.Equal(CapabilityMatrix.NotApplicable, unfinished.Claude.Status);
        Assert.Equal(CapabilityMatrix.Unverified, unfinished.Codex.Status);
        Assert.Contains("unfinished: user scope decision 2026-11-13, not a provider restriction", unfinished.Codex.Detail);
        Assert.Contains("Codex profile", unfinished.Codex.Detail);
        Assert.Contains("codex --no-daemon", unfinished.Codex.Detail);
        Assert.Null(unfinished.Proof);
    }

    [Theory]
    [InlineData(false, "off", "window only (Windows Terminal)")]
    [InlineData(true, "on", "tab (Windows Terminal)")]
    public void DoctorReportsTheNativePresentationStateAndBothPrecisionsPerProvider(bool enabled, string state, string now)
    {
        using var workspace = new TestWorkspace();
        new ConfigStore(workspace.Paths).Update(config => config.NativePresentation = enabled);
        using var report = JsonDocument.Parse(Json.Encode(new DiagnosticsReporter(workspace.Paths).Report()));
        var native = report.RootElement.GetProperty(Name(report.RootElement, "nativeSessions"));
        Assert.Equal(state, native.GetProperty(Name(native, "setting")).GetString());
        foreach (var provider in new[] { "claudeDirect", "codexNoDaemon" })
        {
            var item = native.GetProperty(Name(native, provider));
            Assert.Equal(now, item.GetProperty(Name(item, "navigationNow")).GetString());
            Assert.Contains("assumed until the 6.1 trial", item.GetProperty(Name(item, "titleAndColor")).GetString());
        }
        var plain = native.GetProperty(Name(native, "codexPlainDefault"));
        Assert.Contains("not a provider restriction", plain.GetProperty(Name(plain, "unfinished")).GetString());
        Assert.Contains("codex --no-daemon", plain.GetProperty(Name(plain, "routes")).GetString());
        Assert.Contains("not applicable", native.GetProperty(Name(native, "claudeModeCrossCheck")).GetString());
        var codex = report.RootElement.GetProperty(Name(report.RootElement, "codex"));
        var approval = codex.GetProperty(Name(codex, "approvalResolution")).GetString()!;
        Assert.Contains("shared-backend waitingOnApproval", approval);
        Assert.Contains("alongside the owning runtime", approval);
    }

    // The per-mode states are the single account of native-name and runtime-status access. Nothing in them may read as a provider
    // restriction or as complete without backing: gaps are unverified (owned by a named task or change), absence of a mode is not
    // applicable, and only the owned and shared observer paths with automated coverage are supported.
    [Fact]
    public void PerModeNativeNameAndStatusStatesAreHonestForBothProviders()
    {
        Assert.Equal(CapabilityMatrix.Supported, Row(ModeSurfaces[0]).Codex.Status);
        Assert.Equal(CapabilityMatrix.Supported, Row(ModeSurfaces[1]).Codex.Status);
        Assert.Equal(CapabilityMatrix.Unverified, Row(ModeSurfaces[2]).Codex.Status);
        Assert.Equal(CapabilityMatrix.Unverified, Row(ModeSurfaces[3]).Codex.Status);
        Assert.Equal(CapabilityMatrix.Unverified, Row(ModeSurfaces[4]).Codex.Status);
        Assert.Contains("user scope decision 2026-11-13", Row(ModeSurfaces[3]).Codex.Detail);
        Assert.Contains("task 6.1", Row(ModeSurfaces[4]).Codex.Detail);
        Assert.Contains("elevated", Row(ModeSurfaces[4]).Codex.Detail);
        // Claude is treated as direct: its native-title write stays unfinished and owned by the separate change, never restricted or complete.
        var direct = Row(ModeSurfaces[2]).Claude;
        Assert.Equal(CapabilityMatrix.Unverified, direct.Status);
        Assert.Contains("sync-native-session-titles", direct.Detail);
        Assert.Contains("not a provider restriction", direct.Detail);
        // A mode the provider does not have is stated as such, never as supported or unverified.
        foreach (var surface in new[] { ModeSurfaces[0], ModeSurfaces[1], ModeSurfaces[3], ModeSurfaces[4], ModeSurfaces[5] })
        {
            Assert.Equal(CapabilityMatrix.NotApplicable, Row(surface).Claude.Status);
        }
        // Doctor reports the same per-mode text for Codex name synchronization from the matrix source.
        Assert.Contains(CodexAccessStatus.NoRuntime, CapabilityMatrix.CodexNameModes);
        Assert.Contains(CodexAccessStatus.Unverified, CapabilityMatrix.CodexNameModes);
        Assert.Contains(CodexAccessStatus.NoRuntime, Row(ModeSurfaces[2]).Codex.Detail);
        Assert.Contains(CodexAccessStatus.Unverified, Row(ModeSurfaces[4]).Codex.Detail);
        Assert.Contains("user scope decision 2026-11-13", CapabilityMatrix.CodexNameModes);
        Assert.Contains("task 6.1", CapabilityMatrix.CodexNameModes);
        Assert.Contains("read through the tray-home backend, which does not host it", CapabilityMatrix.CodexNameModes);
        Assert.DoesNotContain("is not read through", CapabilityMatrix.CodexNameModes);
    }

    [Fact]
    public void DoctorReportsTheSamePerModeCodexNameText()
    {
        using var workspace = new TestWorkspace();
        using var report = JsonDocument.Parse(Json.Encode(new DiagnosticsReporter(workspace.Paths).Report()));
        var codex = report.RootElement.GetProperty(Name(report.RootElement, "codex"));
        Assert.Equal(CapabilityMatrix.CodexNameModes, codex.GetProperty(Name(codex, "nameSynchronization")).GetString());
    }

    // No refusal label claims a restriction or a completed capability: each states an unavailable access with its own reason.
    [Fact]
    public void EveryAccessRefusalLabelNamesAnUnavailableReasonAndNeverClaimsRestrictionOrCompletion()
    {
        var labels = new List<string> { CodexAccessStatus.NoRuntime, CodexAccessStatus.ConnectionFailed, CodexAccessStatus.Mismatch, CodexAccessStatus.Unverified };
        var shared = new List<string>();
        foreach (var failure in Enum.GetValues<CodexTransportFailure>())
        {
            var label = CodexAccessStatus.For(new CodexAccess.Shared("home"), new CodexTransportException(failure, "message"));
            if (failure == CodexTransportFailure.BackedOff)
            {
                Assert.Null(label);
                continue;
            }
            if (failure is not (CodexTransportFailure.ThreadUnmapped or CodexTransportFailure.ThreadUnverified))
            {
                shared.Add(label!);
            }
            labels.Add(label!);
        }
        Assert.Equal(shared.Count, shared.Distinct().Count());
        foreach (var label in labels)
        {
            Assert.StartsWith("unavailable: ", label, StringComparison.Ordinal);
            Assert.DoesNotContain("restrict", label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotMatch(@"\bsupported\b", label);
            Assert.DoesNotContain("complete", label, StringComparison.OrdinalIgnoreCase);
        }
        var elevated = CodexAccessStatus.For(new CodexAccess.Shared("home"), new CodexTransportException(CodexTransportFailure.ElevatedPeer, "m"));
        Assert.Equal("unavailable: shared backend runs elevated", elevated);
    }

    // A supported mode row names its automated backing, and each named test class and method exists.
    [Fact]
    public void SupportedModeRowsNameBackingTestsThatExist()
    {
        var assembly = typeof(CapabilityMatrixTests).Assembly;
        foreach (var surface in new[] { ModeSurfaces[0], ModeSurfaces[1], ModeSurfaces[5] })
        {
            var detail = Row(surface).Codex.Detail;
            var references = System.Text.RegularExpressions.Regex.Matches(detail, @"\b(\w+Tests)(?:\.(\w+))?\b");
            Assert.NotEmpty(references);
            foreach (System.Text.RegularExpressions.Match reference in references)
            {
                var type = assembly.GetTypes().SingleOrDefault(candidate => candidate.Name == reference.Groups[1].Value);
                Assert.NotNull(type);
                if (reference.Groups[2].Success)
                {
                    Assert.NotNull(type.GetMethod(reference.Groups[2].Value));
                }
            }
        }
    }

    // docs/compatibility.md prints the same states as the source (and so as doctor): the first status word of each provider cell.
    [Fact]
    public void CompatibilityDocumentStatesMatchTheMatrixSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ZeusApps.BellTower.slnx")))
        {
            directory = directory.Parent;
        }
        var lines = File.ReadAllLines(Path.Combine(directory!.FullName, "docs", "compatibility.md"));
        Assert.DoesNotContain(lines, line => line.Contains("is not read through", StringComparison.Ordinal) || line.Contains("verified interactive and", StringComparison.Ordinal));
        Assert.DoesNotContain(CapabilityMatrix.Rows, row => row.Claude.Detail.Contains("verified interactive and", StringComparison.Ordinal));
        Assert.DoesNotContain(CapabilityMatrix.Rows, row => row.Claude.Detail.Contains("always direct", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("always direct", StringComparison.Ordinal));
        var table = lines.Where(line => line.StartsWith("| ", StringComparison.Ordinal)).Select(line => line.Split('|').Select(cell => cell.Trim()).ToArray())
            .Where(cells => cells.Length >= 4).GroupBy(cells => cells[1], StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        string[] words = [CapabilityMatrix.Supported, CapabilityMatrix.Restricted, CapabilityMatrix.Unverified, CapabilityMatrix.NotApplicable];
        static string First(string cell, string[] words)
        {
            var plain = cell.Replace("*", "");
            return words.Where(word => plain.Contains(word, StringComparison.OrdinalIgnoreCase)).OrderBy(word => plain.IndexOf(word, StringComparison.OrdinalIgnoreCase)).First();
        }
        foreach (var row in CapabilityMatrix.Rows)
        {
            Assert.True(table.TryGetValue(row.Surface, out var cells), "docs/compatibility.md lacks the row: " + row.Surface);
            Assert.Equal(row.Claude.Status, First(cells![2], words));
            Assert.Equal(row.Codex.Status, First(cells[3], words));
        }
    }
}
