using BellTower.Core;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Core;

public sealed class ReducerTests
{
    private readonly AppState state = new();
    private readonly DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-2);
    private SessionState Session => state.Sessions["claude:session-1"];

    private void Apply(Observation e) => SessionReducer.Apply(state, e);
    [Fact]
    public void CompletionHasTwentySecondGrace()
    {
        Apply(TestWorkspace.Event(EventKind.Stop, start));
        Assert.False(Session.RequiresAttention(start.AddSeconds(19)));
        Assert.True(Session.RequiresAttention(start.AddSeconds(20)));
    }

    [Theory]
    [InlineData(EventKind.Question, AttentionReason.Question)]
    [InlineData(EventKind.Failure, AttentionReason.Error)]
    public void ForegroundWaitsAreImmediate(EventKind kind, AttentionReason reason)
    {
        Apply(TestWorkspace.Event(kind, start));
        Assert.Equal(reason, Session.Reason);
        Assert.True(Session.RequiresAttention(start));
    }

    [Fact]
    public void ProvisionalPermissionIsNeverAttentionAndConfirmedPermissionIsEligibleOnArrival()
    {
        Apply(TestWorkspace.Event(EventKind.Permission, start));
        Assert.Null(Session.Reason);
        Assert.False(Session.RequiresAttention(start.AddMinutes(10)));
        Assert.Equal(HumanConfirmation.Provisional, Assert.Single(Session.Waits).Confirmation);
        Apply(TestWorkspace.ConfirmedPermission(start.AddSeconds(6)));
        Assert.Equal(AttentionReason.Permission, Session.Reason);
        Assert.Equal(start, Session.Since);
        Assert.True(Session.RequiresAttention(start.AddSeconds(6)));
        Assert.Single(Session.Waits);
    }

    [Fact]
    public void NewPromptCancelsCompletion()
    {
        Apply(TestWorkspace.Event(EventKind.Stop, start));
        Apply(TestWorkspace.Event(EventKind.Prompt, start.AddSeconds(5), turn: "turn-2"));
        Assert.Null(Session.Reason);
        Assert.False(Session.RequiresAttention(start.AddMinutes(1)));
    }

    [Fact]
    public void BackgroundWorkDoesNotHideAQuestion()
    {
        Apply(TestWorkspace.Event(EventKind.BackgroundStart, start) with
        {
            BackgroundId = "agent",
            BackgroundType = "subagent"
        });
        Apply(TestWorkspace.Event(EventKind.Question, start.AddSeconds(1), operation: "question"));
        Assert.Single(Session.Background);
        Assert.True(Session.RequiresAttention(start.AddSeconds(1)));
    }

    [Fact]
    public void ForegroundStopWaitsForSubagentThenStartsGrace()
    {
        Apply(TestWorkspace.Event(EventKind.BackgroundStart, start) with
        {
            BackgroundId = "agent",
            BackgroundType = "subagent"
        });
        Apply(TestWorkspace.Event(EventKind.Stop, start.AddSeconds(1)));
        Assert.Null(Session.Reason);
        Apply(TestWorkspace.Event(EventKind.BackgroundEnd, start.AddSeconds(10)) with
        {
            BackgroundId = "agent"
        });
        Assert.Equal(start.AddSeconds(30), Session.Due);
    }

    [Theory]
    [InlineData("shell")]
    [InlineData("unknown")]
    public void ShellAndUnknownTasksDoNotSuppressCompletion(string type)
    {
        Apply(TestWorkspace.Event(EventKind.Stop, start) with
        {
            BackgroundSnapshot = [new("task", type)]
        });
        Assert.Equal(AttentionReason.Done, Session.Reason);
    }

    [Theory]
    [InlineData("subagent")]
    [InlineData("monitor")]
    [InlineData("workflow")]
    public void KnownTasksSuppressCompletion(string type)
    {
        Apply(TestWorkspace.Event(EventKind.Stop, start) with
        {
            BackgroundSnapshot = [new("task", type)]
        });
        Assert.Null(Session.Reason);
    }

    [Fact]
    public void UnrelatedToolDoesNotResolvePermission()
    {
        Apply(TestWorkspace.ConfirmedPermission(start, "blocked"));
        Apply(TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(1), "other"));
        Assert.Equal(AttentionReason.Permission, Session.Reason);
        Apply(TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(2), "blocked"));
        Assert.Null(Session.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchingOperationResumptionClearsPermissionWithoutResolvingOtherWaits(bool fingerprintOnly)
    {
        var blocked = TestWorkspace.ConfirmedPermission(start, fingerprintOnly ? null : "blocked") with
        {
            OperationFingerprint = "arguments"
        };
        Apply(blocked);
        Apply(TestWorkspace.Event(EventKind.Question, start.AddSeconds(1), "question"));
        Apply(TestWorkspace.Event(EventKind.ToolStart, start.AddSeconds(2), "blocked") with
        {
            OperationFingerprint = "arguments"
        });
        Assert.Equal(AttentionReason.Question, Session.Reason);
        Assert.Single(Session.Waits);
        Assert.Equal(start.AddSeconds(1), Session.Since);
    }

    [Fact]
    public void UnrelatedResumptionWithIdenticalArgumentsCannotClearAnIdentifiedWait()
    {
        Apply(TestWorkspace.ConfirmedPermission(start, "blocked") with { OperationFingerprint = "same" });
        Apply(TestWorkspace.Event(EventKind.ToolStart, start.AddSeconds(1), "other") with { OperationFingerprint = "same" });
        Assert.Equal(AttentionReason.Permission, Session.Reason);
        Apply(TestWorkspace.Event(EventKind.ToolStart, start.AddSeconds(2), "blocked"));
        Assert.Null(Session.Reason);
        Assert.Null(Session.Episode);
    }

    [Fact]
    public void AmbiguousParallelPermissionRequiresEveryCandidateToResumeOrComplete()
    {
        Apply(TestWorkspace.Event(EventKind.ToolStart, start, "one") with { OperationFingerprint = "same" });
        Apply(TestWorkspace.Event(EventKind.ToolStart, start.AddSeconds(1), "two") with { OperationFingerprint = "same" });
        Apply(TestWorkspace.ConfirmedPermission(start.AddSeconds(2)) with { OperationFingerprint = "same" });
        Apply(TestWorkspace.Event(EventKind.ToolStart, start.AddSeconds(3), "one") with { OperationFingerprint = "same" });
        Assert.Equal(AttentionReason.Permission, Session.Reason);
        Apply(TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(4), "two") with { OperationFingerprint = "same" });
        Assert.Null(Session.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BackgroundCompletionAfterInterruptionCannotCreateACompletionAlert(bool stoppedBeforeInterrupt)
    {
        Apply(TestWorkspace.Event(EventKind.BackgroundStart, start) with { BackgroundId = "agent", BackgroundType = "subagent" });
        if (stoppedBeforeInterrupt)
        {
            Apply(TestWorkspace.Event(EventKind.Stop, start.AddSeconds(1)));
        }
        Apply(TestWorkspace.Event(EventKind.Interrupt, start.AddSeconds(2)));
        // Eligibility must survive the snapshot/restart boundary independently of idle activity.
        var restored = System.Text.Json.JsonSerializer.Deserialize<AppState>(System.Text.Json.JsonSerializer.Serialize(state))!;
        SessionReducer.Apply(restored, TestWorkspace.Event(EventKind.BackgroundEnd, start.AddSeconds(3)) with { BackgroundId = "agent" });
        var session = restored.Sessions[Session.Key];
        Assert.Equal("idle", session.Activity);
        Assert.False(session.CompletionPending);
        Assert.Null(session.Reason);
        Assert.False(session.RequiresAttention(start.AddMinutes(1)));
        SessionReducer.Apply(restored, TestWorkspace.Event(EventKind.Prompt, start.AddSeconds(4), turn: "new"));
        SessionReducer.Apply(restored, TestWorkspace.Event(EventKind.Stop, start.AddSeconds(5), turn: "new"));
        Assert.Equal(AttentionReason.Done, session.Reason);
    }

    [Fact]
    public void FingerprintCorrelatesPermissionWithoutToolId()
    {
        Apply(TestWorkspace.ConfirmedPermission(start) with
        {
            OperationFingerprint = "fingerprint"
        });
        Apply(TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(1), "tool-7") with
        {
            OperationFingerprint = "fingerprint"
        });
        Assert.Null(Session.Reason);
    }

    [Fact]
    public void LatePermissionCannotResurrectCompletedOperation()
    {
        Apply(TestWorkspace.Event(EventKind.ToolEnd, start, "tool"));
        Apply(TestWorkspace.ConfirmedPermission(start.AddSeconds(1), "tool"));
        Assert.Null(Session.Reason);
    }

    [Fact]
    public void OldTurnCompletionDoesNotOverwriteNewPrompt()
    {
        Apply(TestWorkspace.Event(EventKind.Prompt, start, turn: "old"));
        Apply(TestWorkspace.Event(EventKind.Prompt, start.AddSeconds(1), turn: "new"));
        Apply(TestWorkspace.Event(EventKind.Stop, start.AddSeconds(2), turn: "old"));
        Assert.Null(Session.Reason);
        Assert.Equal("new", Session.TurnId);
    }

    [Fact]
    public void OldOccurrenceTimestampIsRejected()
    {
        Apply(TestWorkspace.Event(EventKind.Prompt, start.AddSeconds(10)));
        Apply(TestWorkspace.Event(EventKind.Stop, start));
        Assert.Null(Session.Reason);
    }

    [Fact]
    public void AcknowledgmentSurvivesDuplicateWait()
    {
        var e = TestWorkspace.ConfirmedPermission(start, "tool");
        Apply(e);
        Session.AcknowledgedEpisode = Session.Episode;
        Apply(e with
        {
            Id = Guid.NewGuid().ToString("N"),
            At = start.AddSeconds(1)
        });
        Assert.False(Session.RequiresAttention(start.AddSeconds(2)));
        Assert.Equal(start, Session.Since);
    }

    [Fact]
    public void ResolvedThenNewWaitGetsANewEpisode()
    {
        Apply(TestWorkspace.ConfirmedPermission(start, "one"));
        var old = Session.Episode;
        Session.AcknowledgedEpisode = old;
        Apply(TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(1), "one"));
        Apply(TestWorkspace.ConfirmedPermission(start.AddSeconds(2), "two"));
        Assert.NotEqual(old, Session.Episode);
        Assert.True(Session.RequiresAttention(start.AddSeconds(17)));
    }

    [Fact]
    public void ProviderAndSessionNamesDoNotMergeIdentity()
    {
        Apply(TestWorkspace.ConfirmedPermission(start));
        Apply(TestWorkspace.Event(EventKind.Stop, start) with
        {
            Provider = "codex"
        });
        Assert.Equal(2, state.Sessions.Count);
    }

    [Fact]
    public void StaleAttachmentCannotMutateResumedSession()
    {
        Apply(TestWorkspace.Event(EventKind.Register, start));
        Apply(TestWorkspace.Event(EventKind.Register, start.AddSeconds(1)) with
        {
            AttachmentId = "new"
        });
        Apply(TestWorkspace.ConfirmedPermission(start.AddSeconds(2)));
        Assert.Null(Session.Reason);
        Assert.Equal("new", Session.AttachmentId);
    }

    [Fact]
    public void RepeatedEventIsIdempotent()
    {
        var e = TestWorkspace.ConfirmedPermission(start, "tool");
        Apply(e);
        Apply(e);
        Assert.Single(Session.Waits);
        Assert.Single(state.AppliedEvents);
    }

    [Fact]
    public void EndAndInterruptClearAttention()
    {
        Apply(TestWorkspace.ConfirmedPermission(start));
        Apply(TestWorkspace.Event(EventKind.Interrupt, start.AddSeconds(1)));
        Assert.Null(Session.Reason);
        Apply(TestWorkspace.Event(EventKind.End, start.AddSeconds(2)));
        Assert.True(Session.Ended);
    }

    [Fact]
    public void OlderToolResultWithIdenticalArgumentsCannotClearANewTurnWait()
    {
        Apply(TestWorkspace.Event(EventKind.ToolStart, start, "old", turn: null) with
        {
            OperationFingerprint = "same"
        });
        Apply(TestWorkspace.Event(EventKind.Prompt, start.AddSeconds(1), turn: null));
        Apply(TestWorkspace.Event(EventKind.ToolStart, start.AddSeconds(2), "new", turn: null) with
        {
            OperationFingerprint = "same"
        });
        Apply(TestWorkspace.ConfirmedPermission(start.AddSeconds(3), turn: null) with
        {
            OperationFingerprint = "same"
        });
        Apply(TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(4), "old", turn: null) with
        {
            OperationFingerprint = "same"
        });
        Assert.Equal(AttentionReason.Permission, Session.Reason);
        Apply(TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(5), "new", turn: null) with
        {
            OperationFingerprint = "same"
        });
        Assert.Null(Session.Reason);
    }

    [Fact]
    public void IdenticalParallelOperationsResolveOnlyWithCompleteEvidence()
    {
        Apply(TestWorkspace.Event(EventKind.ToolStart, start, "one") with
        {
            OperationFingerprint = "same"
        });
        Apply(TestWorkspace.Event(EventKind.ToolStart, start.AddSeconds(1), "two") with
        {
            OperationFingerprint = "same"
        });
        Apply(TestWorkspace.ConfirmedPermission(start.AddSeconds(2)) with
        {
            OperationFingerprint = "same"
        });
        Apply(TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(3), "one") with
        {
            OperationFingerprint = "same"
        });
        Assert.Equal(AttentionReason.Permission, Session.Reason);
        Apply(TestWorkspace.Event(EventKind.ToolEnd, start.AddSeconds(4), "two") with
        {
            OperationFingerprint = "same"
        });
        Assert.Null(Session.Reason);
    }

    [Fact]
    public void DenialThenTurnStopStartsANewCompletionEpisode()
    {
        Apply(TestWorkspace.ConfirmedPermission(start, "blocked"));
        var old = Session.Episode;
        Session.AcknowledgedEpisode = old;
        Apply(TestWorkspace.Event(EventKind.Stop, start.AddSeconds(1)));
        Assert.NotEqual(old, Session.Episode);
        Assert.True(Session.RequiresAttention(start.AddSeconds(22)));
    }
}
