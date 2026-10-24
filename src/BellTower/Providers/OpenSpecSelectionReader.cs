using System.Text.Json;
using System.Text.RegularExpressions;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Providers;

// Provider adapter: translates Claude Code and Codex hook payloads into the shared work evidence model.
// Classification policy lives in Core (WorkPolicy, ShellGrammar); this class only reads native shapes.
public static partial class OpenSpecSelectionReader
{
    // Claude Code: Bash and PowerShell. Codex hooks report its shell tool as Bash (verified against the
    // openai/codex hook payload sources); history normalizes exec_command to the same name.
    private static bool IsShell(string? tool) => tool is "Bash" or "PowerShell";

    public static OpenSpecSelection? Read(JsonElement input)
    {
        if (ReadWork(input) is { } work)
        { return work; }
        if (Classify(input) is not { Verb: "new" } created || HookInput.Text(input, "tool_use_id") is not { Length: > 0 } operation)
        {
            return null;
        }
        return new(created.Change, created.Store, ContentHash.Compute(operation), "new")
        { Directory = created.Directory, Redirected = created.Redirected };
    }

    // Recognizes any bounded literal OpenSpec CLI invocation, including inspection forms.
    public static OpenSpecOperation? Classify(JsonElement input)
    {
        if (HookInput.Text(input, "agent_id") is not null || !IsShell(HookInput.Text(input, "tool_name")) ||
            HookInput.Text(input, "hook_event_name") is not ("PreToolUse" or "PostToolUse") ||
            !input.TryGetProperty("tool_input", out var arguments) ||
            HookInput.Text(arguments, "command") is not { Length: > 0 } command)
        {
            return null;
        }
        var cwd = HookInput.Text(input, "cwd");
        var invocation = ShellGrammar.Parse(command, cwd);
        if (invocation is null || invocation.Words.Count < 3)
        {
            return null;
        }
        var words = invocation.Words;
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

        return change is not null && WorkClassifier.IsChangeName(change)
            ? new(verb, change, store, invocation.WorkDirectory ?? cwd, invocation.Redirected) : null;
    }

    private static OpenSpecSelection? ReadWork(JsonElement input)
    {
        if (HookInput.Text(input, "agent_id") is not null || HookInput.Text(input, "hook_event_name") is not ("PreToolUse" or "PostToolUse") ||
            !input.TryGetProperty("tool_input", out var args) || HookInput.Text(input, "tool_use_id") is not { } operation)
        { return null; }
        var tool = HookInput.Text(input, "tool_name");
        var cwd = HookInput.Text(input, "cwd");
        var candidates = new List<(string Change, string Root)>();
        void PathCandidate(string? value)
        {
            if (WorkClassifier.ChangeFromPath(value, cwd) is { } found)
            { candidates.Add(found); }
        }
        if (tool is "Write" or "Edit" or "MultiEdit" or "NotebookEdit")
        { PathCandidate(HookInput.Text(args, "file_path") ?? HookInput.Text(args, "notebook_path")); }
        else if (tool == "apply_patch")
        {
            // Codex hooks provide the patch as tool_input.command.
            var patch = args.ValueKind == JsonValueKind.String ? args.GetString() : HookInput.Text(args, "command") ?? HookInput.Text(args, "patch");
            if (patch is { Length: <= 65536 })
            {
                foreach (var line in patch.Split('\n').Take(2000))
                {
                    foreach (var prefix in new[] { "*** Update File: ", "*** Add File: ", "*** Delete File: ", "*** Move to: " })
                    { if (line.StartsWith(prefix, StringComparison.Ordinal)) { PathCandidate(line[prefix.Length..].TrimEnd('\r')); } }
                }
            }
        }
        else if (tool == "Skill" && HookInput.Text(args, "skill") is { } skill && WorkClassifier.IsWorkflowSkill(skill))
        {
            var literal = HookInput.Text(args, "args")?.Trim();
            if (literal is not null && WorkClassifier.IsChangeName(literal) && ScaffoldLocator.ChangeRoot(cwd, literal) is { } root)
            {
                return new(literal, null, ContentHash.Compute(operation), "skill") { WorkRoot = root, Weak = WorkClassifier.IsWeakSkill(skill) };
            }
            return null;
        }
        var unique = candidates.Distinct().ToArray();
        return unique.Length == 1 ? new(unique[0].Change, null, ContentHash.Compute(operation), "edit") { WorkRoot = unique[0].Root } : null;
    }

    public static string? SuccessfulRoot(JsonElement input, OpenSpecSelection selection, bool checkScaffold = true)
    {
        if (HookInput.Text(input, "hook_event_name") != "PostToolUse" ||
            !input.TryGetProperty("tool_response", out var response))
        {
            return null;
        }
        var failed = Failed(response);
        if (selection.WorkRoot is { } workRoot)
        {
            return failed ? null : workRoot;
        }

        // `new change` prints no JSON. Require its exact success marker (or, for redirected output,
        // a scaffold created at the observed time) plus a local scaffold; a failed create against
        // an existing folder is not selection.
        var output = response.ValueKind == JsonValueKind.String ? response.GetString() : HookInput.Text(response, "stdout");
        var marker = output is not null && output.Contains("Created change '" + selection.Change + "' at ", StringComparison.Ordinal);
        if (failed || selection.Verb != "new" || selection.Store is not null || !marker && !selection.Redirected)
        {
            return null;
        }
        return checkScaffold
            ? ScaffoldLocator.Root(selection.Directory ?? HookInput.Text(input, "cwd"), selection.Change,
                selection.Redirected && !marker ? DateTimeOffset.UtcNow : null)
            : null;
    }

    // True when the captured response itself shows that the operation did not succeed.
    public static bool Failed(JsonElement response) => response.ValueKind == JsonValueKind.Object &&
        (response.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True ||
         response.TryGetProperty("exit_code", out var code) && (code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var exit) || exit != 0) ||
         response.TryGetProperty("interrupted", out var interrupted) && interrupted.ValueKind == JsonValueKind.True);

    [GeneratedRegex("\\A[A-Za-z0-9][A-Za-z0-9._-]{0,127}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex StoreName();
}
