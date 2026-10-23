using System.Text.Json.Nodes;
using BellTower.IO;

namespace BellTower.Setup;

public sealed class ProviderSettingsEditor(HookCommandBuilder commands)
{
    private static readonly string[] SharedHooks =
        ["SessionStart", "UserPromptSubmit", "PreToolUse", "PostToolUse", "PermissionRequest", "SubagentStart", "SubagentStop", "Stop", "SessionEnd"];

    public string Edit(string provider, string path, string? before, IntegrationOwnership previous,
        IntegrationOwnership ownership, bool remove, bool statusLine, bool migrateEdited = false, List<string>? notes = null)
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
        var retained = new List<OwnedHook>();
        var blocked = RemoveOwnedHooks(hooks, previousCommands.ToHashSet(StringComparer.Ordinal),
            previous.Handlers.GetValueOrDefault(path) ?? [], migrateEdited, retained, notes);
        if (remove)
        {
            if (retained.Count == 0)
            {
                ownership.Commands.Remove(path);
                ownership.Handlers.Remove(path);
            }
            else
            {
                ownership.Handlers[path] = retained;
            }
        }
        else
        {
            AddHooks(hooks, provider, command, blocked, retained, notes);
            ownership.Commands[path] = previousCommands.Append(command).Distinct(StringComparer.Ordinal).ToList();
            ownership.Handlers[path] = retained;
        }
        if (provider == "claude")
        {
            EditStatusLine(document, path, previous, ownership, remove, statusLine);
        }

        return document.ToJsonString(Json.Options);
    }

    private static HashSet<string> RemoveOwnedHooks(JsonObject hooks, HashSet<string> ownedCommands, List<OwnedHook> previous,
        bool migrateEdited, List<OwnedHook> retained, List<string>? notes)
    {
        var blocked = new HashSet<string>(StringComparer.Ordinal);
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
                        var groupMetadata = (JsonObject)group.DeepClone();
                        groupMetadata.Remove("hooks");
                        var expected = previous.FirstOrDefault(owned => owned.Event == pair.Key &&
                            JsonNode.Parse(owned.Handler)?["command"]?.GetValue<string>() == text);
                        // Older manifests stored commands only; the shipped default is known exactly.
                        var legacy = new JsonObject { ["type"] = "command", ["command"] = text, ["timeout"] = 3 };
                        var unedited = expected is null
                            ? JsonNode.DeepEquals(handler, legacy) && JsonNode.DeepEquals(groupMetadata,
                                new JsonObject { ["matcher"] = pair.Key == "Notification" ? "permission_prompt|elicitation_dialog|idle_prompt" : ".*" })
                            : JsonNode.DeepEquals(handler, JsonNode.Parse(expected.Handler)) && JsonNode.DeepEquals(groupMetadata, JsonNode.Parse(expected.Group));
                        if (!unedited && !migrateEdited)
                        {
                            blocked.Add(pair.Key);
                            retained.Add(expected ?? new OwnedHook(pair.Key,
                                new JsonObject { ["matcher"] = pair.Key == "Notification" ? "permission_prompt|elicitation_dialog|idle_prompt" : ".*" }.ToJsonString(),
                                legacy.ToJsonString()));
                            notes?.Add("Edited Bell Tower " + pair.Key + " handler retained. Preview --migrate-edited-hooks to replace it explicitly.");
                            continue;
                        }
                        notes?.Add((unedited ? "Replace owned " : "Explicitly replace edited ") + pair.Key + " hook: " + handler?.ToJsonString());
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
        return blocked;
    }

    private void AddHooks(JsonObject hooks, string provider, string command, HashSet<string> blocked, List<OwnedHook> ownership, List<string>? notes)
    {
        var events = provider == "claude"
            ? SharedHooks.Concat(["PostToolUseFailure", "StopFailure", "Notification"])
            : SharedHooks.Concat(["Interrupt"]);
        foreach (var eventName in events)
        {
            if (blocked.Contains(eventName))
            {
                continue;
            }
            var groups = hooks[eventName] as JsonArray ?? new JsonArray();
            if (hooks[eventName] is null)
            {
                hooks[eventName] = groups;
            }

            var handler = new JsonObject { ["type"] = "command", ["command"] = command, ["timeout"] = 3 };
            if (commands.Fast && eventName != "SessionEnd")
            {
                handler["async"] = true;
            }
            if (commands.Fast && provider == "claude")
            {
                handler["shell"] = "bash";
            }
            var group = new JsonObject
            {
                ["matcher"] = eventName == "Notification" ? "permission_prompt|elicitation_dialog|idle_prompt" : ".*",
                ["hooks"] = new JsonArray(handler)
            };
            groups.Add(group);
            var metadata = (JsonObject)group.DeepClone();
            metadata.Remove("hooks");
            ownership.Add(new(eventName, metadata.ToJsonString(), handler.ToJsonString()));
            notes?.Add("Install " + eventName + " hook: " + handler.ToJsonString());
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
