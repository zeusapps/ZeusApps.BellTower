using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using BellTower.FieldAcceptance.Isolation;
using BellTower.Protocol;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// A live fake provider session: a sandbox <c>claude.exe</c> or <c>codex.exe</c> that stays running (as a real session does,
/// including across an update) and runs each hook as its child with the event on standard input. A managed session carries the
/// attachment id Bell Tower's launcher exports; a plain one (native adoption) carries nothing.
/// </summary>
public sealed class AgentSession : IDisposable
{
    private readonly Instance instance;
    private readonly Process process;
    private readonly TextReader reader;
    private readonly TextWriter writer;
    private readonly IDisposable? connection;
    private Process? manager;
    private readonly object gate = new();

    public string Provider { get; }
    public string SessionId { get; }
    public string Directory { get; }
    public string? AttachmentId { get; }
    public int ProcessId => process.Id;

    /// <summary>The process names above this session's process in the product's own ancestry walk (the nearest first), captured at start: the evidence a null-process verdict relies on.</summary>
    public IReadOnlyList<string> Ancestry { get; private set; } = [];

    private AgentSession(Instance instance, string provider, string sessionId, string directory, string? attachmentId, Process process, TextReader reader, TextWriter writer, IDisposable? connection = null)
    {
        this.reader = reader;
        this.writer = writer;
        this.connection = connection;
        this.instance = instance;
        Provider = provider;
        SessionId = sessionId;
        Directory = directory;
        AttachmentId = attachmentId;
        this.process = process;
    }

    public string Key => Provider + ":" + SessionId;

    /// <param name="arguments">The provider process arguments; null takes the model's default (a Codex session then carries <c>--no-daemon</c>).</param>
    /// <param name="dedicated">Sets <c>BELLTOWER_DEDICATED=1</c>, as a Bell Tower launch of the owned Codex runtime does.</param>
    /// <param name="unidentified">Starts the image the product cannot recognize as the provider (null-process startup): its hooks capture no agent process.</param>
    public static AgentSession Start(Instance instance, string provider, string sessionId, string directory, string? attachmentId, IReadOnlyList<string>? arguments = null, bool unidentified = false, bool dedicated = false, bool ownedRuntime = false)
    {
        instance.Abort.ThrowIfRequested();
        ProcessEnvironmentSwap.ThrowIfForeign();
        var environment = new Dictionary<string, string>(instance.Environment, StringComparer.OrdinalIgnoreCase) { [FakeAgent.ServeVariable] = "1" };
        if (attachmentId is not null)
        {
            environment["BELLTOWER_ATTACHMENT"] = attachmentId;
        }
        if (dedicated)
        {
            // What Bell Tower's own Codex launch sets for the owned --no-daemon runtime (BellTower.psm1, AgentLauncher): the 0.1.9 ingress keeps the managed id only with it.
            environment["BELLTOWER_DEDICATED"] = "1";
        }
        var executable = instance.FakeAgentPath(provider, unidentified);
        var model = ProviderModel.For(provider);
        arguments ??= model.AgentArguments;
        // A Codex without --no-daemon uses a backend shared with the real installation; an instance that also has a hook-started tray is refused.
        instance.Expose(exposure => exposure.NoteSessionStart(model, arguments));
        ProductionGuard.Require(ProductionGuard.CheckCommand(executable, arguments, instance.Layout, instance.Production, instance.Tools.All));
        ProductionGuard.Require(ProductionGuard.CheckEnvironment(environment, instance.Production));
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory
        };
        start.Environment.Clear();
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        if (unidentified || ownedRuntime)
        {
            return StartDetached(instance, provider, sessionId, directory, attachmentId, executable, arguments, environment, unidentified, ownedRuntime);
        }
        var process = Process.Start(start) ?? throw new IOException("Could not start the fake " + provider + " session.");
        _ = process.StandardError.ReadToEndAsync();
        if (process.StandardOutput.ReadLine() != "ready")
        {
            process.Kill(true);
            throw new IOException("The fake " + provider + " session did not become ready.");
        }
        var session = new AgentSession(instance, provider, sessionId, directory, attachmentId, process, process.StandardOutput, process.StandardInput);
        session.Ancestry = ProductAncestry(process.Id);
        return session;
    }

    /// <summary>
    /// The null-process startup must not depend on where the harness runs (QA-260): the product's ancestry walk takes the first <c>claude.exe</c> or
    /// <c>codex.exe</c> among up to 32 ancestors, so an unidentified fake agent that is a descendant of the harness would find the real host provider
    /// that launched the harness. The agent is therefore started through a spawner that exits at once (the harness executable in
    /// <c>--spawn-detached</c> mode), which leaves the agent with a dead parent: the walk stops there, whatever launched the harness. The agent talks
    /// to the harness over a loopback socket instead of standard input and output (it has no inherited streams).
    /// </summary>
    private static AgentSession StartDetached(Instance instance, string provider, string sessionId, string directory, string? attachmentId, string executable,
        IReadOnlyList<string> arguments, Dictionary<string, string> environment, bool unidentified, bool ownedRuntime)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            environment[FakeAgent.ConnectVariable] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var spawner = Path.Combine(instance.Layout.FakeBin, "BellTower.FieldAcceptance.exe");
            var mode = ownedRuntime ? FakeAgent.ManagerArgument : FakeAgent.SpawnDetachedArgument;
            ProductionGuard.Require(ProductionGuard.CheckCommand(spawner, [mode, executable, .. arguments], instance.Layout, instance.Production, instance.Tools.All));
            var start = new ProcessStartInfo(spawner) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory };
            start.Environment.Clear();
            foreach (var (name, value) in environment)
            {
                start.Environment[name] = value;
            }
            start.ArgumentList.Add(mode);
            start.ArgumentList.Add(executable);
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }
            var helper = Process.Start(start) ?? throw new IOException("Could not start the detached spawner.");
            if (!ownedRuntime)
            {
                // A spawner exits at once and leaves the agent with a dead parent; an owned runtime's manager stays alive as the backend's parent.
                helper.WaitForExit(15000);
                helper.Dispose();
                helper = null;
            }
            var accept = listener.AcceptTcpClientAsync();
            if (!accept.Wait(20000))
            {
                throw new IOException("The detached fake " + provider + " session did not connect.");
            }
            var client = accept.Result;
            var stream = client.GetStream();
            var reader = new StreamReader(stream);
            var writer = new StreamWriter(stream) { AutoFlush = true };
            var ready = reader.ReadLine()?.Split(' ');
            if (ready is not ["ready", var pid] || !int.TryParse(pid, out var agentPid))
            {
                client.Dispose();
                throw new IOException("The detached fake " + provider + " session did not become ready.");
            }
            var process = Process.GetProcessById(agentPid);
            var session = new AgentSession(instance, provider, sessionId, directory, attachmentId, process, reader, writer, client);
            session.manager = helper;
            session.Ancestry = ProductAncestry(agentPid);
            if (ownedRuntime && attachmentId is not null)
            {
                // The runtime record names the manager (the backend's parent) and the shell: both the dedicated idle helper, never the harness.
                OwnedRuntimeRecord.Write(instance, attachmentId, helper!.Id);
            }
            if (unidentified && session.Ancestry.Any(name => name.Equals(provider + ".exe", StringComparison.OrdinalIgnoreCase)))
            {
                session.Dispose();
                throw new InvalidOperationException("The unidentified fake " + provider + " session still has a " + provider + ".exe ancestor (" + string.Join(" <- ", session.Ancestry) + "): the null-process model would depend on the host.");
            }
            return session;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>The ancestor image names the product's ancestry walk sees above a process (nearest first, at most 32), from the same process table type the ingress uses.</summary>
    public static IReadOnlyList<string> ProductAncestry(int pid) => [.. BellTower.IO.ProcessTable.Take().Ancestors(pid).Select(ancestor => ancestor.Name)];

    /// <summary>
    /// Runs an installed hook command as a child of this session through the given shell. Hooks must go through here and not through
    /// <see cref="Instance.Run"/>: only this path compares the recorded tray start attempt before and after the hook (the exposure mark
    /// that keeps a hook-started tray and a shared-backend Codex apart). Calls on different sessions may run in parallel (this method
    /// keeps no shared mutable state beyond the instance's thread-safe exposure and logs); calls on one session are serialized, like a real
    /// provider's.
    /// </summary>
    public ProcessResult Hook(string command, string payload, ResolvedShell shell)
    {
        instance.Abort.ThrowIfRequested();
        var tools = instance.Tools;
        var request = shell.Kind switch
        {
            HookShell.GitBash => new FakeAgent.HookRequest(shell.Executable, ["-c", command], null, payload),
            HookShell.PowerShell => new FakeAgent.HookRequest(shell.Executable, ["-NoProfile", "-NonInteractive", "-Command", command], null, payload),
            _ => new FakeAgent.HookRequest(shell.Executable, null, "/d /s /c \"" + command + "\"", payload)
        };
        ProductionGuard.Require(ProductionGuard.CheckCommand(request.Executable, [.. request.Arguments ?? [], request.RawArguments ?? ""], instance.Layout, instance.Production, tools.All));
        lock (gate)
        {
            var attemptBefore = HookTrayStart.LastAttempt(instance.Layout.Data);
            var watch = Stopwatch.StartNew();
            string? line;
            try
            {
                writer.WriteLine(JsonSerializer.Serialize(request));
                writer.Flush();
                line = reader.ReadLine();
            }
            catch (IOException) when (instance.Abort.Requested)
            {
                line = null;
            }
            // An interrupt ends the session mid-hook: that is the stop, not a hook failure.
            instance.Abort.ThrowIfRequested();
            if (line is null)
            {
                throw new IOException("The fake session ended while running a hook.");
            }
            var response = JsonSerializer.Deserialize<FakeAgent.HookResponse>(line)!;
            // A hook can start a tray (by product design with the real profile environment): report it to the instance's exposure so a shared-backend
            // Codex in the same instance is refused and its trays ended. The attempt record is written before the hook returns.
            var attemptAfter = HookTrayStart.LastAttempt(instance.Layout.Data);
            instance.Expose(exposure => exposure.NoteHook(attemptBefore, attemptAfter));
            return new(response.ExitCode, response.Output, response.Error, watch.Elapsed, response.ExitCode == 124, response.Milliseconds);
        }
    }

    public bool IsRunning => !process.HasExited;

    public void Dispose()
    {
        try
        {
            if (!process.HasExited)
            {
                writer.Close();
                connection?.Dispose();
                if (!process.WaitForExit(3000))
                {
                    process.Kill(true);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            // The sandbox sweep in Instance.Dispose ends anything left.
        }
        try
        {
            if (manager is { HasExited: false })
            {
                manager.Kill(true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The sandbox sweep ends anything left.
        }
        manager?.Dispose();
        process.Dispose();
    }
}
