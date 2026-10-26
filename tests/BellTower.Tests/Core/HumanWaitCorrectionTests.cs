using BellTower.Core;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Core;

// Fix round 1 for confirm-claude-human-permission-waits: Codex runtime confirmation takes the shared stored transition,
// a prompt can only confirm requests old enough to be its source, an ambiguous prompt is never credited to a permission by
// elimination, and sessions without a supported signal show a silent, visibly unverified chip. Fixtures only, no live providers.
public sealed class HumanWaitCorrectionTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(5);

    private static string Id(int number) => number.ToString("x32");

    private static string PermissionScope(string episode) => new WaitState(AttentionReason.Permission, DateTimeOffset.UnixEpoch,
        null, null, Episode: episode).ConfirmationScopeId;

    private static Observation Observe(string provider, EventKind kind, DateTimeOffset at, int id, bool causal, string? operation = null,
        string? fingerprint = null) => TestWorkspace.Event(kind, at, operation) with
        {
            Provider = provider,
            Id = Id(id),
            OperationFingerprint = fingerprint,
            IngressAt = causal ? at : null
        };

    private static Observation Prompt(string provider, DateTimeOffset at, int id, bool causal) =>
        Observe(provider, EventKind.Permission, at, id, causal) with { HumanPrompt = true, ProviderDelay = Delay };

    // What the Codex pump publishes when the owning runtime first reports a pending approval.
    private static Observation RuntimeConfirmation(DateTimeOffset at, int id, bool causal, params string[] scope) =>
        Observe("codex", EventKind.Permission, at, id, causal) with
        {
            HumanPrompt = true,
            RuntimeApproval = true,
            RuntimeApprovalScope = scope.Select(PermissionScope).ToArray(),
            RuntimeApprovalRevision = scope.Length
        };

    private static SessionState Session(AppState state, string provider) => state.Sessions[provider + ":session-1"];

    [Fact]
    public void RuntimeApprovalScopeIncludesCanonicalCandidateMembership()
    {
        var since = DateTimeOffset.UtcNow;
        var both = new WaitState(AttentionReason.Permission, since, null, "private-fingerprint", ["candidate-secret-alpha", "candidate-secret-beta"], Id(1));
        var reversed = both with { Candidates = ["candidate-secret-beta", "candidate-secret-alpha"] };
        var narrowed = both with { Candidates = ["candidate-secret-beta"] };

        Assert.Equal(both.ConfirmationScopeId, reversed.ConfirmationScopeId);
        Assert.NotEqual(both.ConfirmationScopeId, narrowed.ConfirmationScopeId);
        Assert.DoesNotContain("private-fingerprint", both.ConfirmationScopeId);
        Assert.DoesNotContain("candidate-secret", both.ConfirmationScopeId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CodexAutoApprovedNewRequestCannotReannounceAnOlderWait(bool causal)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start);
        SessionReducer.Apply(state, Observe("codex", EventKind.Permission, start.AddMinutes(-1), 1, causal, "old", "old-print"));
        SessionReducer.Apply(state, RuntimeConfirmation(start.AddSeconds(-55), 2, causal, Id(1)));
        var session = Session(state, "codex");
        session.NativePermissionPending = true;
        session.NativePermissionSince = start.AddSeconds(-55);
        Assert.Equal(HumanConfirmation.Confirmed, Assert.Single(session.Waits).Confirmation);
        Assert.Empty(policy.Collect([session], start, false));
        var episode = session.Episode;
        SessionReducer.Apply(state, Observe("codex", EventKind.Permission, start.AddSeconds(1), 3, causal, "new", "new-print"));
        session = Session(state, "codex");
        Assert.Equal(episode, session.Episode);
        // The pump's next publication (the oldest provisional request is now the new one) while the runtime still reports pending:
        // a continuing positive status is the older wait's evidence and is not credited to the newer request.
        SessionReducer.Apply(state, RuntimeConfirmation(start.AddSeconds(2), 9, causal, Id(1), Id(3)));
        session = Session(state, "codex");
        Assert.Equal(episode, session.Episode);
        Assert.Equal(HumanConfirmation.Provisional, session.Waits.Single(wait => wait.OperationId == "new").Confirmation);
        Assert.Empty(policy.Collect([session], start.AddSeconds(30), false));
        SessionReducer.Apply(state, Observe("codex", EventKind.ToolEnd, start.AddSeconds(31), 4, causal, "new", "new-print"));
        session = Session(state, "codex");
        Assert.Equal(episode, session.Episode);
        Assert.True(session.RequiresAttention(start.AddSeconds(32)));
        Assert.Single(session.Waits);
        Assert.Empty(policy.Collect([session], start.AddSeconds(32), false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CodexQuestionAndConfirmedPermissionKeepTheirOwnEpisodesAndAnnouncements(bool causal)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        SessionReducer.Apply(state, Observe("codex", EventKind.Question, start, 1, causal, "question", "question-print"));
        var session = Session(state, "codex");
        var questionEpisode = session.Episode;
        Assert.Single(policy.Collect([session], start.AddSeconds(1), false));
        SessionReducer.Apply(state, Observe("codex", EventKind.Permission, start.AddSeconds(1), 2, causal, "tool", "tool-print"));
        session = Session(state, "codex");
        Assert.Equal(questionEpisode, session.Episode);
        // The question chip is dismissed before the runtime confirms the permission request.
        session.AcknowledgedEpisode = session.Episode;
        SessionReducer.Apply(state, RuntimeConfirmation(start.AddSeconds(7), 3, causal, Id(2)));
        session = Session(state, "codex");
        session.NativePermissionPending = true;
        session.NativePermissionSince = start.AddSeconds(7);
        Assert.Equal(AttentionReason.Permission, session.Reason);
        Assert.NotEqual(questionEpisode, session.Episode);
        Assert.True(session.RequiresAttention(start.AddSeconds(12)));
        var announced = Assert.Single(policy.Collect([session], start.AddSeconds(12), false));
        Assert.Equal(AttentionReason.Permission, announced.Reason);
        Assert.Equal(session.Episode, announced.Episode);
    }

    [Fact]
    public void CausalAndUnorderedPathsAgreeOnEpisodesAndConfirmationForTheSameCodexSequence()
    {
        var start = DateTimeOffset.UtcNow;
        (string? Episode, DateTimeOffset? EpisodeStarted, AttentionReason? Reason, string Waits)[] Run(bool causal)
        {
            var state = new AppState();
            var steps = new[]
            {
                Observe("codex", EventKind.Question, start, 1, causal, "question", "question-print"),
                Observe("codex", EventKind.Permission, start.AddSeconds(1), 2, causal, "first", "first-print"),
                RuntimeConfirmation(start.AddSeconds(7), 3, causal, Id(2)),
                Observe("codex", EventKind.Permission, start.AddSeconds(8), 4, causal, "second", "second-print"),
                Observe("codex", EventKind.ToolEnd, start.AddSeconds(9), 5, causal, "second", "second-print"),
                Observe("codex", EventKind.ToolEnd, start.AddSeconds(10), 6, causal, "question", "question-print"),
                Observe("codex", EventKind.ToolEnd, start.AddSeconds(11), 7, causal, "first", "first-print")
            };
            return steps.Select(step =>
            {
                SessionReducer.Apply(state, step);
                var session = Session(state, "codex");
                return (session.Episode, session.EpisodeStarted, session.Reason,
                    string.Join("|", session.Waits.Select(wait => $"{wait.Reason}:{wait.OperationId}:{wait.Confirmation}:{wait.Episode}")));
            }).ToArray();
        }

        Assert.Equal(Run(false), Run(true));
    }

    [Fact]
    public void RuntimeConfirmationNeverCreatesAWaitAndAnAlreadyResolvedRequestIsNotResurrected()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, RuntimeConfirmation(start, 1, false));
        Assert.Empty(Session(state, "codex").Waits);
        SessionReducer.Apply(state, Observe("codex", EventKind.Permission, start.AddSeconds(1), 2, false, "tool", "print"));
        SessionReducer.Apply(state, Observe("codex", EventKind.ToolEnd, start.AddSeconds(2), 3, false, "tool", "print"));
        SessionReducer.Apply(state, RuntimeConfirmation(start.AddSeconds(3), 4, false));
        Assert.Empty(Session(state, "codex").Waits);
        Assert.Null(Session(state, "codex").Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RuntimeConfirmationRequiresTheExactInspectedPermissionScope(bool causal)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, Observe("codex", EventKind.Permission, start, 1, causal, "first", "first-print"));
        SessionReducer.Apply(state, RuntimeConfirmation(start.AddSeconds(1), 2, causal));
        Assert.Equal(HumanConfirmation.Provisional, Assert.Single(Session(state, "codex").Waits).Confirmation);

        // A positive result captured for a different or now resolved wait cannot be credited to the first request.
        SessionReducer.Apply(state, RuntimeConfirmation(start.AddSeconds(2), 3, causal, Id(9)));
        Assert.Equal(HumanConfirmation.Provisional, Assert.Single(Session(state, "codex").Waits).Confirmation);

        // A positive result for the unchanged inspected scope remains usable.
        SessionReducer.Apply(state, RuntimeConfirmation(start.AddSeconds(3), 4, causal, Id(1)));
        Assert.Equal(HumanConfirmation.Confirmed, Assert.Single(Session(state, "codex").Waits).Confirmation);
    }

    [Fact]
    public void ACodexRuntimeConfirmationCreditsNoEarlierDelayAndGraceStartsAtTheReport()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, Observe("codex", EventKind.Permission, start, 1, false, "tool", "print"));
        SessionReducer.Apply(state, RuntimeConfirmation(start.AddSeconds(20), 2, false, Id(1)));
        var session = Session(state, "codex");
        Assert.Equal(start.AddSeconds(25), session.Due);
        Assert.False(session.RequiresAttention(start.AddSeconds(24)));
        Assert.True(session.RequiresAttention(start.AddSeconds(25)));
    }

    [Fact]
    public void APromptCannotConfirmARequestYoungerThanTheProviderDelay()
    {
        // Probe P4: the newer sibling began 1.5 s before the prompt, so only the older request can be its source.
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, Observe("claude", EventKind.Permission, start, 1, false, null, "older"));
        SessionReducer.Apply(state, Observe("claude", EventKind.Permission, start.AddSeconds(4.5), 2, false, null, "newer"));
        SessionReducer.Apply(state, Prompt("claude", start.AddSeconds(6), 3, false));
        var session = Session(state, "claude");
        var older = session.Waits.Single(wait => wait.Fingerprint == "older");
        var newer = session.Waits.Single(wait => wait.Fingerprint == "newer");
        Assert.Equal(HumanConfirmation.Confirmed, older.Confirmation);
        Assert.False(older.Ambiguous);
        Assert.Equal(HumanConfirmation.Provisional, newer.Confirmation);
        Assert.True(session.RequiresAttention(start.AddSeconds(6)));
    }

    [Fact]
    public void ARacingPromptAfterAResolutionNeverConfirmsAYoungSibling()
    {
        // Probe P5: the first request is answered at 6.0 s and its prompt arrives in a race at 6.4 s.
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, Observe("claude", EventKind.Permission, start, 1, false, null, "first"));
        SessionReducer.Apply(state, Observe("claude", EventKind.Permission, start.AddSeconds(5.5), 2, false, null, "second"));
        SessionReducer.Apply(state, Observe("claude", EventKind.ToolEnd, start.AddSeconds(6), 3, false, null, "first"));
        SessionReducer.Apply(state, Prompt("claude", start.AddSeconds(6.4), 4, false));
        var session = Session(state, "claude");
        var second = Assert.Single(session.Waits);
        Assert.Equal(HumanConfirmation.Provisional, second.Confirmation);
        Assert.Null(session.Reason);
        Assert.False(session.RequiresAttention(start.AddMinutes(1)));
    }

    [Fact]
    public void ARacingPromptAfterAResolutionMarksAnOldEnoughSiblingAmbiguousInsteadOfConfirmingIt()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, Observe("claude", EventKind.Permission, start, 1, false, null, "first"));
        SessionReducer.Apply(state, Observe("claude", EventKind.Permission, start.AddSeconds(0.5), 2, false, null, "second"));
        SessionReducer.Apply(state, Observe("claude", EventKind.ToolEnd, start.AddSeconds(6), 3, false, null, "first"));
        SessionReducer.Apply(state, Prompt("claude", start.AddSeconds(6.4), 4, false));
        var second = Assert.Single(Session(state, "claude").Waits);
        Assert.Equal(HumanConfirmation.Provisional, second.Confirmation);
        Assert.True(second.Ambiguous);
        // A prompt well after the resolution is the sibling's own notification.
        SessionReducer.Apply(state, Prompt("claude", start.AddSeconds(16), 5, false));
        Assert.Equal(HumanConfirmation.Confirmed, Assert.Single(Session(state, "claude").Waits).Confirmation);
    }

    private static AppState QuestionAndPermissionState(DateTimeOffset start, params (string Print, double At)[] permissions)
    {
        var state = new AppState();
        var id = 1;
        foreach (var (print, at) in permissions)
        {
            SessionReducer.Apply(state, Observe("claude", EventKind.Permission, start.AddSeconds(at), id++, false, null, print));
        }
        SessionReducer.Apply(state, Observe("claude", EventKind.Question, start.AddSeconds(1), id++, false, "question", "question-print"));
        SessionReducer.Apply(state, Prompt("claude", start.AddSeconds(6.5), id++, false));
        return state;
    }

    [Fact]
    public void AnAmbiguousPromptIsNeverCreditedToThePermissionWhenTheQuestionResolves()
    {
        // ANALYSIS-100 / task 2A.6: the question's own dialog emits a permission_prompt, so once the question resolves the earlier
        // prompt is not evidence for the remaining request. It waits for its own later prompt.
        var start = DateTimeOffset.UtcNow;
        var policy = new AlertPolicy(start.AddSeconds(-1));
        var state = QuestionAndPermissionState(start, ("bash", 0));
        var session = Session(state, "claude");
        Assert.All(session.Waits, wait => Assert.True(wait.Ambiguous));
        Assert.Equal(AttentionReason.Question, session.Reason);
        SessionReducer.Apply(state, Observe("claude", EventKind.ToolEnd, start.AddSeconds(8), 10, false, "question", "question-print"));
        session = Session(state, "claude");
        var permission = Assert.Single(session.Waits);
        Assert.NotEqual(HumanConfirmation.Confirmed, permission.Confirmation);
        Assert.Null(session.Reason);
        Assert.False(session.RequiresAttention(start.AddMinutes(1)));
        Assert.Empty(policy.Collect([session], start.AddMinutes(1), false));
        // Its own later prompt confirms it and clears the ambiguity.
        SessionReducer.Apply(state, Prompt("claude", start.AddSeconds(20), 11, false));
        session = Session(state, "claude");
        permission = Assert.Single(session.Waits);
        Assert.Equal(HumanConfirmation.Confirmed, permission.Confirmation);
        Assert.False(permission.Ambiguous);
        Assert.True(session.RequiresAttention(start.AddSeconds(20)));
        Assert.Equal(AttentionReason.Permission, Assert.Single(policy.Collect([session], start.AddSeconds(20), false)).Reason);
    }

    [Fact]
    public void AnAmbiguousPromptConfirmsNothingWithSeveralPermissionsAndNeverAfterTheQuestionResolves()
    {
        var start = DateTimeOffset.UtcNow;
        var state = QuestionAndPermissionState(start, ("first", 0), ("second", 0.2));
        SessionReducer.Apply(state, Observe("claude", EventKind.ToolEnd, start.AddSeconds(8), 10, false, "question", "question-print"));
        var session = Session(state, "claude");
        Assert.Equal(2, session.Waits.Count);
        Assert.All(session.Waits, wait => Assert.NotEqual(HumanConfirmation.Confirmed, wait.Confirmation));
        Assert.Null(session.Reason);
    }

    [Fact]
    public void AYoungPermissionCannotBeTheSourceSoThePromptConfirmsTheQuestionOnly()
    {
        var start = DateTimeOffset.UtcNow;
        var state = QuestionAndPermissionState(start, ("young", 3));
        var session = Session(state, "claude");
        Assert.Equal(HumanConfirmation.Confirmed, session.Waits.Single(wait => wait.Reason == AttentionReason.Question).Confirmation);
        SessionReducer.Apply(state, Observe("claude", EventKind.ToolEnd, start.AddSeconds(8), 10, false, "question", "question-print"));
        Assert.Equal(HumanConfirmation.Provisional, Assert.Single(Session(state, "claude").Waits).Confirmation);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ARequestWithNoSupportedSignalShowsASilentUnverifiedChipAfterFifteenSeconds(string provider)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        SessionReducer.Apply(state, Observe(provider, EventKind.Permission, start, 1, false, "tool", "print"));
        var session = Session(state, provider);
        // Without the adapter flag (a supported signal exists) the request stays invisible however long it lasts.
        Assert.False(session.PossiblyWaiting(start.AddMinutes(2)));
        session.ConfirmationSignalUnavailable = true;
        Assert.False(session.PossiblyWaiting(start.AddSeconds(14.9)));
        Assert.True(session.PossiblyWaiting(start.AddSeconds(15)));
        Assert.Equal(start, session.UnverifiedSince);
        // Never confirmed, never audible, never eligible for reminders or escalation.
        Assert.False(session.RequiresAttention(start.AddMinutes(10)));
        Assert.Null(session.Reason);
        Assert.Empty(policy.Collect([session], start.AddMinutes(10), false));
        Assert.NotEqual(HumanConfirmation.Confirmed, session.EffectiveConfirmation(Assert.Single(session.Waits)));
    }

    [Theory]
    [InlineData("claude", "ToolEnd")]
    [InlineData("codex", "ToolEnd")]
    [InlineData("claude", "Stop")]
    [InlineData("codex", "Stop")]
    [InlineData("claude", "End")]
    [InlineData("codex", "End")]
    [InlineData("claude", "Dismiss")]
    [InlineData("codex", "Dismiss")]
    public void TheUnverifiedChipClearsOnCorrelatedResolutionStopEndOrDismissal(string provider, string clearing)
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, Observe(provider, EventKind.Permission, start, 1, false, "tool", "print"));
        var session = Session(state, provider);
        session.ConfirmationSignalUnavailable = true;
        Assert.True(session.PossiblyWaiting(start.AddSeconds(20)));
        if (clearing == "Dismiss")
        {
            session.AcknowledgedEpisode = session.Episode;
        }
        else
        {
            SessionReducer.Apply(state, Observe(provider, Enum.Parse<EventKind>(clearing), start.AddSeconds(21), 2, false, "tool", "print"));
            session = Session(state, provider);
            session.ConfirmationSignalUnavailable = true;
        }
        Assert.False(session.PossiblyWaiting(start.AddSeconds(30)));
    }

    [Fact]
    public void AnUnrelatedResultLeavesTheUnverifiedChipAndAConfirmationReplacesIt()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, Observe("codex", EventKind.Permission, start, 1, false, "tool", "print"));
        SessionReducer.Apply(state, Observe("codex", EventKind.ToolEnd, start.AddSeconds(2), 2, false, "other", "other-print"));
        var session = Session(state, "codex");
        session.ConfirmationSignalUnavailable = true;
        Assert.True(session.PossiblyWaiting(start.AddSeconds(16)));
        SessionReducer.Apply(state, RuntimeConfirmation(start.AddSeconds(17), 3, false, Id(1)));
        session = Session(state, "codex");
        session.ConfirmationSignalUnavailable = true;
        Assert.False(session.PossiblyWaiting(start.AddSeconds(18)));
        Assert.Equal(AttentionReason.Permission, session.Reason);
    }

    [Fact]
    public void MixedSessionsKeepTheUnverifiedChipPerSession()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, Observe("claude", EventKind.Permission, start, 1, false, "tool", "print"));
        SessionReducer.Apply(state, Observe("codex", EventKind.Permission, start, 2, false, "tool", "print"));
        var claude = Session(state, "claude");
        var codex = Session(state, "codex");
        codex.ConfirmationSignalUnavailable = true;
        Assert.False(claude.PossiblyWaiting(start.AddMinutes(1)));
        Assert.True(codex.PossiblyWaiting(start.AddMinutes(1)));
        SessionReducer.Apply(state, Prompt("claude", start.AddSeconds(30), 3, false));
        Assert.Equal(AttentionReason.Permission, claude.Reason);
        Assert.True(codex.PossiblyWaiting(start.AddMinutes(1)));
        Assert.Null(codex.Reason);
    }

    private static Observation Restored(DateTimeOffset at, int id, bool causal, DateTimeOffset? restoredSince, params string[] scope) =>
        RuntimeConfirmation(at, id, causal, scope) with { RestoredSince = restoredSince };

    [Fact]
    public void ABackDatedRestoreApprovalStillConfirmsAfterMoreThanTheCausalWindowOfLaterEvents()
    {
        // ANALYSIS-101 (P10b): the approval keeps its journal position at observation time instead of being back-dated below the floor.
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, Observe("codex", EventKind.Permission, start.AddMinutes(-2), 1, true, "old", "old-print"));
        for (var index = 0; index < CausalSessionReducer.Window + 40; index++)
        {
            SessionReducer.Apply(state, Observe("codex", EventKind.ToolStart, start.AddMinutes(-1).AddMilliseconds(index), 100 + index, true, "other-" + index, "other-" + index));
        }
        SessionReducer.Apply(state, Restored(start, 5000, true, start.AddSeconds(-1), Id(1)));
        var session = Session(state, "codex");
        var wait = session.Waits.Single(item => item.OperationId == "old");
        Assert.Equal(HumanConfirmation.Confirmed, wait.Confirmation);
        Assert.Equal(wait.Since.AddMilliseconds(1), wait.ConfirmedAt);
        // Restored waits restore silently.
        Assert.Empty(new AlertPolicy(start).Collect([session], start.AddSeconds(10), false));
        Assert.True(session.RequiresAttention(start.AddSeconds(10)));
    }

    [Fact]
    public void ABackDatedRestoreApprovalConfirmsEveryPreRunRequestOnBothReducerPaths()
    {
        var start = DateTimeOffset.UtcNow;
        string Run(bool causal)
        {
            var state = new AppState();
            SessionReducer.Apply(state, Observe("codex", EventKind.Permission, start.AddSeconds(-60), 1, causal, "first", "first-print"));
            SessionReducer.Apply(state, Observe("codex", EventKind.Permission, start.AddSeconds(-50), 2, causal, "second", "second-print"));
            SessionReducer.Apply(state, Restored(start, 3, causal, start.AddSeconds(-1), Id(1), Id(2)));
            var session = Session(state, "codex");
            Assert.All(session.Waits, wait => Assert.Equal(wait.Since.AddMilliseconds(1), wait.ConfirmedAt));
            Assert.Empty(new AlertPolicy(start).Collect([session], start.AddSeconds(10), false));
            return string.Join("|", session.Waits.Select(wait => $"{wait.OperationId}:{wait.Confirmation}:{wait.ConfirmedAt:O}"));
        }

        Assert.Equal(Run(false), Run(true));
    }

    [Fact]
    public void ARequestNewerThanTheRestoreAnchorIsConfirmedAtTheObservationTime()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, Observe("codex", EventKind.Permission, start.AddSeconds(2), 1, false, "fresh", "fresh-print"));
        SessionReducer.Apply(state, Restored(start.AddSeconds(3), 2, false, start, Id(1)));
        Assert.Equal(start.AddSeconds(3), Assert.Single(Session(state, "codex").Waits).ConfirmedAt);
    }

    [Theory]
    [InlineData("claude", false)]
    [InlineData("claude", true)]
    [InlineData("codex", false)]
    [InlineData("codex", true)]
    public void ConfirmationAfterADismissedUnverifiedChipStartsAFreshAnnouncedEpisode(string provider, bool causal)
    {
        // QA-100 / task 2.7.
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        var policy = new AlertPolicy(start.AddSeconds(-1));
        SessionReducer.Apply(state, Observe(provider, EventKind.Permission, start, 1, causal, "tool", "print"));
        var session = Session(state, provider);
        session.ConfirmationSignalUnavailable = true;
        Assert.True(session.PossiblyWaiting(start.AddSeconds(20)));
        var unverifiedEpisode = session.Episode;
        session.AcknowledgedEpisode = session.Episode;
        var at = start.AddSeconds(30);
        SessionReducer.Apply(state, provider == "claude" ? Prompt(provider, at, 2, causal) : RuntimeConfirmation(at, 2, causal, Id(1)));
        session = Session(state, provider);
        Assert.Equal(AttentionReason.Permission, session.Reason);
        Assert.NotEqual(unverifiedEpisode, session.Episode);
        Assert.NotEqual(session.Episode, session.AcknowledgedEpisode);
        var due = session.Due!.Value;
        Assert.True(session.RequiresAttention(due));
        var announced = Assert.Single(policy.Collect([session], due, false));
        Assert.Equal(AttentionReason.Permission, announced.Reason);
        Assert.Equal(session.Episode, announced.Episode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AClaudeSessionWithNoObservedPromptShowsTheSilentUnverifiedChipAndLosesItOncePromptsAreObserved(bool causal)
    {
        // Task 2.8.
        var start = DateTimeOffset.UtcNow;
        var policy = new AlertPolicy(start.AddSeconds(-1));
        var state = new AppState();
        SessionReducer.Apply(state, Observe("claude", EventKind.Permission, start, 1, causal, "tool", "print"));
        var session = Session(state, "claude");
        BellTower.Providers.ClaudeConfirmationSignal.Apply([session]);
        Assert.True(session.ConfirmationSignalUnavailable);
        Assert.False(session.PossiblyWaiting(start.AddSeconds(14.9)));
        Assert.True(session.PossiblyWaiting(start.AddSeconds(15)));
        Assert.False(session.RequiresAttention(start.AddMinutes(10)));
        Assert.Empty(policy.Collect([session], start.AddMinutes(10), false));
        Assert.NotEqual(HumanConfirmation.Confirmed, session.EffectiveConfirmation(Assert.Single(session.Waits)));

        // A prompt observed in the session shows the signal works: the next request gets no unverified chip.
        var other = new AppState();
        SessionReducer.Apply(other, Prompt("claude", start, 2, causal));
        SessionReducer.Apply(other, Observe("claude", EventKind.Permission, start.AddSeconds(1), 3, causal, "tool", "print"));
        var observed = Session(other, "claude");
        Assert.True(observed.PromptObserved);
        BellTower.Providers.ClaudeConfirmationSignal.Apply([observed]);
        Assert.False(observed.ConfirmationSignalUnavailable);
        Assert.False(observed.PossiblyWaiting(start.AddMinutes(1)));
    }

    [Fact]
    public void ThePromptThatConfirmsTheRequestAlsoRemovesTheClaudeUnverifiedChip()
    {
        var start = DateTimeOffset.UtcNow;
        var state = new AppState();
        SessionReducer.Apply(state, Observe("claude", EventKind.Permission, start, 1, false, "tool", "print"));
        var session = Session(state, "claude");
        BellTower.Providers.ClaudeConfirmationSignal.Apply([session]);
        SessionReducer.Apply(state, Prompt("claude", start.AddSeconds(20), 2, false));
        session = Session(state, "claude");
        BellTower.Providers.ClaudeConfirmationSignal.Apply([session]);
        Assert.False(session.PossiblyWaiting(start.AddSeconds(30)));
        Assert.Equal(AttentionReason.Permission, session.Reason);
    }

    [Fact]
    public void ClaudeAdapterNeverFlagsCodexSessions()
    {
        var state = new AppState();
        SessionReducer.Apply(state, Observe("codex", EventKind.Permission, DateTimeOffset.UtcNow, 1, false, "tool", "print"));
        var codex = Session(state, "codex");
        BellTower.Providers.ClaudeConfirmationSignal.Apply([codex]);
        Assert.False(codex.ConfirmationSignalUnavailable);
    }
}
