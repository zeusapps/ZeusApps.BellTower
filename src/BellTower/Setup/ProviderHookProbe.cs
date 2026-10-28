using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using BellTower.IO;
using BellTower.Protocol;
using Tomlyn;
using Tomlyn.Model;

namespace BellTower.Setup;

internal static class ProviderHookProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly CodexHookShell[] WindowsCodexShellCandidates =
    [
        CodexHookShell.PowerShell,
        CodexHookShell.Cmd,
        CodexHookShell.Bash
    ];

    public static CodexHookShell SelectCodexShell(string executable, string ingress)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        foreach (var shell in WindowsCodexShellCandidates)
        {
            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }
            try
            {
                Verify("codex", executable, ingress, shell, remaining);
                return shell;
            }
            catch (IOException)
            {
                // A candidate is accepted only when Codex's native hook runner produced a durable capture.
            }
        }
        throw new IOException("Codex did not deliver the isolated hook through PowerShell, cmd, or Bash within the bounded shell-selection probe; no settings were changed.");
    }

    public static void Verify(string provider, string executable, string ingress, CodexHookShell? codexShell = null, TimeSpan? timeout = null)
    {
        var temporaryRoot = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "BellTower HookProbe.Δ.O'Brien.%BELLTOWER_HOOK_PROBE_EXPANSION% ! & ^ (x)." + Guid.NewGuid().ToString("N"));
        if (!temporaryRoot.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("The provider hook probe path is not isolated.");
        }

        Directory.CreateDirectory(temporaryRoot);
        try
        {
            Directory.CreateDirectory(Path.Combine(temporaryRoot, "project"));
            var data = new DataPaths(Path.Combine(temporaryRoot, "data"));
            var hook = new HookCommandBuilder(executable, data, ingress, codexShell).Build("event", provider);
            if (provider == "codex")
            {
                VerifyCodex(data, hook, temporaryRoot, timeout ?? Timeout);
            }
            else
            {
                VerifyBash(data, hook, temporaryRoot);
            }
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Exception? lastFailure = null;
                for (var attempt = 0; attempt < 6; attempt++)
                {
                    try
                    {
                        Directory.Delete(temporaryRoot, recursive: true);
                        lastFailure = null;
                        break;
                    }
                    catch (IOException exception)
                    {
                        lastFailure = exception;
                        Thread.Sleep(100);
                    }
                }
                if (lastFailure is not null)
                {
                    throw new IOException("The isolated provider hook probe could not clean up its temporary directory.", lastFailure);
                }
            }
        }
    }

    private static void VerifyBash(DataPaths data, string hook, string root)
    {
        var bash = ResolveClaudeBash() ?? throw new IOException("Bash is not available to verify Claude hook delivery.");
        using var process = StartHookShell(bash, ["-c", hook], root);
        process.StandardInput.WriteLine(Event("claude", root));
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit((int)Timeout.TotalMilliseconds);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
        var response = output.GetAwaiter().GetResult().Trim();
        if (!exited || process.ExitCode != 0 || response is not ("" or "{}") ||
            error.GetAwaiter().GetResult().Length != 0 || !HasCapture(data, "claude"))
        {
            throw new IOException("Claude hook delivery could not be verified through Bash; no settings were changed.");
        }
    }

    private static void VerifyCodex(DataPaths data, string hook, string root, TimeSpan timeout)
    {
        var codex = ResolveCodexExecutable() ?? throw new IOException("Codex CLI is not available to verify hook delivery; no settings were changed.");
        var home = Path.Combine(root, "codex-home");
        var project = Path.Combine(root, "project");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(project);
        var projectKey = project.Replace("\\", "/", StringComparison.Ordinal);
        using var endpoint = new ProbeEndpoint();
        endpoint.Start();
        var config = TomlSerializer.Serialize(new TomlTable
        {
            ["model"] = "hook-probe-model",
            ["model_provider"] = "hook_probe",
            ["cli_auth_credentials_store"] = "file",
            ["approval_policy"] = "never",
            ["sandbox_mode"] = "read-only",
            ["check_for_update_on_startup"] = false,
            ["features"] = new TomlTable
            {
                ["hooks"] = true,
                ["plugins"] = false,
                ["remote_plugin"] = false,
                ["apps"] = false,
                ["tool_suggest"] = false
            },
            ["analytics"] = new TomlTable { ["enabled"] = false },
            ["model_providers"] = new TomlTable
            {
                ["hook_probe"] = new TomlTable
                {
                    ["name"] = "Isolated hook probe",
                    ["base_url"] = "http://127.0.0.1:" + endpoint.Port + "/v1",
                    ["wire_api"] = "responses",
                    ["env_key"] = "BELLTOWER_HOOK_PROBE_KEY",
                    ["request_max_retries"] = 0,
                    ["stream_max_retries"] = 0
                }
            },
            ["projects"] = new TomlTable
            {
                [projectKey] = new TomlTable { ["trust_level"] = "trusted" }
            },
            ["tui"] = new TomlTable { ["screen_reader_detection_done"] = true }
        });
        File.WriteAllText(Path.Combine(home, "config.toml"), config);
        var hookConfig = new
        {
            hooks = new Dictionary<string, object>
            {
                ["SessionStart"] = new[]
                {
                    new { matcher = ".*", hooks = new[] { new { type = "command", command = hook, timeout = 3 } } }
                }
            }
        };
        File.WriteAllText(Path.Combine(home, "hooks.json"), JsonSerializer.Serialize(hookConfig));

        var processInfo = new ProcessStartInfo(codex)
        {
            WorkingDirectory = project,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        processInfo.Environment["CODEX_HOME"] = home;
        processInfo.Environment["BELLTOWER_HOOK_PROBE_KEY"] = "isolated-hook-probe";
        processInfo.Environment["BELLTOWER_HOOK_PROBE_EXPANSION"] = "incorrect-expansion-marker";
        foreach (var argument in new[]
        {
            "exec", "--json", "--dangerously-bypass-hook-trust", "--sandbox", "read-only", "--skip-git-repo-check",
            "Verify the isolated hook."
        })
        {
            processInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(processInfo) ?? throw new IOException("Codex could not start its isolated hook probe.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        var deadline = DateTimeOffset.UtcNow + timeout;
        var observedWhileRunning = false;
        while (!process.HasExited && DateTimeOffset.UtcNow < deadline)
        {
            if (HasCapture(data, "codex"))
            {
                observedWhileRunning = true;
                break;
            }
            Thread.Sleep(50);
        }

        var timedOut = !observedWhileRunning && !process.HasExited && DateTimeOffset.UtcNow >= deadline;
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
        _ = stdout.GetAwaiter().GetResult();
        _ = stderr.GetAwaiter().GetResult();
        if (!CaptureConfirmsCompletedProbe(observedWhileRunning, timedOut, process.HasExited, data, "codex"))
        {
            var result = timedOut ? "timed out" : process.HasExited ? "exit code " + process.ExitCode : "runner did not exit";
            var stdoutLength = stdout.GetAwaiter().GetResult().Length;
            var stderrLength = stderr.GetAwaiter().GetResult().Length;
            throw new IOException($"Codex hook delivery could not be verified through its native runner ({result}; stdout={stdoutLength > 0}; stderr={stderrLength > 0}); no settings were changed.");
        }
        endpoint.Stop();
    }

    internal static bool CaptureConfirmsCompletedProbe(bool observedWhileRunning, bool timedOut, bool runnerExited,
        DataPaths data, string provider) =>
        (observedWhileRunning || runnerExited && !timedOut) && HasCapture(data, provider);

    private static Process StartHookShell(string executable, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        return Process.Start(start) ?? throw new IOException("The provider hook shell could not start.");
    }

    private static string Event(string provider, string root) => JsonSerializer.Serialize(new
    {
        session_id = "bell-tower-hook-probe",
        cwd = Path.Combine(root, "project"),
        hook_event_name = "SessionStart",
        source = "startup"
    });

    private static bool HasCapture(DataPaths data, string provider)
    {
        var inbox = Path.Combine(data.Root, CaptureProtocol.InboxName);
        if (!Directory.Exists(inbox))
        {
            return false;
        }

        foreach (var path in Directory.EnumerateFiles(inbox, "*.json"))
        {
            try
            {
                using var stream = File.OpenRead(path);
                if (CaptureProtocol.Decode(stream).Observation.Provider == provider)
                {
                    return true;
                }
            }
            catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or InvalidDataException) { }
        }
        return false;
    }

    private static string? ResolveCodexExecutable()
    {
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var direct = Path.Combine(entry, OperatingSystem.IsWindows() ? "codex.exe" : "codex");
            if (File.Exists(direct))
            {
                return direct;
            }
            if (OperatingSystem.IsWindows() && (File.Exists(Path.Combine(entry, "codex.ps1")) || File.Exists(Path.Combine(entry, "codex.cmd"))))
            {
                var packaged = Path.Combine(entry, "node_modules", "@openai", "codex", "node_modules", "@openai", "codex-win32-x64",
                    "vendor", "x86_64-pc-windows-msvc", "bin", "codex.exe");
                if (File.Exists(packaged))
                {
                    return packaged;
                }
            }
        }

        if (OperatingSystem.IsWindows())
        {
            var userInstall = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "bin", "codex.exe");
            if (File.Exists(userInstall))
            {
                return userInstall;
            }
        }
        return null;
    }

    private static string? ResolveApplication(params string[] names)
    {
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in names)
            {
                var candidate = Path.Combine(entry, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        return null;
    }

    private static string? ResolveClaudeBash()
    {
        // Claude's documented Windows override must win over PATH, matching the runner users configure.
        var configured = Environment.GetEnvironmentVariable("CLAUDE_CODE_GIT_BASH_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var fullPath = Path.GetFullPath(configured);
            if (!File.Exists(fullPath))
            {
                throw new IOException("Claude's configured Git Bash path is unavailable; no settings were changed.");
            }
            return fullPath;
        }
        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var gitBash = Path.Combine(programFiles, "Git", "bin", "bash.exe");
            if (File.Exists(gitBash))
            {
                return gitBash;
            }
        }
        return ResolveApplication("bash.exe", "bash");
    }

    private sealed class ProbeEndpoint : IDisposable
    {
        private readonly HttpListener listener = new();
        private Task? serve;

        public int Port { get; }

        public ProbeEndpoint()
        {
            using var socket = new TcpListener(IPAddress.Loopback, 0);
            socket.Start();
            Port = ((IPEndPoint)socket.LocalEndpoint).Port;
            socket.Stop();
            listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        }

        public void Start()
        {
            listener.Start();
            serve = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext context;
                    try
                    { context = await listener.GetContextAsync().ConfigureAwait(false); }
                    catch (HttpListenerException) { break; }
                    catch (ObjectDisposedException) { break; }
                    if (context.Request.HttpMethod != "POST")
                    {
                        context.Response.ContentType = "application/json";
                        await context.Response.OutputStream.WriteAsync("{\"data\":[]}"u8.ToArray()).ConfigureAwait(false);
                    }
                    else
                    {
                        context.Response.ContentType = "text/event-stream";
                        var responseId = "resp_probe_" + Guid.NewGuid().ToString("N");
                        var events = string.Join("\n\n", new[]
                        {
                            $"event: response.created\ndata: {{\"type\":\"response.created\",\"response\":{{\"id\":\"{responseId}\"}}}}",
                            $"event: response.output_item.done\ndata: {{\"type\":\"response.output_item.done\",\"item\":{{\"id\":\"msg_probe\",\"type\":\"message\",\"role\":\"assistant\",\"content\":[{{\"type\":\"output_text\",\"text\":\"Probe complete.\"}}]}}}}",
                            $"event: response.completed\ndata: {{\"type\":\"response.completed\",\"response\":{{\"id\":\"{responseId}\",\"status\":\"completed\",\"usage\":{{\"input_tokens\":1,\"output_tokens\":1,\"total_tokens\":2}}}}}}"
                        }) + "\n\n";
                        await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(events)).ConfigureAwait(false);
                    }
                    context.Response.Close();
                }
            });
        }

        public void Stop()
        {
            if (listener.IsListening)
            {
                listener.Stop();
            }
            try
            { serve?.GetAwaiter().GetResult(); }
            catch (HttpListenerException) { }
        }

        public void Dispose()
        {
            Stop();
            listener.Close();
        }
    }
}
