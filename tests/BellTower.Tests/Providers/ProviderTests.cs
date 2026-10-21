using System.Text.Json;
using BellTower.Core;
using BellTower.Providers;
using Xunit;

namespace BellTower.Tests.Providers;

public sealed class ProviderTests
{
    private static readonly Attachment Attachment = new("attachment", "claude", null, new(), "D:/test", DateTimeOffset.UtcNow);
    [Theory]
    [InlineData("claude", "SessionStart", EventKind.Register)]
    [InlineData("codex", "SessionStart", EventKind.Register)]
    [InlineData("claude", "UserPromptSubmit", EventKind.Prompt)]
    [InlineData("codex", "UserPromptSubmit", EventKind.Prompt)]
    [InlineData("claude", "PreToolUse", EventKind.ToolStart)]
    [InlineData("codex", "PreToolUse", EventKind.ToolStart)]
    [InlineData("claude", "PostToolUse", EventKind.ToolEnd)]
    [InlineData("codex", "PostToolUse", EventKind.ToolEnd)]
    [InlineData("claude", "PostToolUseFailure", EventKind.ToolEnd)]
    [InlineData("codex", "PermissionRequest", EventKind.Permission)]
    [InlineData("claude", "Stop", EventKind.Stop)]
    [InlineData("codex", "Stop", EventKind.Stop)]
    [InlineData("claude", "StopFailure", EventKind.Failure)]
    [InlineData("codex", "Interrupt", EventKind.Interrupt)]
    [InlineData("claude", "SubagentStart", EventKind.BackgroundStart)]
    [InlineData("codex", "SubagentStop", EventKind.BackgroundEnd)]
    [InlineData("claude", "SessionEnd", EventKind.End)]
    [InlineData("codex", "SessionEnd", EventKind.End)]
    public void DocumentedContractsNormalize(string provider, string hook, EventKind expected)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            session_id = "test",
            hook_event_name = hook,
            cwd = "D:/test"
        }));
        Assert.Equal(expected, HookAdapter.Parse(provider, json.RootElement, Attachment)!.Kind);
    }

    [Theory]
    [InlineData("AskUserQuestion")]
    [InlineData("request_user_input")]
    [InlineData("request_user_input_async")]
    public void QuestionToolBeginsAnImmediateQuestionWait(string tool)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            session_id = "test",
            hook_event_name = "PreToolUse",
            tool_name = tool
        }));
        Assert.Equal(EventKind.Question, HookAdapter.Parse("codex", json.RootElement, Attachment)!.Kind);
    }

    [Fact]
    public void ClaudeBackgroundSnapshotExcludesDescriptionsAndShellCommands()
    {
        using var json = JsonDocument.Parse("""
            {"session_id":"test","hook_event_name":"Stop","prompt":"secret","background_tasks":[{"id":"agent","type":"subagent","description":"secret"},{"id":"shell","type":"shell","command":"secret"}]}
            """);
        var e = HookAdapter.Parse("claude", json.RootElement, Attachment)!;
        Assert.Equal(2, e.BackgroundSnapshot!.Count);
        Assert.DoesNotContain("secret", BellTower.IO.Json.Encode(e));
    }

    [Fact]
    public void PermissionAndToolResultFingerprintUsesCanonicalInput()
    {
        using var before = JsonDocument.Parse("""{"session_id":"test","hook_event_name":"PermissionRequest","tool_name":"Bash","tool_input":{"b":2,"a":"secret"}}""");
        using var after = JsonDocument.Parse("""{"session_id":"test","hook_event_name":"PostToolUse","tool_name":"Bash","tool_use_id":"tool","tool_input":{"a":"secret","b":2},"tool_response":"secret"}""");
        var e = HookAdapter.Parse("codex", before.RootElement, Attachment)!;
        var result = HookAdapter.Parse("codex", after.RootElement, Attachment)!;
        Assert.Equal(e.OperationFingerprint, result.OperationFingerprint);
        Assert.DoesNotContain("secret", BellTower.IO.Json.Encode(e));
    }

    [Theory]
    [InlineData("Bash")]
    [InlineData("apply_patch")]
    public void CommandCorrelationIgnoresApprovalDescriptionsAndExecutionOptions(string tool)
    {
        using var before = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            session_id = "test",
            hook_event_name = "PreToolUse",
            tool_name = tool,
            tool_input = new { command = "example operation", timeout_ms = 1000 }
        }));
        using var permission = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            session_id = "test",
            hook_event_name = "PermissionRequest",
            tool_name = tool,
            tool_input = new { command = "example operation", description = "Approval-only explanation" }
        }));
        using var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            session_id = "test",
            hook_event_name = "PostToolUse",
            tool_name = tool,
            tool_input = new { command = "example operation" }
        }));
        var expected = HookAdapter.Parse("codex", before.RootElement, Attachment)!.OperationFingerprint;
        Assert.Equal(expected, HookAdapter.Parse("codex", permission.RootElement, Attachment)!.OperationFingerprint);
        Assert.Equal(expected, HookAdapter.Parse("codex", result.RootElement, Attachment)!.OperationFingerprint);
    }

    [Fact]
    public void ArbitraryToolDescriptionsRemainPartOfTheArguments()
    {
        using var first = JsonDocument.Parse("""{"session_id":"test","hook_event_name":"PermissionRequest","tool_name":"mcp__example__create","tool_input":{"description":"one"}}""");
        using var second = JsonDocument.Parse("""{"session_id":"test","hook_event_name":"PermissionRequest","tool_name":"mcp__example__create","tool_input":{"description":"two"}}""");
        Assert.NotEqual(HookAdapter.Parse("codex", first.RootElement, Attachment)!.OperationFingerprint,
            HookAdapter.Parse("codex", second.RootElement, Attachment)!.OperationFingerprint);
    }

    [Fact]
    public void NotificationReasonsAndUnknownEventsAreExplicit()
    {
        using var json = JsonDocument.Parse("""{"session_id":"test","hook_event_name":"Notification","notification_type":"elicitation_dialog"}""");
        Assert.Equal(EventKind.Question, HookAdapter.Parse("claude", json.RootElement, Attachment)!.Kind);
        using var unknown = JsonDocument.Parse("""{"session_id":"test","hook_event_name":"FutureHook"}""");
        Assert.Null(HookAdapter.Parse("codex", unknown.RootElement, Attachment));
    }

    [Fact]
    public void OversizedInputIsRejectedWithoutReadingIndefinitely() => Assert.Throws<InvalidDataException>(() => HookInput.ReadBounded(new StringReader(new string('x', 100)), 50));
    [Fact]
    public void StatusLineUsesSharedAccentAndSanitizesTitle()
    {
        var identity = new SessionIdentity
        {
            Key = "x",
            ManualName = "task\u001b\nname",
            Palette = new("#102030", "#4080c0", "#ffffff")
        };
        var text = StatusLine.Render(identity, new("feature/test", "worktree"));
        Assert.Contains("48;2;64;128;192m", text);
        Assert.Contains("taskname", text);
        Assert.Contains("worktree: worktree", text);
        Assert.DoesNotContain("\n", text);
    }
}
