using System.Text.Json;
using BellTower.Core;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Core;

// Question-tool approval flows stay question waits (field-observed Question -> Permission flip). All sequences are redacted
// fixtures derived from isolated provider captures (Claude 2.1.310, Codex 0.164.0), not live sessions.
public sealed class QuestionToolApprovalTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UtcNow;

    private static Observation Hook(string provider, string json, double seconds, string session = "session-fixture")
    {
        using var document = JsonDocument.Parse(json);
        var attachment = new Attachment("attachment-" + provider, provider, null, new(), "D:/example", Start);
        return HookAdapter.Parse(provider, document.RootElement, attachment, Start.AddSeconds(seconds))! with { SessionId = session };
    }

    private static string Ask(string hook, string? id = "toolu_1") =>
        "{\"session_id\":\"session-fixture\",\"hook_event_name\":\"" + hook + "\",\"tool_name\":\"AskUserQuestion\"," +
        (id is null ? "" : "\"tool_use_id\":\"" + id + "\",") + "\"tool_input\":{\"questions\":[{\"question\":\"Fixture question?\"}]}}";

    private const string Prompt = "{\"session_id\":\"session-fixture\",\"hook_event_name\":\"Notification\",\"notification_type\":\"permission_prompt\",\"prompt_id\":\"prompt-fixture\"}";

    private static string Bash(string hook, string? id = null) =>
        "{\"session_id\":\"session-fixture\",\"hook_event_name\":\"" + hook + "\",\"tool_name\":\"Bash\"," +
        (id is null ? "" : "\"tool_use_id\":\"" + id + "\",") + "\"tool_input\":{\"command\":\"example\"}}";

    [Fact]
    public void ClaudeQuestionDialogApprovalFlowNeverFlipsTheQuestionToPermission()
    {
        using var sequence = HookSequence.Load("claude-askuserquestion-auto.json");
        var state = new AppState();
        var policy = new AlertPolicy(Start.AddSeconds(-1));
        var announcements = 0;
        for (var index = 0; index < sequence.Events.Count; index++)
        {
            var observation = sequence.Parse(index, Start);
            SessionReducer.Apply(state, observation);
            var session = state.Sessions[observation.Key];
            if (index is >= 0 and <= 2)
            {
                Assert.Equal(AttentionReason.Question, session.Reason);
                Assert.DoesNotContain(session.Waits, wait => wait.Reason == AttentionReason.Permission);
                Assert.Single(session.Waits);
            }
            if (index == 3)
            {
                Assert.DoesNotContain(session.Waits, wait => wait.Reason is AttentionReason.Question or AttentionReason.Permission);
            }
            announcements += policy.Collect([session], observation.At.AddSeconds(1), false)
                .Count(item => item.Reason == AttentionReason.Question);
            Assert.DoesNotContain(policy.Collect([session], observation.At.AddSeconds(30), false), item => item.Reason == AttentionReason.Permission);
        }
        Assert.Equal(1, announcements);
    }

    [Fact]
    public void TheQuestionEpisodeIsSingleAndItsDismissalSurvivesTheApprovalFlow()
    {
        using var sequence = HookSequence.Load("claude-askuserquestion-auto.json");
        var state = new AppState();
        SessionReducer.Apply(state, sequence.Parse(0, Start));
        var session = state.Sessions.Values.Single();
        var episode = session.Episode;
        session.AcknowledgedEpisode = episode;
        session.ViewedEpisode = episode;
        SessionReducer.Apply(state, sequence.Parse(1, Start));
        SessionReducer.Apply(state, sequence.Parse(2, Start));
        Assert.Equal(episode, session.Episode);
        Assert.Equal(Start, session.EpisodeStarted);
        Assert.Equal(episode, session.AcknowledgedEpisode);
        Assert.False(session.RequiresAttention(Start.AddMinutes(1)));
        var wait = Assert.Single(session.Waits);
        Assert.Equal(AttentionReason.Question, wait.Reason);
        Assert.Equal(HumanConfirmation.Confirmed, wait.Confirmation);
        Assert.Equal("toolu_fixture_ask_1", wait.OperationId);
    }

    [Fact]
    public void RestartKeepsTheQuestionAndRestoresItSilently()
    {
        using var sequence = HookSequence.Load("claude-askuserquestion-auto.json");
        var state = new AppState();
        for (var index = 0; index < 3; index++)
        {
            SessionReducer.Apply(state, sequence.Parse(index, Start.AddMinutes(-1)));
        }
        var restored = JsonSerializer.Deserialize<AppState>(JsonSerializer.Serialize(state))!.Sessions.Values.Single();
        Assert.Equal(AttentionReason.Question, restored.Reason);
        Assert.True(restored.RequiresAttention(Start));
        Assert.Empty(new AlertPolicy(Start).Collect([restored], Start, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApprovalFlowWithoutOrBeforeTheToolStartStillYieldsOneQuestionWait(bool toolStartFirst)
    {
        var state = new AppState();
        var request = Hook("claude", Ask("PermissionRequest", null), 0.1);
        var start = Hook("claude", Ask("PreToolUse"), 0.2);
        var order = toolStartFirst ? new[] { start, request } : new[] { request, start };
        foreach (var observation in order)
        {
            SessionReducer.Apply(state, observation);
        }
        var session = state.Sessions.Values.Single();
        var wait = Assert.Single(session.Waits);
        Assert.Equal(AttentionReason.Question, wait.Reason);
        Assert.Equal("toolu_1", wait.OperationId);
        SessionReducer.Apply(state, Hook("claude", Ask("PostToolUse"), 5));
        Assert.Empty(session.Waits);
        Assert.Null(session.Reason);
    }

    [Fact]
    public void AQuestionApprovalFlowAfterTheQuestionResolvedDoesNotRecreateIt()
    {
        var state = new AppState();
        SessionReducer.Apply(state, Hook("claude", Ask("PreToolUse"), 0));
        SessionReducer.Apply(state, Hook("claude", Ask("PostToolUse"), 1));
        SessionReducer.Apply(state, Hook("claude", Ask("PermissionRequest", null), 2));
        SessionReducer.Apply(state, Hook("claude", Prompt, 7));
        Assert.Empty(state.Sessions.Values.Single().Waits);
    }

    [Fact]
    public void ACodexPlanModeQuestionIsOneEpisodeClearedByTheCorrelatedResult()
    {
        using var sequence = HookSequence.Load("codex-request-user-input-plan.json");
        var state = new AppState();
        var policy = new AlertPolicy(Start.AddSeconds(-1));
        SessionReducer.Apply(state, sequence.Parse(0, Start));
        var session = state.Sessions.Values.Single();
        Assert.Equal(AttentionReason.Question, session.Reason);
        Assert.Single(policy.Collect([session], Start.AddSeconds(1), false));
        Assert.Empty(policy.Collect([session], Start.AddSeconds(16), false));
        Assert.DoesNotContain(session.Waits, wait => wait.Reason == AttentionReason.Permission);
        SessionReducer.Apply(state, sequence.Parse(1, Start));
        Assert.Empty(session.Waits);
        SessionReducer.Apply(state, sequence.Parse(2, Start));
        Assert.Equal(AttentionReason.Done, session.Reason);
    }

    [Fact]
    public void ARejectedCodexDefaultModeQuestionClearsAtStopWithNoResidual()
    {
        using var sequence = HookSequence.Load("codex-request-user-input-default-rejected.json");
        var state = new AppState();
        SessionReducer.Apply(state, sequence.Parse(0, Start));
        var session = state.Sessions.Values.Single();
        Assert.Equal(AttentionReason.Question, session.Reason);
        SessionReducer.Apply(state, sequence.Parse(1, Start));
        Assert.DoesNotContain(session.Waits, wait => wait.Reason is AttentionReason.Question or AttentionReason.Permission);
        Assert.Equal(AttentionReason.Done, session.Reason);
        // Nothing question-related is restored, reminded or escalated after a tray restart.
        var restored = JsonSerializer.Deserialize<AppState>(JsonSerializer.Serialize(state))!.Sessions.Values.Single();
        Assert.DoesNotContain(restored.Waits, wait => wait.Reason is AttentionReason.Question or AttentionReason.Permission);
        var policy = new AlertPolicy(Start.AddSeconds(-1));
        Assert.DoesNotContain(policy.Collect([restored], Start.AddMinutes(5), false), item => item.Reason == AttentionReason.Question);
        // A late Codex hook cannot bring the question back.
        SessionReducer.Apply(state, Hook("codex", Ask("PermissionRequest", null), 1));
        Assert.DoesNotContain(session.Waits, wait => wait.Reason is AttentionReason.Question or AttentionReason.Permission);
    }

    [Fact]
    public void ACodexQuestionApprovalHookWouldBehaveLikeClaudesAndStayAQuestion()
    {
        var state = new AppState();
        SessionReducer.Apply(state, Hook("codex", Ask("PreToolUse").Replace("AskUserQuestion", "request_user_input"), 0));
        SessionReducer.Apply(state, Hook("codex", Ask("PermissionRequest", null).Replace("AskUserQuestion", "request_user_input"), 0.1));
        var session = state.Sessions.Values.Single();
        Assert.Equal(AttentionReason.Question, session.Reason);
        Assert.Single(session.Waits);
    }

    [Fact]
    public void AQuestionAndAnUncorrelatedPermissionPendingTogetherMakeAnUnattributedPromptAmbiguous()
    {
        var state = new AppState();
        var policy = new AlertPolicy(Start.AddSeconds(-1));
        SessionReducer.Apply(state, Hook("claude", Bash("PreToolUse", "bash-1"), 0));
        SessionReducer.Apply(state, Hook("claude", Bash("PermissionRequest"), 0.1));
        SessionReducer.Apply(state, Hook("claude", Ask("PreToolUse"), 1));
        SessionReducer.Apply(state, Hook("claude", Prompt, 6.5));
        var session = state.Sessions.Values.Single();
        Assert.Equal(AttentionReason.Question, session.Reason);
        Assert.Equal(2, session.Waits.Count);
        Assert.All(session.Waits, wait => Assert.True(wait.Ambiguous));
        var question = session.Waits.Single(wait => wait.Reason == AttentionReason.Question);
        var permission = session.Waits.Single(wait => wait.Reason == AttentionReason.Permission);
        Assert.NotEqual(HumanConfirmation.Confirmed, question.Confirmation);
        Assert.NotEqual(HumanConfirmation.Confirmed, permission.Confirmation);
        Assert.Single(policy.Collect([session], Start.AddSeconds(7), false), item => item.Reason == AttentionReason.Question);
        // No reassignment: resolving the question leaves the permission provisional, and resolving the permission leaves the question.
        SessionReducer.Apply(state, Hook("claude", Bash("PostToolUse", "bash-1"), 8));
        Assert.Equal(AttentionReason.Question, session.Reason);
        Assert.Equal("toolu_1", Assert.Single(session.Waits).OperationId);
        Assert.Empty(policy.Collect([session], Start.AddSeconds(30), false));
    }

    [Fact]
    public void ResolvingTheQuestionNeverCreditsTheHeldPromptToThePermissionByElimination()
    {
        // ANALYSIS-100 / task 2A.6: the question's own dialog emits the prompt; the permission relies on its own later prompt.
        var state = new AppState();
        SessionReducer.Apply(state, Hook("claude", Bash("PermissionRequest"), 0));
        SessionReducer.Apply(state, Hook("claude", Ask("PreToolUse"), 1));
        SessionReducer.Apply(state, Hook("claude", Prompt, 6.5));
        SessionReducer.Apply(state, Hook("claude", Ask("PostToolUse"), 8));
        var session = state.Sessions.Values.Single();
        var wait = Assert.Single(session.Waits);
        Assert.Equal(AttentionReason.Permission, wait.Reason);
        Assert.NotEqual(HumanConfirmation.Confirmed, wait.Confirmation);
        Assert.Null(session.Reason);
        SessionReducer.Apply(state, Hook("claude", Prompt, 20));
        Assert.Equal(AttentionReason.Permission, session.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SequentialQuestionThenBashFixtureKeepsOneQuestionEpisodeAndTracksBashFromItsOwnStart(bool ownPrompt)
    {
        using var sequence = HookSequence.Load("claude-question-then-bash.json");
        var state = new AppState();
        var policy = new AlertPolicy(Start.AddSeconds(-1));
        var questionAnnouncements = 0;
        var questionEpisodes = new HashSet<string?>();
        for (var index = 0; index < sequence.Events.Count; index++)
        {
            var observation = sequence.Parse(index, Start);
            SessionReducer.Apply(state, observation);
            var session = state.Sessions[observation.Key];
            if (session.Reason == AttentionReason.Question)
            {
                questionEpisodes.Add(session.Episode);
            }
            questionAnnouncements += policy.Collect([session], observation.At.AddSeconds(1), false).Count(item => item.Reason == AttentionReason.Question);
            if (index <= 4)
            {
                Assert.DoesNotContain(session.Waits, wait => wait.Reason == AttentionReason.Permission);
            }
            if (index == 2)
            {
                Assert.Equal(HumanConfirmation.Confirmed, session.Waits.Single(wait => wait.Reason == AttentionReason.Question).Confirmation);
            }
            if (index == 5)
            {
                // Bash is tracked only from its own approval-flow start and is not attention.
                var bash = Assert.Single(session.Waits);
                Assert.Equal(AttentionReason.Permission, bash.Reason);
                Assert.Equal(observation.At, bash.Since);
                Assert.NotEqual(HumanConfirmation.Confirmed, bash.Confirmation);
                Assert.Null(session.Reason);
                Assert.Empty(policy.Collect([session], observation.At.AddSeconds(30), false));
                if (ownPrompt)
                {
                    SessionReducer.Apply(state, Hook("claude", Prompt, 36.727 + 6.2));
                    Assert.Equal(AttentionReason.Permission, session.Reason);
                    Assert.Equal(observation.At, Assert.Single(session.Waits).Since);
                    Assert.Equal(AttentionReason.Permission, Assert.Single(policy.Collect([session], Start.AddSeconds(36.727 + 6.2), false)).Reason);
                    // Resolve through the fixture's remaining events.
                    SessionReducer.Apply(state, Hook("claude", Bash("PostToolUse"), 50));
                }
            }
        }
        Assert.Single(questionEpisodes);
        Assert.Equal(1, questionAnnouncements);
        Assert.DoesNotContain(state.Sessions.Values.Single().Waits, wait => wait.Reason == AttentionReason.Permission);
    }

    [Fact]
    public void ClaudeQuestionAndCodexPermissionPendingTogetherKeepTheirKindsAndEpisodes()
    {
        var state = new AppState();
        var policy = new AlertPolicy(Start.AddSeconds(-1));
        SessionReducer.Apply(state, Hook("claude", Ask("PreToolUse"), 0, "claude-session"));
        SessionReducer.Apply(state, Hook("claude", Ask("PermissionRequest", null), 0.1, "claude-session"));
        SessionReducer.Apply(state, Hook("codex", Bash("PermissionRequest"), 1, "codex-session"));
        SessionReducer.Apply(state, Hook("claude", Prompt, 6.5, "claude-session"));
        var claude = state.Sessions["claude:claude-session"];
        var codex = state.Sessions["codex:codex-session"];
        Assert.Equal(AttentionReason.Question, claude.Reason);
        Assert.Null(codex.Reason);
        codex.NativePermissionPending = true;
        codex.NativePermissionSince = Start.AddSeconds(2);
        Assert.Equal(AttentionReason.Permission, codex.Reason);
        Assert.NotEqual(claude.Episode, codex.Episode);
        var announced = policy.Collect([claude, codex], Start.AddSeconds(8), false);
        Assert.Equal(2, announced.Count);
        Assert.Equal(AttentionReason.Question, announced.Single(item => item.Provider == "claude").Reason);
        Assert.Equal(AttentionReason.Permission, announced.Single(item => item.Provider == "codex").Reason);
        // The Claude question is dismissed independently of the Codex permission.
        claude.AcknowledgedEpisode = claude.Episode;
        Assert.False(claude.RequiresAttention(Start.AddSeconds(9)));
        Assert.True(codex.RequiresAttention(Start.AddSeconds(9)));
    }
}
