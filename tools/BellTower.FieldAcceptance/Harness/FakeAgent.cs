using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// Stand-in provider processes. The harness copies its own executable into the sandbox as <c>claude.exe</c> and <c>codex.exe</c>
/// (never a real provider binary, never credentials). A fake agent stays alive like a provider session and runs each hook as its
/// child, so hooks see the same process ancestry and image name a real provider gives them. With <c>exec</c> a fake Codex answers
/// the setup probe the way Codex's hook runner does: it runs the SessionStart hook of <c>CODEX_HOME/hooks.json</c> through the shell
/// Codex 0.164.0 discovers (see <see cref="CodexShell"/>) with the event on standard input.
/// </summary>
public static class FakeAgent
{
    /// <summary>Set (to 1) in the environment of a session agent: it then serves hook requests, one JSON line each, until its input closes.</summary>
    public const string ServeVariable = "FIELD_ACCEPTANCE_SERVE";

    public sealed record HookRequest(string Executable, string[]? Arguments, string? RawArguments, string Stdin);

    public sealed record HookResponse(int ExitCode, string Output, string Error, double Milliseconds);

    /// <summary>The loopback port a detached (unidentified) agent connects to instead of using standard streams.</summary>
    public const string ConnectVariable = "FIELD_ACCEPTANCE_CONNECT";

    /// <summary>First argument of the harness executable acting as a spawner that starts an agent and exits at once, leaving it with a dead parent.</summary>
    public const string SpawnDetachedArgument = "--spawn-detached";

    /// <summary>Starts the agent named by <paramref name="args"/> (image, then its arguments) with the inherited environment and returns at once.</summary>
    public static int SpawnDetached(string[] args)
    {
        var start = new ProcessStartInfo(args[0]) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Environment.CurrentDirectory };
        foreach (var argument in args.Skip(1))
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start) ?? throw new IOException("Could not start the detached agent.");
        return 0;
    }

    /// <summary>The image-name prefix of a fake agent the product cannot recognize as its provider's process (the null-process startup injection).</summary>
    /// <summary>First argument of the harness executable acting as an owned runtime's manager: it starts the backend, stays alive as its parent and exits when it exits.</summary>
    public const string ManagerArgument = "--manager";

    public static int RunManager(string[] args)
    {
        var start = new ProcessStartInfo(args[0]) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Environment.CurrentDirectory };
        foreach (var argument in args.Skip(1))
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start) ?? throw new IOException("Could not start the backend.");
        process.WaitForExit();
        return 0;
    }

    public const string UnidentifiedPrefix = "unidentified-";

    public static bool IsFakeImage(string? processPath) =>
        Path.GetFileNameWithoutExtension(processPath ?? "") is { } name &&
        (name.Equals("claude", StringComparison.OrdinalIgnoreCase) || name.Equals("codex", StringComparison.OrdinalIgnoreCase) ||
         name.Equals(UnidentifiedPrefix + "claude", StringComparison.OrdinalIgnoreCase) || name.Equals(UnidentifiedPrefix + "codex", StringComparison.OrdinalIgnoreCase));

    public static int Run(string[] args)
    {
        var name = Path.GetFileNameWithoutExtension(Environment.ProcessPath)!.ToLowerInvariant();
        if (name == "codex" && args.Contains("exec"))
        {
            return Probe();
        }
        if (Environment.GetEnvironmentVariable(ServeVariable) != "1")
        {
            Console.Error.WriteLine("fake " + name + ": not started as a session agent");
            return 2;
        }
        TextReader input = Console.In;
        TextWriter output = Console.Out;
        TcpClient? connection = null;
        if (int.TryParse(Environment.GetEnvironmentVariable(ConnectVariable), out var port))
        {
            // A detached agent has no inherited standard streams: it serves over the loopback socket the harness opened.
            connection = new TcpClient("127.0.0.1", port);
            var stream = connection.GetStream();
            input = new StreamReader(stream);
            output = new StreamWriter(stream) { AutoFlush = true };
        }
        output.WriteLine(connection is null ? "ready" : "ready " + Environment.ProcessId);
        output.Flush();
        while (input.ReadLine() is { } line)
        {
            var request = JsonSerializer.Deserialize<HookRequest>(line)!;
            var watch = Stopwatch.StartNew();
            var (exit, hookOutput, error) = RunChild(request.Executable, request.Arguments, request.RawArguments, request.Stdin, Environment.CurrentDirectory);
            output.WriteLine(JsonSerializer.Serialize(new HookResponse(exit, hookOutput, error, watch.Elapsed.TotalMilliseconds)));
            output.Flush();
        }
        connection?.Dispose();
        return 0;
    }

    // Codex's setup probe: `codex exec` with an isolated CODEX_HOME whose hooks.json holds a SessionStart command.
    private static int Probe()
    {
        var home = Environment.GetEnvironmentVariable("CODEX_HOME")!;
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(home, "hooks.json")));
        var command = document.RootElement.GetProperty("hooks").GetProperty("SessionStart")[0].GetProperty("hooks")[0].GetProperty("command").GetString()!;
        var payload = JsonSerializer.Serialize(new { session_id = "fake-codex-probe", cwd = Environment.CurrentDirectory, hook_event_name = "SessionStart", source = "startup" });
        var shell = CodexShell.Resolve(Environment.GetEnvironmentVariable("PATH"), Environment.GetEnvironmentVariable("ProgramFiles") ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.SystemDirectory);
        var (exit, output, error) = shell.Kind == HookShell.PowerShell
            ? RunChild(shell.Executable, ["-NoProfile", "-NonInteractive", "-Command", command], null, payload, Environment.CurrentDirectory)
            : RunChild(shell.Executable, null, "/d /s /c \"" + command + "\"", payload, Environment.CurrentDirectory);
        Console.Out.Write(output);
        Console.Error.Write(error);
        return exit;
    }

    private static (int ExitCode, string Output, string Error) RunChild(string executable, string[]? arguments, string? rawArguments, string stdin, string directory)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory
        };
        if (rawArguments is not null)
        {
            start.Arguments = rawArguments;
        }
        foreach (var argument in arguments ?? [])
        {
            start.ArgumentList.Add(argument);
        }
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var error = child.StandardError.ReadToEndAsync();
        try
        {
            child.StandardInput.Write(stdin);
            child.StandardInput.Close();
        }
        catch (IOException)
        {
            // A hook that exits before reading its input is reported through its exit code and streams.
        }
        if (!child.WaitForExit(30000))
        {
            child.Kill(true);
            return (124, "", "hook timed out");
        }
        return (child.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
}
