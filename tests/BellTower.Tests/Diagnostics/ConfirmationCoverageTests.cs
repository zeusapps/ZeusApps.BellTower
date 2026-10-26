using System.Text.Json;
using BellTower.Core;
using BellTower.Diagnostics;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Diagnostics;

// Doctor reports human-wait confirmation coverage per provider honestly: configured, observed, unavailable. Fixtures only.
public sealed class ConfirmationCoverageTests
{
    private static JsonElement Coverage(TestWorkspace workspace, out JsonDocument document)
    {
        document = JsonDocument.Parse(Json.Encode(new DiagnosticsReporter(workspace.Paths).Report()));
        var root = document.RootElement;
        return root.GetProperty(Name(root, "confirmationCoverage"));
    }

    private static string Name(JsonElement element, string wanted) =>
        element.EnumerateObject().Single(property => string.Equals(property.Name, wanted, StringComparison.OrdinalIgnoreCase)).Name;

    private static JsonElement Get(JsonElement element, string name) => element.GetProperty(Name(element, name));

    [Fact]
    public void AFreshDataDirectoryReportsConfiguredNotObservedForBothProviders()
    {
        using var workspace = new TestWorkspace();
        var coverage = Coverage(workspace, out var document);
        using (document)
        {
            Assert.Contains("never alerts", Get(coverage, "policy").GetString());
            var claude = Get(coverage, "claude");
            Assert.Contains("configured, not yet observed", Get(claude, "status").GetString());
            Assert.Contains("not observed", Get(claude, "observed").GetString());
            Assert.Contains("2.1.310", Get(claude, "verifiedVersionModes").GetString());
            Assert.Contains("CLAUDE_CODE_DISABLE_PERMISSION_PROMPT_NOTIFY_HOOKS", Get(claude, "knownUnavailable").GetString());
            Assert.Contains("possibly waiting for approval", Get(claude, "knownUnavailable").GetString());
            var codex = Get(coverage, "codex");
            Assert.Contains("configured, not yet observed", Get(codex, "status").GetString());
            Assert.Contains("0.164.0", Get(codex, "verifiedVersionModes").GetString());
            Assert.Equal(0, Get(Get(claude, "waits"), "confirmed").GetInt32());
        }
    }

    [Fact]
    public void ObservedClaudeConfirmationRecordsWhenAndInWhichModeWithoutSessionContent()
    {
        using var workspace = new TestWorkspace();
        using var input = JsonDocument.Parse("""{"session_id":"fixture","cwd":"D:/example","hook_event_name":"Notification","notification_type":"permission_prompt","permission_mode":"default","message":"private-message"}""");
        var attachment = new Attachment("fixture-attachment", "claude", null, new(), "D:/example", DateTimeOffset.UtcNow);
        CaptureSpool.Write(workspace.Paths.Root, CaptureFactory.Create("claude", input.RootElement, attachment, DateTimeOffset.UtcNow)!);
        var state = new StateStore(workspace.Paths).Drain();
        Assert.Equal(AttentionReason.Permission, state.Sessions["claude:fixture"].Reason);
        var coverage = Coverage(workspace, out var document);
        using (document)
        {
            var claude = Get(coverage, "claude");
            Assert.Equal("observed", Get(claude, "status").GetString());
            Assert.Contains("default", Get(claude, "observed").GetString());
            Assert.Equal(1, Get(Get(claude, "waits"), "confirmed").GetInt32());
        }
        Assert.DoesNotContain("private-message", File.ReadAllText(ConfirmationEvidence.Path(workspace.Paths, "claude")));
    }

    [Fact]
    public void CodexSessionsWithoutAVerifiedRuntimeAreUnavailableAndTheirRequestsStayUnverified()
    {
        using var workspace = new TestWorkspace();
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Permission, DateTimeOffset.UtcNow, "tool") with
        {
            Provider = "codex",
            Directory = workspace.Root
        });
        var state = new StateStore(workspace.Paths).Drain();
        var session = state.Sessions["codex:session-1"];
        Assert.Null(session.Reason);
        var coverage = Coverage(workspace, out var document);
        using (document)
        {
            var codex = Get(coverage, "codex");
            Assert.Equal(1, Get(codex, "activeSessions").GetInt32());
            Assert.Equal(0, Get(codex, "withVerifiableRuntime").GetInt32());
            Assert.Equal(1, Get(codex, "unavailableWithoutRuntime").GetInt32());
            Assert.StartsWith("unavailable", Get(codex, "status").GetString());
            Assert.Contains("possibly waiting for approval", Get(codex, "status").GetString());
            var waits = Get(codex, "waits");
            Assert.Equal(1, Get(waits, "provisional").GetInt32());
            Assert.Equal(0, Get(waits, "confirmed").GetInt32());
        }
    }

    [Fact]
    public void LegacyTimingOnlyWaitsAreCountedAsUnverifiedNeverConfirmed()
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        SessionReducer.Apply(state, TestWorkspace.Event(EventKind.Permission, DateTimeOffset.UtcNow.AddMinutes(-1), "tool"));
        var session = state.Sessions.Values.Single();
        session.Waits[0] = session.Waits[0] with { Confirmation = null };
        AtomicFile.Write(workspace.Paths.State, Json.Encode(state));
        var coverage = Coverage(workspace, out var document);
        using (document)
        {
            var waits = Get(Get(coverage, "claude"), "waits");
            Assert.Equal(1, Get(waits, "unverified").GetInt32());
            Assert.Equal(0, Get(waits, "confirmed").GetInt32());
        }
    }

    [Fact]
    public void TheMatrixKeepsTheCodexHookOnlyGapUnverifiedAndEveryNewRowStatesBothProviders()
    {
        var rows = CapabilityMatrix.Rows.Where(row => row.Surface.StartsWith("Human-wait", StringComparison.Ordinal) ||
            row.Surface.StartsWith("Question-tool", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, rows.Count);
        var gap = rows.Single(row => row.Surface.Contains("without an owning runtime", StringComparison.Ordinal));
        Assert.Equal(CapabilityMatrix.Unverified, gap.Codex.Status);
        Assert.Contains("Closing evidence", gap.Codex.Detail);
        Assert.Equal(CapabilityMatrix.Supported, gap.Claude.Status);
    }
}
