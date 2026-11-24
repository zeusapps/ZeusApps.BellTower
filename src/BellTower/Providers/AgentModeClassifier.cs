using System.Text;
using BellTower.Core;

namespace BellTower.Providers;

/// <summary>
/// Provider adapter for native adoption: classifies the hook's agent ancestor from its verified command-line vocabulary and
/// states whether a provider can host a shared backend. Everything not provably a direct client fails closed to unknown.
/// </summary>
public static class AgentModeClassifier
{
    // Provider adapter fact: Codex has a shared backend whose hook ancestry and environment belong to its first launcher.
    // No shared backend is known for Claude, so an unreadable Claude command line keeps its existing direct classification.
    public static bool MayShareBackend(string provider) => provider == "codex";

    /// <summary>
    /// Provider adapter fact: true only for Codex <c>app-server</c> launched as the provider's detached managed daemon, which
    /// carries <c>--managed-daemon</c> before any <c>--</c>. That backend is pidfile-backed and relaunched by the provider's
    /// updater, so its parent is whichever transient process last started it and says nothing about whether it is the shared
    /// backend. Bell Tower's owned runtime, stdio <c>app-server</c>, the updater loop and every Claude invocation are false.
    /// </summary>
    public static bool LauncherIndependentBackend(string provider, IReadOnlyList<string>? arguments)
    {
        if (provider != "codex" || arguments is null)
        {
            return false;
        }
        var end = arguments.ToList().IndexOf("--");
        var scanned = end < 0 ? arguments : arguments.Take(end).ToList();
        return scanned.Contains("--managed-daemon") && Codex(scanned) == AgentModes.SharedBackend;
    }

    // Codex 0.165.0 options that consume the next token, so the value is never mistaken for a subcommand or prompt.
    private static readonly HashSet<string> CodexValueOptions = new(StringComparer.Ordinal)
    {
        "-c", "--config", "-m", "--model", "-p", "--profile", "-s", "--sandbox", "-a", "--ask-for-approval", "-C", "--cd",
        "-i", "--image", "--add-dir", "--enable", "--disable", "--remote", "--remote-auth-token-env"
    };

    // Options without values. --no-daemon is verified; the others come from the 0.165.0 help. An option outside both
    // lists makes the invocation unknown, because its value cannot be skipped safely.
    private static readonly HashSet<string> CodexFlagOptions = new(StringComparer.Ordinal)
    {
        "--no-daemon", "--oss", "--full-auto", "--dangerously-bypass-approvals-and-sandbox", "--search", "--no-alt-screen"
    };

    // Subcommands that are not interactive sessions (0.165.0 help). They never claim a terminal.
    private static readonly HashSet<string> CodexNonSessionCommands = new(StringComparer.Ordinal)
    {
        "mcp", "mcp-server", "exec-server", "execpolicy", "login", "logout", "apply", "a", "cloud", "sandbox", "debug",
        "completion", "features", "plugin", "proto", "review", "responses-api-proxy", "stdio-to-uds", "help", "app", "marketplace", "update"
    };

    /// <param name="arguments">Arguments after the executable, or null when they could not be read.</param>
    public static string Classify(string provider, bool agentFound, IReadOnlyList<string>? arguments)
    {
        if (!agentFound || arguments is null)
        {
            return AgentModes.Unknown;
        }
        return provider switch
        {
            "claude" => Claude(arguments),
            "codex" => Codex(arguments),
            _ => AgentModes.Unknown
        };
    }

    // Verified: native `claude --print` is headless (evidence claude-native-001). Any other invocation hosts its own hooks in an interactive client.
    private static string Claude(IReadOnlyList<string> arguments)
    {
        foreach (var argument in arguments)
        {
            if (argument == "--")
            {
                break;
            }
            if (argument is "-p" or "--print")
            {
                return AgentModes.Headless;
            }
        }
        return AgentModes.InteractiveClient;
    }

    // Verified for Codex 0.165.0: `app-server` (anywhere before `--`) is the shared backend; `exec` is headless with an
    // in-process runtime; the embedded runtime is only a TUI with no subcommand and known options, a positional prompt that
    // is not a known subcommand name, `resume` or `fork`. Known non-session subcommands and unknown options are unknown.
    private static string Codex(IReadOnlyList<string> arguments)
    {
        var end = arguments.ToList().IndexOf("--");
        var scanned = end < 0 ? arguments : arguments.Take(end).ToList();
        if (scanned.Contains("app-server"))
        {
            return AgentModes.SharedBackend;
        }
        for (var index = 0; index < scanned.Count; index++)
        {
            var argument = scanned[index];
            if (argument.StartsWith('-'))
            {
                var name = argument.StartsWith("--", StringComparison.Ordinal) && argument.IndexOf('=') is var equals and > 0 ? argument[..equals] : argument;
                if (CodexValueOptions.Contains(name))
                {
                    index += name == argument ? 1 : 0;
                }
                else if (!CodexFlagOptions.Contains(name))
                {
                    return AgentModes.Unknown;
                }
                continue;
            }
            return argument switch
            {
                "exec" or "e" => AgentModes.Headless,
                _ when CodexNonSessionCommands.Contains(argument) => AgentModes.Unknown,
                _ => AgentModes.EmbeddedRuntime
            };
        }
        return AgentModes.EmbeddedRuntime;
    }

    /// <summary>Splits a Windows command line with the standard quoting and backslash rules, dropping the executable.</summary>
    public static List<string> Arguments(string commandLine)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var started = false;
        var quoted = false;
        var index = 0;
        while (index < commandLine.Length && result.Count < 256)
        {
            var character = commandLine[index];
            if (character == '\\')
            {
                var slashes = 0;
                while (index < commandLine.Length && commandLine[index] == '\\')
                {
                    slashes++;
                    index++;
                }
                started = true;
                if (index < commandLine.Length && commandLine[index] == '"')
                {
                    current.Append('\\', slashes / 2);
                    if (slashes % 2 == 1)
                    {
                        current.Append('"');
                        index++;
                    }
                }
                else
                {
                    current.Append('\\', slashes);
                }
                continue;
            }
            index++;
            if (character == '"')
            {
                quoted = !quoted;
                started = true;
            }
            else if (char.IsWhiteSpace(character) && !quoted)
            {
                if (started)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    started = false;
                }
            }
            else
            {
                current.Append(character);
                started = true;
            }
        }
        if (started)
        {
            result.Add(current.ToString());
        }
        if (result.Count > 0)
        {
            result.RemoveAt(0);
        }
        return result;
    }
}
