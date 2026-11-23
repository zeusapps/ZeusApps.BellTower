using System.Text.Json.Nodes;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace BellTower.Setup;

/// <summary>What setup changed to stop a provider writing its own terminal title (design decision 11): the previous value of the one key, and whether setup created its container.</summary>
public sealed record OwnedProviderTitle(string Provider, string? Previous, bool ContainerCreated);

/// <summary>
/// The opt-in step that disables a provider's own terminal title, so Bell Tower's native title can persist. Each edit changes one key
/// and nothing else: Claude Code <c>CLAUDE_CODE_DISABLE_TERMINAL_TITLE=1</c> in the settings <c>env</c> block, Codex
/// <c>[tui] terminal_title = []</c>. Both were probed (2026-11-22, isolated provider homes, a pseudo-console): without the setting Codex 0.171.0
/// writes the title at start, about 20 times a second during a turn and at completion and Claude Code 2.1.330 writes its own title; with it
/// neither wrote a title after the shell's. The setting applies to the whole provider profile, and a provider whose setting is not verified
/// is not offered.
/// </summary>
public static class ProviderTitleSettings
{
    public const string ClaudeKey = "CLAUDE_CODE_DISABLE_TERMINAL_TITLE";
    public const string ClaudeValue = "1";
    public const string CodexKey = "terminal_title";
    public const string CodexValue = "[]";

    /// <summary>Providers whose setting is verified to stop the provider's title writes. Setup offers the step only for these.</summary>
    public static readonly IReadOnlySet<string> Verified = new HashSet<string>(StringComparer.Ordinal) { "claude", "codex" };

    public const string Consent =
        "Provider title step: stops the provider writing its own terminal title for every session of that provider profile (not only Bell Tower's), " +
        "so Bell Tower's session title can persist and Windows Terminal tab navigation can use it. Remove it with --provider-titles --remove.";

    /// <summary>Plans the claude edit. Returns the new text, or <paramref name="before"/> when nothing changes.</summary>
    public static string? EditClaude(string path, string? before, Dictionary<string, OwnedProviderTitle> previous,
        Dictionary<string, OwnedProviderTitle> ownership, bool remove, List<string> notes)
    {
        var file = new JsonSettingsFile(path, before);
        if (file.Root["env"] is not null and not JsonObject)
        {
            throw new InvalidDataException("Claude settings env must be an object.");
        }
        var env = file.Root["env"] as JsonObject;
        var current = env?[ClaudeKey]?.ToString();
        var owned = previous.GetValueOrDefault(path);
        if (remove)
        {
            ownership.Remove(path);
            if (owned is null)
            {
                return before;
            }
            if (current != ClaudeValue)
            {
                notes.Add("Kept user-edited Claude title setting: " + path);
                return before;
            }
            var restored = (JsonObject)env!.DeepClone();
            restored.Remove(ClaudeKey);
            if (owned.Previous is not null)
            {
                restored[ClaudeKey] = owned.Previous;
            }
            file.Set("env", restored.Count == 0 && owned.ContainerCreated ? null : restored);
            return Result(file, before);
        }
        if (current == ClaudeValue)
        {
            if (owned is not null)
            {
                ownership[path] = owned;
            }
            else
            {
                notes.Add("Claude Code already has its own terminal title disabled in " + path + "; left unowned and counted: Bell Tower titles sessions started after the setting took effect.");
            }
            return before;
        }
        var updated = env is null ? new JsonObject() : (JsonObject)env.DeepClone();
        updated[ClaudeKey] = ClaudeValue;
        file.Set("env", updated);
        ownership[path] = new("claude", owned?.Previous ?? current, owned?.ContainerCreated ?? env is null);
        return Result(file, before);
    }

    /// <summary>Plans the codex edit for <c>config.toml</c>. Returns the new text, or <paramref name="before"/> when nothing changes.</summary>
    public static string? EditCodex(string path, string? before, Dictionary<string, OwnedProviderTitle> previous,
        Dictionary<string, OwnedProviderTitle> ownership, bool remove, List<string> notes)
    {
        var text = before ?? "";
        var document = SyntaxParser.Parse(text);
        if (document.HasErrors)
        {
            throw new InvalidDataException("Codex configuration contains invalid TOML.");
        }
        var model = TomlSerializer.Deserialize<TomlTable>(text)!;
        if (model.TryGetValue("tui", out var rawTui) && rawTui is not TomlTable)
        {
            throw new InvalidDataException("Codex tui configuration must be a table.");
        }
        var entry = CodexFooterSettings.Find(document, CodexKey);
        var existing = entry?.Value is { } value ? CodexFooterSettings.Slice(text, value) : null;
        var owned = previous.GetValueOrDefault(path);
        if (remove)
        {
            ownership.Remove(path);
            if (owned is null || entry is null)
            {
                return before;
            }
            if (Compact(existing) != CodexValue)
            {
                notes.Add("Kept user-edited Codex title setting: " + path);
                return before;
            }
            if (owned.Previous is not null)
            {
                return CodexFooterSettings.Replace(text, entry.Value!, owned.Previous);
            }
            RemoveKey(document, entry);
            var removed = document.ToString();
            // Drop the empty [tui] table setup created, so removal leaves the file as it was.
            return owned.ContainerCreated ? RemoveEmptyTuiTable(removed) : removed;
        }
        if (Compact(existing) == CodexValue)
        {
            if (owned is not null)
            {
                ownership[path] = owned;
            }
            else
            {
                notes.Add("Codex already has its own terminal title disabled in " + path + "; left unowned and counted: Bell Tower titles sessions started after the setting took effect.");
            }
            return before;
        }
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var hasTui = rawTui is TomlTable;
        ownership[path] = new("codex", owned?.Previous ?? existing, owned?.ContainerCreated ?? !hasTui);
        if (entry?.Value is { } oldValue)
        {
            return CodexFooterSettings.Replace(text, oldValue, CodexValue);
        }
        var line = CodexKey + " = " + CodexValue + newline;
        var section = document.Tables.OfType<TableSyntax>().FirstOrDefault(table => CodexFooterSettings.Parts(table.Name).SequenceEqual(["tui"]));
        if (section is not null)
        {
            var end = text.IndexOf('\n', section.CloseBracket!.Span.End.Offset);
            return end < 0 ? text + newline + line : text.Insert(end + 1, line);
        }
        if (hasTui)
        {
            if (document.KeyValues.Any(pair => CodexFooterSettings.Parts(pair.Key).SequenceEqual(["tui"])))
            {
                throw new InvalidDataException("Codex uses an inline tui table. Add terminal_title there before using the title step.");
            }
            return "tui." + line + text;
        }
        return text + (text.Length == 0 || text.EndsWith('\n') ? "" : newline) + "[tui]" + newline + line;
    }

    private static string? Compact(string? value) => value is null ? null : string.Concat(value.Where(character => !char.IsWhiteSpace(character)));

    private static void RemoveKey(DocumentSyntax document, KeyValueSyntax entry)
    {
        if (document.KeyValues.Contains(entry))
        {
            document.KeyValues.RemoveChild(entry);
            return;
        }
        foreach (var table in document.Tables.OfType<TableSyntax>())
        {
            if (table.Items.Contains(entry))
            {
                table.Items.RemoveChild(entry);
                return;
            }
        }
    }

    private static string RemoveEmptyTuiTable(string text)
    {
        var document = SyntaxParser.Parse(text);
        var table = document.Tables.OfType<TableSyntax>().FirstOrDefault(item => CodexFooterSettings.Parts(item.Name).SequenceEqual(["tui"]));
        if (table is null || table.Items.ChildrenCount != 0)
        {
            return text;
        }
        document.Tables.RemoveChild(table);
        return document.ToString();
    }

    private static string? Result(JsonSettingsFile file, string? before)
    {
        var changes = new List<FileChange>();
        file.AddTo(changes);
        return changes.Count == 0 ? before : changes[0].After;
    }
}
