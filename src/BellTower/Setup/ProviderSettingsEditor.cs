using System.Text.Json.Nodes;
using BellTower.IO;

namespace BellTower.Setup;

public sealed class ProviderSettingsEditor(HookCommandBuilder commands)
{
    private static readonly string[] SharedHooks =
        ["SessionStart", "UserPromptSubmit", "PreToolUse", "PostToolUse", "PermissionRequest", "SubagentStart", "SubagentStop", "Stop", "SessionEnd"];

    public string Edit(string provider, string path, string? before, IntegrationOwnership previous,
        IntegrationOwnership ownership, bool remove, bool statusLine)
    {
        var document = before is null ? new JsonObject() : JsonNode.Parse(before)?.AsObject()
            ?? throw new InvalidDataException("Invalid client settings.");
        var hooks = document["hooks"] as JsonObject;
        if (document["hooks"] is not null && hooks is null)
        {
            throw new InvalidDataException("Existing hooks are malformed.");
        }

        hooks ??= new JsonObject();
        document["hooks"] = hooks;
        var command = commands.Build("event", provider);
        var previousCommands = previous.Commands.GetValueOrDefault(path) ?? [];
        RemoveOwnedHooks(hooks, previousCommands.Append(command).ToHashSet(StringComparer.Ordinal));
        if (remove)
        {
            ownership.Commands.Remove(path);
        }
        else
        {
            AddHooks(hooks, provider, command);
            ownership.Commands[path] = previousCommands.Append(command).Distinct(StringComparer.Ordinal).ToList();
        }
        if (provider == "claude")
        {
            EditStatusLine(document, path, previous, ownership, remove, statusLine);
        }

        return document.ToJsonString(Json.Options);
    }

    private static void RemoveOwnedHooks(JsonObject hooks, HashSet<string> ownedCommands)
    {
        foreach (var pair in hooks.ToArray())
        {
            if (pair.Value is not JsonArray groups)
            {
                throw new InvalidDataException("Existing hook groups are malformed.");
            }

            foreach (var node in groups.ToArray())
            {
                if (node is not JsonObject group || group["hooks"] is not JsonArray handlers)
                {
                    throw new InvalidDataException("Existing hook handlers are malformed.");
                }

                foreach (var handler in handlers.ToArray())
                {
                    var text = handler?["command"]?.GetValue<string>();
                    if (text is not null && ownedCommands.Contains(text))
                    {
                        handlers.Remove(handler);
                    }
                }
                if (handlers.Count == 0)
                {
                    groups.Remove(node);
                }
            }
            if (groups.Count == 0)
            {
                hooks.Remove(pair.Key);
            }
        }
    }

    private static void AddHooks(JsonObject hooks, string provider, string command)
    {
        var events = provider == "claude"
            ? SharedHooks.Concat(["PostToolUseFailure", "StopFailure", "Notification"])
            : SharedHooks.Concat(["Interrupt"]);
        foreach (var eventName in events)
        {
            var groups = hooks[eventName] as JsonArray ?? new JsonArray();
            if (hooks[eventName] is null)
            {
                hooks[eventName] = groups;
            }

            groups.Add(new JsonObject
            {
                ["matcher"] = eventName == "Notification" ? "permission_prompt|elicitation_dialog|idle_prompt" : ".*",
                ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = command, ["timeout"] = 3 })
            });
        }
    }

    private void EditStatusLine(JsonObject document, string path, IntegrationOwnership previous,
        IntegrationOwnership ownership, bool remove, bool enable)
    {
        var existing = document["statusLine"]?["command"]?.GetValue<string>();
        if (existing is not null && previous.StatusLines.GetValueOrDefault(path) == existing && (remove || enable))
        {
            document.Remove("statusLine");
            ownership.StatusLines.Remove(path);
            existing = null;
        }
        if (remove || !enable)
        {
            return;
        }

        var badge = commands.Build("statusline", "claude");
        if (existing is not null && existing != badge)
        {
            throw new InvalidOperationException("An unowned Claude status line already exists. Keep it or remove it explicitly before enabling Bell Tower's badge.");
        }

        document["statusLine"] = new JsonObject { ["type"] = "command", ["command"] = badge, ["refreshInterval"] = 5 };
        ownership.StatusLines[path] = badge;
    }
}
