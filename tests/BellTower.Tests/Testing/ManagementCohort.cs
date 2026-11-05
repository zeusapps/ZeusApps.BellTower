using System.Diagnostics;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Tests.Testing;

/// <summary>
/// Stand-in processes for the ownership tests of Bell Tower management actions (detach, stop, setup removal, tray exit, update).
/// Everything is a copy of cmd.exe (or ping.exe) under a workspace image, so nothing is a real provider. The independent
/// processes model what Bell Tower must never touch: a user's Claude and Codex clients and shell, and a shared or private
/// Codex backend started by someone else. The owned manager models a runtime-host manager that Bell Tower started: it
/// honors the stop marker exactly like <c>CodexRuntimeHost.RunAsync</c> and removes its own record files on exit.
/// </summary>
internal sealed class ManagementCohort : IDisposable
{
    public const string OwnedAttachment = "owned-runtime-attachment";
    private readonly List<Process> started = [];
    private readonly TestWorkspace workspace;

    public ProcessIdentity Shell { get; }
    public ProcessIdentity ClaudeClient { get; }
    public ProcessIdentity CodexClient { get; }
    public ProcessIdentity SharedBackend { get; }
    public ProcessIdentity PrivateBackend { get; }
    public ProcessIdentity OwnedManager { get; }

    public ManagementCohort(TestWorkspace workspace)
    {
        this.workspace = workspace;
        Shell = Idle("shell", "powershell.exe");
        ClaudeClient = Idle("claude", "claude.exe");
        CodexClient = Idle("codex-client", "codex.exe");
        SharedBackend = Idle("codex-shared", "codex.exe");
        PrivateBackend = Idle("codex-private", "codex.exe");
        OwnedManager = StartOwnedManager();
    }

    // Optional native row: a real backend started outside the tests (BELLTOWER_NATIVE_BACKEND=<pid>,<creation ticks>) joins
    // the independent set, so every management action is also checked against it. Unset in normal runs.
    private static ProcessIdentity? NativeBackend =>
        Environment.GetEnvironmentVariable("BELLTOWER_NATIVE_BACKEND")?.Split(',') is [var pid, var ticks] && int.TryParse(pid, out var id) && long.TryParse(ticks, out var started)
            ? new ProcessIdentity(id, started) : null;

    public IReadOnlyList<ProcessIdentity> Independent => NativeBackend is { } native
        ? [Shell, ClaudeClient, CodexClient, SharedBackend, PrivateBackend, native]
        : [Shell, ClaudeClient, CodexClient, SharedBackend, PrivateBackend];

    public static bool IsLive(ProcessIdentity process) => ProcessProbe.Inspect(process) == ProcessLiveness.Live;

    public bool WaitForEnd(ProcessIdentity process, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (IsLive(process) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(50);
        }
        return !IsLive(process);
    }

    public void AssertIndependentSurvive(string action)
    {
        foreach (var process in Independent)
        {
            Assert(IsLive(process), $"{action} must not stop an independent process (pid {process.Pid}).");
        }
    }

    public CodexRuntime OwnedRecord(ProcessIdentity? manager = null, string? attachment = null, string? endpoint = null) =>
        new(attachment ?? OwnedAttachment, endpoint ?? "ws://127.0.0.1:9", manager ?? OwnedManager, Shell);

    public const string FixtureToken = "fixture-token";

    public void WriteRecord(CodexRuntime runtime)
    {
        var store = new CodexRuntimeStore(workspace.Paths);
        Directory.CreateDirectory(Path.GetDirectoryName(store.RecordPath(runtime.Attachment))!);
        AtomicFile.Write(store.RecordPath(runtime.Attachment), Json.Encode(runtime));
        File.WriteAllText(store.TokenPath(runtime.Attachment), FixtureToken);
    }

    private ProcessIdentity Idle(string folder, string image) =>
        Launch(image, folder, "ping -n 180 127.0.0.1 >nul");

    private ProcessIdentity StartOwnedManager()
    {
        var store = new CodexRuntimeStore(workspace.Paths);
        var script = Path.Combine(workspace.Root, "owned-manager.cmd");
        File.WriteAllText(script,
            "@echo off\r\n" +
            ":loop\r\n" +
            $"if exist \"{store.StopPath(OwnedAttachment)}\" goto stop\r\n" +
            "ping -n 2 127.0.0.1 >nul\r\n" +
            "goto loop\r\n" +
            ":stop\r\n" +
            $"del \"{store.RecordPath(OwnedAttachment)}\" \"{store.TokenPath(OwnedAttachment)}\" \"{store.StopPath(OwnedAttachment)}\"\r\n");
        var manager = Launch("bell-tower.exe", "owned-manager", $"\"{script}\"");
        WriteRecord(new(OwnedAttachment, "ws://127.0.0.1:9", manager, Shell));
        return manager;
    }

    private ProcessIdentity Launch(string image, string folder, string command)
    {
        var start = new ProcessStartInfo(StandIns.CopyImage(workspace, "stand-in", folder, image))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Arguments = StandIns.Under("", command)
        };
        StandIns.ScrubEnvironment(start);
        var process = Process.Start(start)!;
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        started.Add(process);
        return ProcessProbe.Identify(process.Id)!;
    }

    private static void Assert(bool condition, string message) => Xunit.Assert.True(condition, message);

    public void Dispose()
    {
        foreach (var process in started)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }
            process.Dispose();
        }
    }
}
