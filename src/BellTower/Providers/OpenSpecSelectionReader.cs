using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BellTower.IO;

namespace BellTower.Providers;

public static partial class OpenSpecSelectionReader
{
    public static OpenSpecSelection? Read(JsonElement input)
    {
        if (HookInput.Text(input, "agent_id") is not null || HookInput.Text(input, "tool_name") != "Bash" ||
            HookInput.Text(input, "hook_event_name") is not ("PreToolUse" or "PostToolUse") ||
            !input.TryGetProperty("tool_input", out var arguments) ||
            HookInput.Text(arguments, "command") is not { Length: > 0 and <= 8192 } command ||
            HookInput.Text(input, "tool_use_id") is not { Length: > 0 } operation)
        {
            return null;
        }

        var words = Words(command);
        if (words is null || words.Count < 3)
        {
            return null;
        }
        var executable = Path.GetFileName(words[0].Replace('\\', '/'));
        if (executable is not ("openspec" or "openspec.cmd" or "openspec.ps1" or "openspec.exe"))
        {
            return null;
        }

        string? change = null;
        string? store = null;
        var verb = words[1];
        var index = 2;
        if (verb == "new")
        {
            if (words.Count < 4 || words[2] != "change")
            {
                return null;
            }
            change = words[3];
            index = 4;
        }
        else if (verb == "instructions")
        {
            if (words[index].StartsWith('-'))
            {
                return null;
            }
            index++;
        }
        else if (verb != "status")
        {
            return null;
        }

        while (index < words.Count)
        {
            var word = words[index++];
            if (word == "--json")
            {
                continue;
            }
            var split = word.IndexOf('=');
            var option = split < 0 ? word : word[..split];
            if (option is not ("--change" or "--store"))
            {
                return null;
            }
            var value = split >= 0 ? word[(split + 1)..] : index < words.Count ? words[index++] : "";
            if (value.Length == 0 || value.StartsWith('-'))
            {
                return null;
            }
            if (option == "--change")
            {
                if (change is not null)
                {
                    return null;
                }
                change = value;
            }
            else
            {
                if (store is not null || !StoreName().IsMatch(value))
                {
                    return null;
                }
                store = value;
            }
        }

        return change is not null && Name().IsMatch(change)
            ? new(change, store, ContentHash.Compute(operation), verb) : null;
    }

    public static string? SuccessfulRoot(JsonElement input, OpenSpecSelection selection)
    {
        if (HookInput.Text(input, "hook_event_name") != "PostToolUse" ||
            !input.TryGetProperty("tool_response", out var response))
        {
            return null;
        }
        string? output;
        if (response.ValueKind == JsonValueKind.String)
        {
            output = response.GetString();
        }
        else if (response.ValueKind == JsonValueKind.Object)
        {
            if (response.TryGetProperty("exit_code", out var code) &&
                (code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var exit) || exit != 0) ||
                response.TryGetProperty("interrupted", out var interrupted) && interrupted.ValueKind == JsonValueKind.True)
            {
                return null;
            }
            output = HookInput.Text(response, "stdout");
        }
        else
        {
            return null;
        }

        if (output is null)
        {
            return null;
        }
        try
        {
            using var result = JsonDocument.Parse(output.TrimStart('\uFEFF'));
            var data = result.RootElement;
            if (HookInput.Text(data, "changeName") == selection.Change &&
                HookInput.Text(data, "schemaName") is not null &&
                !data.TryGetProperty("error", out _) && data.TryGetProperty("root", out var root) &&
                HookInput.Text(root, "path") is { } path && Path.IsPathFullyQualified(path))
            {
                return Path.GetFullPath(path);
            }
        }
        catch (JsonException) { }

        // `new change` has no JSON success output. Require both its exact success marker
        // and a local scaffold; a failed create against an existing folder is not selection.
        if (selection.Verb == "new" && selection.Store is null &&
            output.Contains("Created change '" + selection.Change + "' at ", StringComparison.Ordinal) &&
            HookInput.Text(input, "cwd") is { } directory)
        {
            for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "openspec")))
                {
                    return File.Exists(Path.Combine(current.FullName, "openspec", "changes", selection.Change, ".openspec.yaml"))
                        ? current.FullName : null;
                }
            }
        }
        return null;
    }

    private static List<string>? Words(string command)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        char quote = '\0';
        var started = false;
        var literal = command.Trim();
        // PowerShell's call operator is allowed only before the literal executable.
        if (literal.StartsWith("& ", StringComparison.Ordinal))
        {
            literal = literal[2..].TrimStart();
        }
        foreach (var character in literal)
        {
            // Expansion and control characters are rejected even inside quotes. This
            // intentionally recognizes literal invocations, never shell programs.
            if (character is '$' or '`' or '\r' or '\n' or '\0')
            {
                return null;
            }
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
                else
                {
                    word.Append(character);
                }
                continue;
            }
            if (character is '\'' or '"')
            {
                quote = character;
                started = true;
            }
            else if (char.IsWhiteSpace(character))
            {
                if (started)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    started = false;
                }
            }
            else if (character is ';' or '|' or '&' or '(' or ')' or '<' or '>')
            {
                return null;
            }
            else
            {
                word.Append(character);
                started = true;
            }
        }
        if (quote != '\0')
        {
            return null;
        }
        if (started)
        {
            words.Add(word.ToString());
        }
        return words;
    }

    [GeneratedRegex("\\A[a-z0-9]+(?:-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Name();

    [GeneratedRegex("\\A[A-Za-z0-9][A-Za-z0-9._-]{0,127}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex StoreName();
}
