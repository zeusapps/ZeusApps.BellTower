namespace BellTower.Core;

// A literal command recognized inside a bounded shell wrapper. WorkDirectory is the resolved
// literal cd/Set-Location target (null when the command runs in the hook working directory).
public sealed record ShellInvocation(IReadOnlyList<string> Words, string? WorkDirectory, bool Redirected);

// Recognizes literal command invocations only. It never evaluates shell programs: expansion,
// substitution, subshells, multiple or ambiguous directory changes and arbitrary chains are rejected.
public static class ShellGrammar
{
    public const int MaximumLength = 8192;

    private readonly record struct Token(string Text, bool Operator);

    public static ShellInvocation? Parse(string command, string? cwd)
    {
        if (command.Length is 0 or > MaximumLength || command.IndexOfAny(['$', '`', '\r', '\n', '\0']) >= 0)
        {
            return null;
        }
        var text = Unwrap(command.Trim());
        if (text is null)
        {
            return null;
        }
        // PowerShell's call operator is allowed only before the literal executable.
        if (text.StartsWith("& ", StringComparison.Ordinal))
        {
            text = text[2..].TrimStart();
        }
        var tokens = Tokenize(text);
        if (tokens is null)
        {
            return null;
        }

        var segments = new List<List<Token>> { new() };
        foreach (var token in tokens)
        {
            if (token.Operator && token.Text is "&&" or ";")
            {
                segments.Add([]);
            }
            else
            {
                segments[^1].Add(token);
            }
        }
        if (segments.Count > 2 || segments.Any(segment => segment.Count == 0))
        {
            return null;
        }

        string? workDirectory = null;
        if (segments.Count == 2)
        {
            workDirectory = ChangeDirectory(segments[0], cwd);
            if (workDirectory is null)
            {
                return null;
            }
        }
        var body = segments[^1];
        if (body.Count == 0 || body[0].Operator || IsDirectoryCommand(body[0].Text))
        {
            return null;
        }

        var words = new List<string>();
        var index = 0;
        while (index < body.Count && !body[index].Operator)
        {
            words.Add(body[index++].Text);
        }
        var redirected = false;
        if (index < body.Count)
        {
            var tail = body.Skip(index).ToArray();
            if (tail[0].Text is ">" or ">>")
            {
                if (tail.Length != 2 || tail[1].Operator)
                {
                    return null;
                }
                redirected = true;
            }
            else if (tail[0].Text != "|" || !Filter(tail.Skip(1).ToArray()))
            {
                return null;
            }
        }
        return words.Count == 0 ? null : new(words, workDirectory, redirected);
    }

    private static bool IsDirectoryCommand(string word) =>
        word.Equals("cd", StringComparison.OrdinalIgnoreCase) || word.Equals("Set-Location", StringComparison.OrdinalIgnoreCase);

    private static string? ChangeDirectory(List<Token> segment, string? cwd)
    {
        if (segment.Any(token => token.Operator) || segment.Count is < 2 or > 3 || !IsDirectoryCommand(segment[0].Text))
        {
            return null;
        }
        var words = segment.Select(token => token.Text).ToArray();
        string target;
        if (words.Length == 2)
        {
            target = words[1];
        }
        else if (words[0].Equals("cd", StringComparison.OrdinalIgnoreCase) && words[1].Equals("/d", StringComparison.OrdinalIgnoreCase) ||
            words[0].Equals("Set-Location", StringComparison.OrdinalIgnoreCase) &&
            (words[1].Equals("-Path", StringComparison.OrdinalIgnoreCase) || words[1].Equals("-LiteralPath", StringComparison.OrdinalIgnoreCase)))
        {
            target = words[2];
        }
        else
        {
            return null;
        }
        if (target.Length == 0 || target[0] is '-' or '~' || target.IndexOfAny(['*', '?', '%', '<', '>', '|']) >= 0)
        {
            return null;
        }
        try
        {
            if (Path.IsPathFullyQualified(target))
            {
                return Path.GetFullPath(target);
            }
            // Drive-relative and root-relative forms depend on shell state we do not have.
            return Path.IsPathRooted(target) || cwd is null || !Path.IsPathFullyQualified(cwd) ? null : Path.GetFullPath(target, cwd);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    // One terminal filter stage. Anything that could change what ran or hide a failure is rejected.
    private static bool Filter(Token[] stage)
    {
        if (stage.Length == 0 || stage.Any(token => token.Operator))
        {
            return false;
        }
        var words = stage.Select(token => token.Text).ToArray();
        return words[0].ToLowerInvariant() switch
        {
            "out-string" or "convertto-json" or "cat" => words.Length == 1,
            "head" => words.Length == 2 && IsCount(words[1].TrimStart('-')) ||
                words.Length == 3 && words[1] == "-n" && IsCount(words[2]),
            "select-object" => words.Length == 3 && words[1].Equals("-First", StringComparison.OrdinalIgnoreCase) && IsCount(words[2]),
            _ => false
        };
    }

    private static bool IsCount(string value) => value.Length is > 0 and <= 4 && value.All(char.IsAsciiDigit);

    private static string? Unwrap(string text)
    {
        var space = text.IndexOf(' ');
        if (space < 0)
        {
            return text;
        }
        var executable = text[..space];
        if (!executable.Equals("pwsh", StringComparison.OrdinalIgnoreCase) && !executable.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase) &&
            !executable.Equals("powershell", StringComparison.OrdinalIgnoreCase) && !executable.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }
        var rest = text[(space + 1)..].TrimStart();
        if (rest.StartsWith("-NoProfile ", StringComparison.OrdinalIgnoreCase))
        {
            rest = rest["-NoProfile ".Length..].TrimStart();
        }
        if (!rest.StartsWith("-Command ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var nested = rest["-Command ".Length..].Trim();
        if (nested.Length < 2 || nested[0] is not ('\'' or '"') || nested[^1] != nested[0] || nested[1..^1].Contains(nested[0]))
        {
            return null;
        }
        return nested[1..^1];
    }

    private static List<Token>? Tokenize(string text)
    {
        var tokens = new List<Token>();
        var word = new System.Text.StringBuilder();
        var started = false;
        var quote = '\0';
        void Flush()
        {
            if (started)
            {
                tokens.Add(new(word.ToString(), false));
                word.Clear();
                started = false;
            }
        }
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
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
                Flush();
            }
            else if (character == '&' && i + 1 < text.Length && text[i + 1] == '&')
            {
                Flush();
                tokens.Add(new("&&", true));
                i++;
            }
            else if (character == ';')
            {
                Flush();
                tokens.Add(new(";", true));
            }
            else if (character == '|')
            {
                if (i + 1 < text.Length && text[i + 1] == '|')
                {
                    return null;
                }
                Flush();
                tokens.Add(new("|", true));
            }
            else if (character == '>')
            {
                Flush();
                if (i + 1 < text.Length && text[i + 1] == '>')
                {
                    tokens.Add(new(">>", true));
                    i++;
                }
                else
                {
                    tokens.Add(new(">", true));
                }
            }
            else if (character is '&' or '(' or ')' or '<' or '{' or '}')
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
        Flush();
        return tokens;
    }
}
