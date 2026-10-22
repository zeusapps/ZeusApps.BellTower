using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace BellTower.Setup;

public static class CodexFooterSettings
{
    public static string? Edit(string path, string? before, Dictionary<string, OwnedValue> previous,
        Dictionary<string, OwnedValue> ownership, bool remove, List<string> notes)
    {
        var text = before ?? "";
        var document = SyntaxParser.Parse(text);
        if (document.HasErrors)
        {
            throw new InvalidDataException("Codex configuration contains invalid TOML.");
        }
        var model = TomlSerializer.Deserialize<TomlTable>(text)!;
        var footer = Find(document);
        var existing = footer?.Value is { } value ? Slice(text, value) : null;
        var owned = previous.GetValueOrDefault(path);
        if (owned is not null && existing != owned.After)
        {
            notes.Add("Kept user-edited Codex footer: " + path);
            ownership.Remove(path);
            return before;
        }
        if (remove)
        {
            ownership.Remove(path);
            if (owned is null || footer is null)
            {
                return before;
            }
            if (owned.Before is not null)
            {
                return Replace(text, footer.Value!, owned.Before);
            }
            // An added key is always a regular table/root entry, never an inline item.
            if (document.KeyValues.Contains(footer))
            {
                document.KeyValues.RemoveChild(footer);
            }
            else
            {
                foreach (var table in document.Tables.OfType<TableSyntax>())
                {
                    if (table.Items.Contains(footer))
                    {
                        table.Items.RemoveChild(footer);
                        break;
                    }
                }
            }
            return document.ToString();
        }

        if (model.TryGetValue("tui", out var rawTui) && rawTui is not TomlTable)
        {
            throw new InvalidDataException("Codex tui configuration must be a table.");
        }
        var tui = rawTui as TomlTable;
        var items = new List<string>();
        if (tui is not null && tui.TryGetValue("status_line", out var rawItems))
        {
            if (rawItems is not TomlArray array || array.Any(item => item is not string))
            {
                throw new InvalidDataException("Codex tui.status_line must be an array of strings.");
            }
            items.AddRange(array.Cast<string>());
        }
        else
        {
            items.AddRange(["model-with-reasoning", "context-remaining", "current-dir"]);
        }
        foreach (var item in new[] { "thread-name", "git-branch" })
        {
            if (!items.Contains(item))
            {
                items.Add(item);
            }
        }
        var values = new TomlArray();
        foreach (var item in items)
        {
            values.Add(item);
        }
        var serialized = TomlSerializer.Serialize(new TomlTable { ["status_line"] = values });
        var afterValue = Slice(serialized, SyntaxParser.Parse(serialized).KeyValues.Single().Value!);
        if (owned is null && existing is not null && tui is not null &&
            tui["status_line"] is TomlArray current && current.OfType<string>().Contains("thread-name") &&
            current.OfType<string>().Contains("git-branch"))
        {
            // Already configured by the user; leave it unowned.
            return before;
        }
        ownership[path] = new(owned?.Before ?? existing, afterValue);
        if (owned is not null)
        {
            ownership[path] = owned with { After = afterValue };
        }
        if (footer?.Value is { } oldValue)
        {
            return Replace(text, oldValue, afterValue);
        }

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var section = document.Tables.OfType<TableSyntax>().FirstOrDefault(table => Parts(table.Name).SequenceEqual(["tui"]));
        var line = "status_line = " + afterValue + newline;
        if (section is not null)
        {
            var end = text.IndexOf('\n', section.CloseBracket!.Span.End.Offset);
            return end < 0 ? text + newline + line : text.Insert(end + 1, line);
        }
        if (tui is not null)
        {
            // Dotted top-level keys permit adding a sibling; sealed inline tables do not.
            if (document.KeyValues.Any(pair => Parts(pair.Key).SequenceEqual(["tui"])))
            {
                throw new InvalidDataException("Codex uses an inline tui table. Add status_line there before enabling Bell Tower's footer.");
            }
            return "tui." + line + text;
        }
        return text + (text.Length == 0 || text.EndsWith('\n') ? "" : newline) + "[tui]" + newline + line;
    }

    private static KeyValueSyntax? Find(DocumentSyntax document)
    {
        foreach (var pair in document.KeyValues)
        {
            if (Parts(pair.Key).SequenceEqual(["tui", "status_line"]))
            {
                return pair;
            }
            if (Parts(pair.Key).SequenceEqual(["tui"]) && pair.Value is InlineTableSyntax inline)
            {
                return inline.Items.Select(item => item.KeyValue).OfType<KeyValueSyntax>()
                    .FirstOrDefault(item => Parts(item.Key).SequenceEqual(["status_line"]));
            }
        }
        return document.Tables.OfType<TableSyntax>().Where(table => Parts(table.Name).SequenceEqual(["tui"]))
            .SelectMany(table => table.Items).FirstOrDefault(pair => Parts(pair.Key).SequenceEqual(["status_line"]));
    }

    private static IEnumerable<string> Parts(KeySyntax? key)
    {
        if (key is null)
        {
            yield break;
        }
        var table = TomlSerializer.Deserialize<TomlTable>(key + " = 0")!;
        while (true)
        {
            var pair = table.Single();
            yield return pair.Key;
            if (pair.Value is not TomlTable child)
            {
                yield break;
            }
            table = child;
        }
    }

    private static string Slice(string text, SyntaxNode value) => text.Substring(value.Span.Start.Offset, value.Span.Length);
    private static string Replace(string text, SyntaxNode value, string replacement) =>
        text[..value.Span.Start.Offset] + replacement + text[(value.Span.Start.Offset + value.Span.Length)..];
}
