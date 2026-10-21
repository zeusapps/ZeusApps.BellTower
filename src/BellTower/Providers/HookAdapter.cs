using System.Text.Json;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Providers;

public static class HookAdapter
{
    public static Observation? Parse(string provider, JsonElement input, Attachment attachment, DateTimeOffset? at = null)
    {
        if (provider is not ("claude" or "codex"))
        {
            throw new ArgumentException("Provider must be claude or codex.");
        }

        var session = HookInput.Text(input, "session_id") ?? throw new InvalidDataException("Session ID missing.");
        var hook = HookInput.Text(input, "hook_event_name") ?? throw new InvalidDataException("Hook event missing.");
        var tool = HookInput.Text(input, "tool_name");
        var notification = HookInput.Text(input, "notification_type");
        var question = tool is "AskUserQuestion" or "request_user_input" or "request_user_input_async";
        EventKind? kind = hook switch
        {
            "SessionStart" => EventKind.Register,
            "UserPromptSubmit" => EventKind.Prompt,
            "PreToolUse" => question ? EventKind.Question : EventKind.ToolStart,
            "PostToolUse" or "PostToolUseFailure" or "PermissionDenied" => EventKind.ToolEnd,
            "PermissionRequest" => EventKind.Permission,
            "Stop" => EventKind.Stop,
            "StopFailure" => EventKind.Failure,
            "Interrupt" => EventKind.Interrupt,
            "SubagentStart" => EventKind.BackgroundStart,
            "SubagentStop" => EventKind.BackgroundEnd,
            "SessionEnd" => EventKind.End,
            "Notification" => notification switch
            {
                "permission_prompt" => EventKind.Permission,
                "elicitation_dialog" => EventKind.Question,
                "idle_prompt" => EventKind.Stop,
                _ => null
            },
            _ => null
        };
        if (kind is null)
        {
            return null;
        }

        string? fingerprint = null;
        if (tool is not null && input.TryGetProperty("tool_input", out var arguments))
        {
            // Approval descriptions and execution options can differ between hook stages.
            // Codex exposes the command itself on all Bash/apply_patch stages.
            var correlationInput = tool is "Bash" or "apply_patch" && arguments.ValueKind == JsonValueKind.Object &&
                arguments.TryGetProperty("command", out var command) ? command : arguments;
            fingerprint = ContentHash.Compute(tool + "\n" + Canonical(correlationInput));
        }

        List<BackgroundTask>? background = null;
        if (input.TryGetProperty("background_tasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array)
        {
            background = tasks.EnumerateArray()
                .Select(task => new BackgroundTask(HookInput.Text(task, "id") ?? "", HookInput.Text(task, "type") ?? "unknown"))
                .Where(task => task.Id.Length > 0).ToList();
        }

        var agent = HookInput.Text(input, "agent_id");
        return new Observation
        {
            Provider = provider,
            SessionId = session,
            AttachmentId = attachment.Id,
            Kind = kind.Value,
            At = at ?? DateTimeOffset.UtcNow,
            Directory = HookInput.Text(input, "cwd") ?? attachment.Directory,
            TurnId = HookInput.Text(input, "turn_id"),
            OperationId = HookInput.Text(input, "tool_use_id"),
            OperationFingerprint = fingerprint,
            BackgroundId = agent,
            BackgroundType = kind == EventKind.BackgroundStart ? "subagent" : null,
            BackgroundSnapshot = background,
            IsMainThread = agent is null || hook is "SubagentStart" or "SubagentStop"
        };
    }

    private static string Canonical(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => JsonSerializer.Serialize(property.Name) + ":" + Canonical(property.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", value.EnumerateArray().Select(Canonical)) + "]",
        _ => value.GetRawText()
    };
}
