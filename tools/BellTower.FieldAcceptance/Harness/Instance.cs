using System.Diagnostics;
using System.IO.Compression;
using BellTower.FieldAcceptance.Isolation;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// One disposable installation with its own data directory, fake user home, temp, fake provider binaries and child environment.
/// Every process it starts passes <see cref="ProductionGuard"/> first, so a scenario cannot reach the user's installation, data,
/// provider settings or tray even by mistake. Scenarios build on this class; they never start processes themselves.
/// </summary>
public sealed class Instance : IDisposable
{
    public InstanceLayout Layout { get; }
    public ProductionLocations Production { get; }
    public SystemTools Tools { get; }
    public IReadOnlyDictionary<string, string> Environment { get; }
    public IReadOnlyList<string> RemovedPathEntries { get; }
    public IReadOnlyList<string> EnvironmentViolations { get; }
    public LockedList<string> CommandLog { get; } = new();
    public LockedList<AgentSession> Sessions { get; } = new();

    /// <summary>The run-wide stop request: once set, this instance starts no new process, session or hook (cleanup excepted).</summary>
    public RunAbort Abort { get; }

    /// <summary>What this instance has exposed to the user's real profile environment (per instance, sticky, fed by every session start and hook).</summary>
    public RealEnvironmentExposure Exposure { get; } = new();

    /// <summary>Hides the sandbox trays' windows on the maintainer's desktop (the product has no setting for that).</summary>
    public SandboxWindowGuard WindowGuard { get; }

    // Per thread: only the thread that is disposing may keep using the instance after a stop request (another thread may still be unwinding).
    [ThreadStatic]
    private static bool cleaning;

    private Instance(InstanceLayout layout, ProductionLocations production, SystemTools tools, Dictionary<string, string> environment, IReadOnlyList<string> removed, IReadOnlyList<string> environmentViolations, RunAbort abort)
    {
        Abort = abort;
        Layout = layout;
        WindowGuard = new SandboxWindowGuard(layout.Install);
        Production = production;
        Tools = tools;
        Environment = environment;
        RemovedPathEntries = removed;
        EnvironmentViolations = environmentViolations;
    }

    public static Instance Create(string name, string sandboxBase, string allowedBase, ProductionLocations production, SystemTools tools, RunAbort? abort = null)
    {
        (abort ??= RunAbort.Current).ThrowIfRequested();
        ProcessEnvironmentSwap.ThrowIfForeign();
        var layout = new InstanceLayout(name, Path.Combine(sandboxBase, name));
        ProductionGuard.Require(ProductionGuard.CheckLayout(layout, production, allowedBase));
        if (Directory.Exists(layout.Root) && Directory.EnumerateFileSystemEntries(layout.Root).Any())
        {
            throw new IOException("The sandbox directory is not empty; each run uses a fresh one.");
        }
        layout.Create();
        var parent = System.Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value ?? "", StringComparer.OrdinalIgnoreCase);
        var (environment, removed) = ProductionGuard.BuildEnvironment(parent, layout, production);
        var violations = ProductionGuard.CheckEnvironment(environment, production);
        ProductionGuard.Require(violations);
        var instance = new Instance(layout, production, tools, environment, removed, violations, abort);
        try
        {
            instance.InstallFakeProviders();
        }
        catch
        {
            // The window guard thread started with the instance must not outlive a failed creation.
            instance.WindowGuard.Dispose();
            throw;
        }
        return instance;
    }

    private void InstallFakeProviders()
    {
        var source = AppContext.BaseDirectory.TrimEnd('\\', '/');
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(Layout.FakeBin, Path.GetRelativePath(source, directory)));
        }
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(Layout.FakeBin, Path.GetRelativePath(source, file)), true);
        }
        var apphost = Path.Combine(Layout.FakeBin, "BellTower.FieldAcceptance.exe");
        foreach (var name in new[] { "claude.exe", "codex.exe", FakeAgent.UnidentifiedPrefix + "claude.exe", FakeAgent.UnidentifiedPrefix + "codex.exe" })
        {
            File.Copy(apphost, Path.Combine(Layout.FakeBin, name), true);
        }
    }

    /// <param name="unidentified">The image whose name is not the provider's, so the product's process ancestry finds no agent process (null-process startup).</param>
    public string FakeAgentPath(string provider, bool unidentified = false) => Path.Combine(Layout.FakeBin, (unidentified ? FakeAgent.UnidentifiedPrefix : "") + provider + ".exe");

    public void ExtractInstallation(string archive) => ZipFile.ExtractToDirectory(archive, Layout.Install);

    /// <summary>
    /// Runs one sandbox process to completion. Safe to call from several threads. Do not run an installed provider hook command through
    /// here: use <see cref="AgentSession.Hook"/>, the only path that notes a hook-started tray for the exposure guard (ANALYSIS-142).
    /// </summary>
    public ProcessResult Run(string executable, IReadOnlyList<string> arguments, string? stdin = null, int timeoutMilliseconds = 60000,
        string? workingDirectory = null, IReadOnlyDictionary<string, string>? extraEnvironment = null, bool record = true)
    {
        if (!cleaning)
        {
            Abort.ThrowIfRequested();
            ProcessEnvironmentSwap.ThrowIfForeign();
        }
        ProductionGuard.Require(ProductionGuard.CheckCommand(executable, arguments, Layout, Production, Tools.All));
        var environment = new Dictionary<string, string>(Environment, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in extraEnvironment ?? new Dictionary<string, string>())
        {
            environment[name] = value;
        }
        ProductionGuard.Require(ProductionGuard.CheckEnvironment(environment, Production));
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory ?? Layout.Root
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
        if (record)
        {
            CommandLog.Add(Display(executable, arguments));
        }
        var watch = Stopwatch.StartNew();
        using var process = Process.Start(start) ?? throw new IOException("Could not start " + Path.GetFileName(executable));
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            if (stdin is not null)
            {
                process.StandardInput.Write(stdin);
            }
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The program may exit before reading its input; its exit code and streams still describe the outcome.
        }
        var timedOut = !process.WaitForExit(timeoutMilliseconds);
        if (timedOut)
        {
            process.Kill(true);
            process.WaitForExit();
        }
        if (!cleaning)
        {
            // An interrupt ends in-flight children; their result is then meaningless and must not become a misleading check.
            Abort.ThrowIfRequested();
        }
        return new(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult(), watch.Elapsed, timedOut);
    }

    /// <summary>Runs the installed launcher against this instance's data directory (always passed explicitly).</summary>
    public ProcessResult Tower(params string[] arguments) => Run(Layout.Launcher, [.. arguments, "--data-dir", Layout.Data]);

    public ProcessResult TowerWithHome(params string[] arguments) => TowerWithHome(60000, arguments);

    public ProcessResult TowerWithHome(int timeoutMilliseconds, params string[] arguments) =>
        Run(Layout.Launcher, [.. arguments, "--home", Layout.Home, "--data-dir", Layout.Data], timeoutMilliseconds: timeoutMilliseconds);

    private string Display(string executable, IReadOnlyList<string> arguments)
    {
        var name = PathRelation.IsUnderOrEqual(executable, Layout.Root) ? Path.GetRelativePath(Layout.Root, executable) : Path.GetFileName(executable);
        var text = name + " " + string.Join(' ', arguments.Select(argument => argument.Length > 120 ? argument[..60] + "..." : argument));
        return text.Replace(Layout.Root, "<sandbox>", StringComparison.OrdinalIgnoreCase).Replace(Layout.Root.Replace('\\', '/'), "<sandbox>", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Runs an in-process call (an update transaction) with the process environment replaced by this instance's, so any child it
    /// starts inherits the sandbox values and nothing of the harness user's environment. Single-threaded, and enforced by
    /// <see cref="ProcessEnvironmentSwap"/>: while it runs, another thread that creates an instance, runs a process or starts a session
    /// gets an exception instead of a child environment built from another sandbox. Start hooks or load after it returns.
    /// </summary>
    public T WithProcessEnvironment<T>(Func<T> action) => ProcessEnvironmentSwap.Run(Environment, action);

    // ---- Hooks ---------------------------------------------------------------------------------------------------

    /// <summary>Starts a live fake provider session (see <see cref="AgentSession"/>); it ends with this instance.</summary>
    public AgentSession StartSession(string provider, string sessionId, string directory, string? attachmentId, IReadOnlyList<string>? arguments = null, bool unidentified = false, bool dedicated = false, bool ownedRuntime = false)
    {
        var session = AgentSession.Start(this, provider, sessionId, directory, attachmentId, arguments, unidentified, dedicated, ownedRuntime);
        Sessions.Add(session);
        return session;
    }

    /// <summary>
    /// Runs a report to this instance's exposure. When it refuses (a hook-started tray and a shared-backend Codex in one instance), the
    /// instance's trays are ended at once, because detecting a hook start cannot undo it, and the refusal propagates.
    /// </summary>
    public void Expose(Action<RealEnvironmentExposure> report) => Exposure.Guard(report, () => EndTrays());

    /// <summary>Ends this instance's tray, launcher and ingress processes (everything under its install directory); sessions and the harness itself are untouched.</summary>
    public IReadOnlyList<string> EndTrays() => LeftoverSweep.KillProcessesUnder(Layout.Install, System.Environment.ProcessId);

    // ---- Cleanup -------------------------------------------------------------------------------------------------

    /// <summary>The on-demand tray start task setup registers for this data directory (a per-user Task Scheduler entry, the one effect outside the sandbox).</summary>
    public string TaskPath => BellTower.Protocol.HookStartTask.TaskPath(Layout.Data);

    public bool TaskExists()
    {
        var result = Run(Tools.Schtasks, ["/query", "/tn", TaskPath], record: false, timeoutMilliseconds: 15000);
        return result.ExitCode == 0;
    }

    private int disposed;

    /// <summary>
    /// Stops this instance's tray, removes its scheduled task and ends any process still running from the sandbox. Never touches anything
    /// outside it. Safe to call more than once and from the interrupt handlers; each step is isolated so one failure cannot skip the rest.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }
        cleaning = true;
        try
        {
            DisposeSteps();
        }
        finally
        {
            cleaning = false;
        }
    }

    private void DisposeSteps()
    {
        var failures = new List<Exception>();
        void Step(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failures.Add(exception);
            }
        }
        foreach (var session in Sessions.ToArray())
        {
            Step(session.Dispose);
        }
        Step(() =>
        {
            if (File.Exists(Layout.Launcher))
            {
                Tower("exit");
            }
        });
        Step(() =>
        {
            if (TaskExists())
            {
                Run(Tools.Schtasks, ["/delete", "/tn", TaskPath, "/f"], record: false, timeoutMilliseconds: 15000);
            }
        });
        Step(() => LeftoverSweep.KillProcessesUnder(Layout.Root, System.Environment.ProcessId));
        Step(WindowGuard.Dispose);
        if (failures.Count > 0)
        {
            throw new AggregateException("Cleanup of instance " + Layout.Name + " had " + failures.Count + " failing step(s), the remaining steps still ran.", failures);
        }
    }

    public void DeleteFiles()
    {
        for (var attempt = 0; attempt < 10 && Directory.Exists(Layout.Root); attempt++)
        {
            try
            {
                Directory.Delete(Layout.Root, true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(300);
            }
        }
    }
}
