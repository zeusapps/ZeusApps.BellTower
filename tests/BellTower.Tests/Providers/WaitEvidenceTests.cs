using System.Text;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;
using BellTower.Sessions;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Providers;

/// <summary>
/// Task 4.8 and 4.9 (design decision 12): a held Codex approval is confirmed from the transcript's approval reviewer, an automatically reviewed
/// one never alerts, a declined Claude Code prompt clears the wait from the transcript, and timing never confirms anything. The fixtures are
/// redacted captures from Codex 0.171.0 and Claude Code 2.1.330 (Fixtures/approval).
/// </summary>
public sealed class WaitEvidenceTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "approval", name);

    private static IReadOnlyList<string> Lines(string name) => File.ReadAllLines(FixturePath(name));

    // ---- The evidence the fixtures record ---------------------------------------------------------------------------------

    [Fact]
    public void TheCapturedPermissionRequestNamesNoReviewerAndTheInterruptHookExists()
    {
        using var request = JsonDocument.Parse(File.ReadAllText(FixturePath("codex-0.171.0-permission-request.json")));
        var payload = request.RootElement.GetProperty("payload");
        Assert.Equal("PermissionRequest", payload.GetProperty("hook_event_name").GetString());
        Assert.False(payload.TryGetProperty("approvals_reviewer", out _));
        Assert.False(payload.TryGetProperty("approval_policy", out _));
        using var interrupt = JsonDocument.Parse(File.ReadAllText(FixturePath("codex-0.171.0-interrupt.json")));
        Assert.Equal("Interrupt", interrupt.RootElement.GetProperty("payload").GetProperty("hook_event_name").GetString());
        // Both parse through the shared adapter into the kinds the reducer already understands.
        var attachment = new Attachment("a", "codex", null, new("unknown"), "D:/x", DateTimeOffset.UtcNow);
        Assert.Equal(EventKind.Permission, BellTower.Providers.HookAdapter.Parse("codex", payload, attachment)!.Kind);
        Assert.Equal(EventKind.Interrupt, BellTower.Providers.HookAdapter.Parse("codex", interrupt.RootElement.GetProperty("payload"), attachment)!.Kind);
    }

    [Fact]
    public void ACodexTranscriptNamesWhoDecidesForTheTurn()
    {
        Assert.True(CodexApprovalEvidence.HumanMustDecide(Lines("codex-0.171.0-transcript-held-approval.jsonl"), "<turn>"));
        Assert.True(CodexApprovalEvidence.HumanMustDecide(Lines("codex-0.171.0-transcript-held-approval.jsonl"), null));
        Assert.False(CodexApprovalEvidence.HumanMustDecide(Lines("codex-0.171.0-transcript-auto-review.jsonl"), "<turn>"));
        // Another turn's record is not this turn's evidence; no record at all confirms and suppresses nothing.
        Assert.Null(CodexApprovalEvidence.HumanMustDecide(Lines("codex-0.171.0-transcript-held-approval.jsonl"), "<other>"));
        Assert.Null(CodexApprovalEvidence.HumanMustDecide(["{\"type\":\"response_item\"}", "not json turn_context"], "<turn>"));
        Assert.Null(CodexApprovalEvidence.HumanMustDecide([], null));
    }

    [Fact]
    public void ADeclinedClaudePromptIsRecognizedForTheToolUseOfTheWaitOnly()
    {
        var lines = Lines("claude-2.1.330-declined-prompt.jsonl");
        var since = DateTimeOffset.Parse("2026-11-22T18:55:03Z");
        Assert.True(ClaudeDeclineEvidence.Declined(lines, ["toolu_declined"], since));
        Assert.Equal(DateTimeOffset.Parse("2026-11-22T18:55:12.486Z"), ClaudeDeclineEvidence.DeclinedAt(lines, ["toolu_declined"], since));
        Assert.True(ClaudeDeclineEvidence.Declined(lines, [], since));
        Assert.False(ClaudeDeclineEvidence.Declined(lines, ["toolu_other"], since));
        // A rejection recorded before the wait started belongs to an earlier prompt.
        Assert.False(ClaudeDeclineEvidence.Declined(lines, ["toolu_declined"], DateTimeOffset.Parse("2026-11-22T18:56:00Z")));
        Assert.False(ClaudeDeclineEvidence.Declined([], [], since));
    }

    [Fact]
    public void TheTranscriptReaderIsBoundedAndRefusesWhatIsNotAnAbsoluteFile()
    {
        using var workspace = new TestWorkspace();
        Assert.Empty(TranscriptTail.Read(null));
        Assert.Empty(TranscriptTail.Read("relative.jsonl"));
        Assert.Empty(TranscriptTail.Read(workspace.Root));
        Assert.Empty(TranscriptTail.Read(Path.Combine(workspace.Root, "missing.jsonl")));
        var path = Path.Combine(workspace.Root, "t.jsonl");
        var text = new StringBuilder();
        for (var index = 0; index < 400; index++)
        {
            text.Append("{\"n\":").Append(index).Append("}\n");
        }
        File.WriteAllText(path, text.ToString());
        var tail = TranscriptTail.Read(path, maxBytes: 200);
        Assert.InRange(tail.Count, 1, 20);
        Assert.Equal("{\"n\":399}", tail[^1]);
        Assert.All(tail, line => Assert.StartsWith("{\"n\":", line));
        // An unfinished last line (still being written) is never read, and an overlong line is dropped.
        File.WriteAllText(path, "{\"a\":1}\n{\"b\":");
        Assert.Equal(["{\"a\":1}"], TranscriptTail.Read(path));
        File.WriteAllText(path, new string('x', TranscriptTail.MaxLineBytes + 1) + "\n{\"ok\":1}\n");
        Assert.Equal(["{\"ok\":1}"], TranscriptTail.Read(path));
    }

    // ---- Codex: confirmed or suppressed from the transcript, never from time --------------------------------------------------

    private static SessionState Session(TestWorkspace workspace, string provider, string id, DateTimeOffset at, string? transcriptFixture, string turn = "<turn>")
    {
        var attachment = new Attachment(provider + "-" + id, provider, ProcessProbe.Identify(Environment.ProcessId), new("unknown"), workspace.Root, DateTimeOffset.UtcNow);
        new AttachmentStore(workspace.Paths).Save(attachment);
        // The turn the transcript record is matched against comes from the prompt that started it.
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Prompt, at.AddSeconds(-1), null, turn) with
        {
            Provider = provider,
            SessionId = id,
            AttachmentId = attachment.Id,
            Directory = workspace.Root
        });
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Permission, at, null, turn) with
        {
            Provider = provider,
            SessionId = id,
            AttachmentId = attachment.Id,
            Directory = workspace.Root
        });
        var session = new StateStore(workspace.Paths).Drain().Sessions[provider + ":" + id];
        if (transcriptFixture is not null)
        {
            var copy = Path.Combine(workspace.Root, provider + "-" + id + ".jsonl");
            File.Copy(FixturePath(transcriptFixture), copy, true);
            new IdentityStore(workspace.Paths).Update(session.Key, workspace.Root, identity => BellTower.Core.HistoryState.Bind(identity, copy));
        }
        return session;
    }

    [Fact]
    public async Task AHeldCodexApprovalWithNoRuntimeBecomesAConfirmedWaitAndAutomaticReviewNeverAlerts()
    {
        using var workspace = new TestWorkspace();
        var held = Session(workspace, "codex", "held", DateTimeOffset.UtcNow.AddSeconds(-30), "codex-0.171.0-transcript-held-approval.jsonl");
        var auto = Session(workspace, "codex", "auto", DateTimeOffset.UtcNow.AddSeconds(-30), "codex-0.171.0-transcript-auto-review.jsonl");
        var unknown = Session(workspace, "codex", "unknown", DateTimeOffset.UtcNow.AddSeconds(-30), null);
        Assert.Equal(HumanConfirmation.Provisional, held.Waits.Single().Confirmation);
        using var pump = new CodexPermissionPump(workspace.Paths);
        await pump.RefreshAsync([held, auto, unknown]);
        var now = DateTimeOffset.UtcNow;
        pump.Apply([held, auto, unknown], now);

        // The held approval is a human wait now, with the confirmation recorded through the shared observation.
        Assert.True(held.NativePermissionPending);
        Assert.Equal(AttentionReason.Permission, held.Reason);
        Assert.False(held.ConfirmationSignalUnavailable);
        var drained = new StateStore(workspace.Paths).Drain();
        Assert.Equal(HumanConfirmation.Confirmed, drained.Sessions[held.Key].Waits.Single().Confirmation);
        Assert.True(drained.Sessions[held.Key].RequiresAttention(now.AddSeconds(10)));

        // Automatic review is suppressed: no attention, no sound, not even the unverified chip, and nothing is confirmed.
        Assert.False(auto.NativePermissionPending);
        Assert.Null(auto.Reason);
        Assert.False(auto.PossiblyWaiting(now.AddMinutes(5)));
        Assert.False(auto.RequiresAttention(now.AddMinutes(5)));
        Assert.Equal(HumanConfirmation.Provisional, drained.Sessions[auto.Key].Waits.Single().Confirmation);

        // No transcript evidence: nothing is confirmed, however long it waits; the silent unverified chip is the only effect.
        Assert.Null(unknown.NativePermissionPending);
        Assert.True(unknown.ConfirmationSignalUnavailable);
        Assert.Null(unknown.Reason);
        Assert.False(unknown.RequiresAttention(now.AddHours(1)));
        Assert.True(unknown.PossiblyWaiting(now.AddMinutes(5)));
    }

    [Fact]
    public async Task ATranscriptFromAnotherTurnNeverConfirmsAWait()
    {
        using var workspace = new TestWorkspace();
        var session = Session(workspace, "codex", "other-turn", DateTimeOffset.UtcNow.AddSeconds(-30), "codex-0.171.0-transcript-held-approval.jsonl", turn: "<another>");
        using var pump = new CodexPermissionPump(workspace.Paths);
        await pump.RefreshAsync([session]);
        pump.Apply([session], DateTimeOffset.UtcNow);
        Assert.Null(session.NativePermissionPending);
        Assert.Null(session.Reason);
    }

    [Fact]
    public void DoctorCountsASessionWithATranscriptAsHavingEvidenceAndOneWithoutAsUnavailable()
    {
        using var workspace = new TestWorkspace();
        Session(workspace, "codex", "with-transcript", DateTimeOffset.UtcNow.AddSeconds(-30), "codex-0.171.0-transcript-held-approval.jsonl");
        Session(workspace, "codex", "without", DateTimeOffset.UtcNow.AddSeconds(-30), null);
        using var report = JsonDocument.Parse(BellTower.IO.Json.Encode(new BellTower.Diagnostics.ConfirmationCoverageReporter(workspace.Paths).Report()));
        var codex = report.RootElement.EnumerateObject().First(property => string.Equals(property.Name, "codex", StringComparison.OrdinalIgnoreCase)).Value;
        int Count(string name) => codex.EnumerateObject().First(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)).Value.GetInt32();
        Assert.Equal(2, Count("activeSessions"));
        Assert.Equal(1, Count("withTranscriptEvidence"));
        Assert.Equal(1, Count("unavailableWithoutRuntime"));
    }

    // ---- Claude Code: the decline clears the wait --------------------------------------------------------------------------

    [Fact]
    public void ADeclinedClaudePromptClearsTheWaitThroughTheSharedInterruptRule()
    {
        using var workspace = new TestWorkspace();
        var waitStarted = DateTimeOffset.Parse("2026-11-22T18:55:03.2Z");
        var session = Session(workspace, "claude", "declined", waitStarted, "claude-2.1.330-declined-prompt.jsonl");
        Assert.NotEmpty(session.Waits);
        using var watcher = new ClaudeDeclineWatcher(workspace.Paths);
        Assert.Equal(1, watcher.Check([session]));
        // The same decline is published once.
        Assert.Equal(0, watcher.Check([session]));
        var after = new StateStore(workspace.Paths).Drain().Sessions[session.Key];
        Assert.Empty(after.Waits);
        Assert.True(after.ForegroundStopped);
        Assert.Null(after.Reason);
    }

    [Fact]
    public void AWaitStartedAfterTheRecordedRejectionOrWithoutATranscriptIsLeftAlone()
    {
        using var workspace = new TestWorkspace();
        var later = Session(workspace, "claude", "later", DateTimeOffset.Parse("2026-11-22T19:30:00Z"), "claude-2.1.330-declined-prompt.jsonl");
        var none = Session(workspace, "claude", "none", DateTimeOffset.Parse("2026-11-22T18:55:03Z"), null);
        using var watcher = new ClaudeDeclineWatcher(workspace.Paths);
        Assert.Equal(0, watcher.Check([later, none]));
        var state = new StateStore(workspace.Paths).Drain();
        Assert.NotEmpty(state.Sessions[later.Key].Waits);
        Assert.NotEmpty(state.Sessions[none.Key].Waits);
    }

    // ---- Both providers together ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task MixedSessionsEachFollowTheirOwnProvidersEvidence()
    {
        using var workspace = new TestWorkspace();
        var codexHeld = Session(workspace, "codex", "held", DateTimeOffset.UtcNow.AddSeconds(-30), "codex-0.171.0-transcript-held-approval.jsonl");
        var codexAuto = Session(workspace, "codex", "auto", DateTimeOffset.UtcNow.AddSeconds(-30), "codex-0.171.0-transcript-auto-review.jsonl");
        var claudeDeclined = Session(workspace, "claude", "declined", DateTimeOffset.Parse("2026-11-22T18:55:03.2Z"), "claude-2.1.330-declined-prompt.jsonl");
        using var pump = new CodexPermissionPump(workspace.Paths);
        using var watcher = new ClaudeDeclineWatcher(workspace.Paths);
        await pump.RefreshAsync([codexHeld, codexAuto, claudeDeclined]);
        watcher.Check([codexHeld, codexAuto, claudeDeclined]);
        var now = DateTimeOffset.UtcNow;
        pump.Apply([codexHeld, codexAuto, claudeDeclined], now);
        var state = new StateStore(workspace.Paths).Drain();
        Assert.Equal(HumanConfirmation.Confirmed, state.Sessions[codexHeld.Key].Waits.Single().Confirmation);
        Assert.Equal(HumanConfirmation.Provisional, state.Sessions[codexAuto.Key].Waits.Single().Confirmation);
        Assert.Empty(state.Sessions[claudeDeclined.Key].Waits);
        Assert.True(codexHeld.Reason is AttentionReason.Permission);
        Assert.Null(codexAuto.Reason);
    }

    // ---- DEV-060: ANALYSIS-4706 and ANALYSIS-4709 ---------------------------------------------------------------------------------

    private static string Rejection(DateTimeOffset at, string toolUseId) =>
        "{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":[{\"type\":\"tool_result\",\"content\":\"rejected\",\"is_error\":true,\"tool_use_id\":\"" + toolUseId +
        "\"}]},\"timestamp\":\"" + at.UtcDateTime.ToString("O") + "\",\"toolDenialKind\":\"user-rejected\"}";

    private static SessionState ClaudeWait(TestWorkspace workspace, string id, DateTimeOffset since, string transcript, string? operation = null)
    {
        var attachment = new Attachment("claude-" + id, "claude", ProcessProbe.Identify(Environment.ProcessId), new("unknown"), workspace.Root, DateTimeOffset.UtcNow);
        new AttachmentStore(workspace.Paths).Save(attachment);
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Permission, since, operation) with
        {
            Provider = "claude",
            SessionId = id,
            AttachmentId = attachment.Id,
            Directory = workspace.Root
        });
        var session = new StateStore(workspace.Paths).Drain().Sessions["claude:" + id];
        new IdentityStore(workspace.Paths).Update(session.Key, workspace.Root, identity => BellTower.Core.HistoryState.Bind(identity, transcript));
        return session;
    }

    [Fact]
    public void ARejectionFromTheEarlierPromptNeverUsesUpTheDeclineOfTheWaitThatFollows()
    {
        using var workspace = new TestWorkspace();
        var transcript = Path.Combine(workspace.Root, "claude.jsonl");
        var first = DateTimeOffset.UtcNow.AddSeconds(-30);
        // The first prompt was declined 2 s before the second wait began; there are no tool use identifiers to tell them apart.
        File.WriteAllLines(transcript, [Rejection(first, "toolu_first")]);
        var waitStarted = first.AddSeconds(2);
        var session = ClaudeWait(workspace, "two-prompts", waitStarted, transcript);
        using var watcher = new ClaudeDeclineWatcher(workspace.Paths);
        Assert.Equal(0, watcher.Check([session]));

        // The real decline of the second wait arrives later and is published once, at its own time.
        File.AppendAllText(transcript, Rejection(waitStarted.AddSeconds(3), "toolu_second") + "\n");
        Assert.Equal(1, watcher.Check([session]));
        Assert.Equal(0, watcher.Check([session]));
        var after = new StateStore(workspace.Paths).Drain().Sessions[session.Key];
        Assert.Empty(after.Waits);
        // A later prompt declined afterwards is a different decline and can still be published.
        Assert.Equal(1, DeclineRecordCount(workspace));
    }

    private static int DeclineRecordCount(TestWorkspace workspace) => DeclineEvidenceRecord.Read(workspace.Paths).DeclinesPublished;

    [Fact]
    public void AMatchedToolUseIdToleratesAClockSkewButAnUnmatchedOneDoesNot()
    {
        using var workspace = new TestWorkspace();
        var transcript = Path.Combine(workspace.Root, "claude.jsonl");
        var since = DateTimeOffset.UtcNow.AddSeconds(-20);
        File.WriteAllLines(transcript, [Rejection(since.AddSeconds(-3), "toolu_other"), Rejection(since.AddSeconds(-3), "toolu_mine")]);
        var session = ClaudeWait(workspace, "ids", since, transcript, "toolu_mine");
        using var watcher = new ClaudeDeclineWatcher(workspace.Paths);
        Assert.Equal(1, watcher.Check([session]));
        Assert.True(ClaudeDeclineEvidence.Declined([Rejection(since.AddSeconds(-3), "toolu_mine")], ["toolu_mine"], since));
        Assert.False(ClaudeDeclineEvidence.Declined([Rejection(since.AddSeconds(-3), "toolu_other")], ["toolu_mine"], since));
        Assert.False(ClaudeDeclineEvidence.Declined([Rejection(since.AddSeconds(-3), "toolu_mine")], [], since));
    }

    [Fact]
    public void AFailedPublicationIsLoggedOncePerSessionAndNeverThrows()
    {
        using var workspace = new TestWorkspace();
        var transcript = Path.Combine(workspace.Root, "claude.jsonl");
        var since = DateTimeOffset.UtcNow.AddSeconds(-20);
        File.WriteAllLines(transcript, [Rejection(since.AddSeconds(2), "toolu_x")]);
        // An observation the store refuses (an empty attachment identifier) is an expected failure, not an unhandled fault.
        var bad = new SessionState { Key = "claude:bad", Provider = "claude", SessionId = "bad", AttachmentId = "", Directory = workspace.Root };
        bad.Waits.Add(new WaitState(AttentionReason.Permission, since, null, null));
        new IdentityStore(workspace.Paths).Update(bad.Key, workspace.Root, identity => BellTower.Core.HistoryState.Bind(identity, transcript));
        using var watcher = new ClaudeDeclineWatcher(workspace.Paths);
        Assert.Equal(0, watcher.Check([bad]));
        Assert.Equal(0, watcher.Check([bad]));
        Assert.Equal(0, watcher.Check([bad]));
        var log = File.ReadAllLines(Path.Combine(workspace.Paths.Root, "bell-tower.log")).Count(line => line.Contains("decline-evidence-unavailable", StringComparison.Ordinal));
        Assert.Equal(1, log);
        Assert.Equal("InvalidDataException", DeclineEvidenceRecord.Read(workspace.Paths).LastFailure);
    }

    [Fact]
    public void DoctorCountsOnlyCodexSessionsWhoseTranscriptProducedARecordAndShowsTheClaudeDeclineCounters()
    {
        using var workspace = new TestWorkspace();
        Session(workspace, "codex", "record", DateTimeOffset.UtcNow.AddSeconds(-30), "codex-0.171.0-transcript-held-approval.jsonl");
        // A transcript path with no turn record (a Claude transcript, drifted or out of window) is not coverage.
        Session(workspace, "codex", "no-record", DateTimeOffset.UtcNow.AddSeconds(-30), "claude-2.1.330-declined-prompt.jsonl");
        Session(workspace, "codex", "no-path", DateTimeOffset.UtcNow.AddSeconds(-30), null);
        var transcript = Path.Combine(workspace.Root, "claude-doctor.jsonl");
        var since = DateTimeOffset.UtcNow.AddSeconds(-20);
        File.WriteAllLines(transcript, [Rejection(since.AddSeconds(2), "toolu_doc")]);
        var claude = ClaudeWait(workspace, "doctor", since, transcript);
        using (var watcher = new ClaudeDeclineWatcher(workspace.Paths))
        {
            Assert.Equal(1, watcher.Check([claude]));
        }
        using var report = JsonDocument.Parse(BellTower.IO.Json.Encode(new BellTower.Diagnostics.ConfirmationCoverageReporter(workspace.Paths).Report()));
        JsonElement Get(JsonElement element, string name) => element.EnumerateObject().First(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)).Value;
        var codex = Get(report.RootElement, "codex");
        Assert.Equal(3, Get(codex, "activeSessions").GetInt32());
        Assert.Equal(1, Get(codex, "withTranscriptEvidence").GetInt32());
        Assert.Equal(2, Get(codex, "unavailableWithoutRuntime").GetInt32());
        var decline = Get(Get(report.RootElement, "claude"), "decline");
        Assert.Equal(1, Get(decline, "sessionsWithKnownTranscript").GetInt32());
        Assert.Equal(1, Get(decline, "declinesPublished").GetInt32());
        Assert.Equal("none", Get(decline, "lastReadFailure").GetString());
    }
}
