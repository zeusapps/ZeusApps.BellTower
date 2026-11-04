using System.Diagnostics;
using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

// QA-1304: AttachmentCapture.Resolve runs in the real hook paths as a child of a real process whose image is named like
// the provider client. The image is a copy of cmd.exe, so the command line is real and readable; no provider is launched.
// The provider arguments come first and `/s /c` follows (cmd.exe tolerates leading tokens; each row's `{}` output and
// stored attachment double as the self-check that it still does). Rows run through both entry points: the legacy `event`
// command and the production fast-hook `bell-tower-ingress.exe`, whose envelope is drained with StateStore.
public sealed class NativeAgentResolveProcessTests
{
    public const string Event = "event";
    public const string Ingress = "ingress";

    // Provider arguments and the classification they must produce. Each token is read by the classifier.
    private static readonly (string Provider, string Arguments, string Mode)[] Rows =
    [
        ("claude", "", AgentModes.InteractiveClient),
        ("claude", "-p", AgentModes.Headless),
        ("claude", "--print", AgentModes.Headless),
        ("claude", "-- -p", AgentModes.InteractiveClient),
        ("codex", "", AgentModes.EmbeddedRuntime),
        ("codex", "\"fixture prompt\"", AgentModes.EmbeddedRuntime),
        ("codex", "exec", AgentModes.Headless),
        ("codex", "e", AgentModes.Headless),
        ("codex", "app-server", AgentModes.SharedBackend),
        ("codex", "review", AgentModes.Unknown),
        ("codex", "--future-option value", AgentModes.Unknown)
    ];

    public static TheoryData<string, string, string, string> Shapes()
    {
        var data = new TheoryData<string, string, string, string>();
        foreach (var entry in new[] { Event, Ingress })
        {
            foreach (var (provider, arguments, mode) in Rows)
            {
                data.Add(entry, provider, arguments, mode);
            }
        }
        return data;
    }

    private sealed record Run(int ExitCode, string Output, string Error, ProcessIdentity Agent)
    {
        public string Message => $"exit={ExitCode}; stdout={Output}; stderr={Error}";
    }

    private static string CopyImage(TestWorkspace workspace, string provider, string folder = "image")
    {
        var directory = Directory.CreateDirectory(Path.Combine(workspace.Root, folder)).FullName;
        var image = Path.Combine(directory, provider + ".exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), image);
        return image;
    }

    private static string Hook(string entry, string provider, TestWorkspace workspace) => entry == Ingress
        ? $"\"{BuiltApplication.Ingress}\" --provider {provider} --data-dir \"{workspace.Paths.Root}\""
        : $"\"{BuiltApplication.Executable}\" event --provider {provider} --data-dir \"{workspace.Paths.Root}\"";

    // cmd.exe strips the outer quotes of a /s /c command, so the whole command is wrapped in one more pair.
    private static string Under(string arguments, string command) => $"{arguments} /s /c \"{command}\"";

    // Starts the image, pipes the payload in and always kills the whole tree, so a failure never leaves processes that lock
    // the workspace and mask the real assertion; stdout and stderr are returned for the assertion messages.
    private static async Task<Run> Launch(string image, string arguments, string payload)
    {
        var start = new ProcessStartInfo(image)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Arguments = arguments
        };
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("BELLTOWER_", StringComparison.Ordinal) || key is "WT_SESSION" or "TERM_PROGRAM").ToArray())
        {
            start.Environment.Remove(key);
        }
        using var process = Process.Start(start)!;
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            var agent = ProcessProbe.Identify(process.Id)!;
            process.StandardInput.Write(payload);
            process.StandardInput.Close();
            var exited = process.WaitForExit(20000);
            Assert.True(exited, $"The hook did not finish. Image {Path.GetFileName(image)}; arguments {arguments}");
            return new Run(process.ExitCode, (await stdout).Trim(), await stderr, agent);
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                process.WaitForExit();
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }
        }
    }

    private static void AssertRan(Run run)
    {
        Assert.True(run.ExitCode == 0, run.Message);
        Assert.True(run.Output == "{}", run.Message);
        Assert.True(run.Error.Length == 0, run.Message);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task ResolveClassifiesTheLiveAncestorCommandLine(string entry, string provider, string arguments, string expectedMode)
    {
        using var workspace = new TestWorkspace();
        var fixture = NativeFixture.SessionStart(provider);
        var image = CopyImage(workspace, provider);
        var run = await Launch(image, Under(arguments, Hook(entry, provider, workspace)), fixture.PayloadText);
        AssertRan(run);
        if (entry == Ingress)
        {
            new StateStore(workspace.Paths).Drain();
        }

        var attachments = new AttachmentStore(workspace.Paths);
        if (expectedMode is AgentModes.SharedBackend or AgentModes.Unknown)
        {
            // Anything not proven a direct client never claims the launcher's process or terminal.
            Assert.Null(attachments.Get(NativeAdoption.OwnId(provider, run.Agent)));
            var shared = attachments.Get(NativeAdoption.SharedId(provider, fixture.SessionId));
            Assert.True(shared is not null, run.Message);
            Assert.Null(shared.Process);
            Assert.Equal("shared-runtime", shared.Ownership);
            Assert.Equal(LifecycleOwners.SharedBackend, shared.Evidence!.LifecycleOwner);
            return;
        }
        var attachment = attachments.Get(NativeAdoption.OwnId(provider, run.Agent));
        Assert.True(attachment is not null, run.Message);
        Assert.Equal(run.Agent.Pid, attachment.Process!.Pid);
        Assert.Equal(expectedMode, attachment.Evidence!.Mode);
        Assert.Equal(LifecycleOwners.AgentProcess, attachment.Evidence.LifecycleOwner);
        Assert.Equal(EvidenceStatus.HookObserved, attachment.Evidence.ConversationHealth);
        if (expectedMode == AgentModes.Headless)
        {
            Assert.Equal("unknown", attachment.Host.Kind);
            Assert.False(attachment.ManagedTitle);
        }
    }

    // ANALYSIS-905: the retained Claude capture is a Claude agent nested under Codex. A hook claims only the nearest image
    // of its own provider, and a client nested under another client never claims the outer one. The inner image is started
    // through a batch file so each level keeps simple quoting.
    [Theory]
    [InlineData(Event, "app-server")]
    [InlineData(Ingress, "app-server")]
    [InlineData(Event, "")]
    [InlineData(Ingress, "")]
    public async Task AClaudeUnderCodexClaimsOnlyItsOwnProcess(string entry, string outerArguments)
    {
        using var workspace = new TestWorkspace();
        var fixture = NativeFixture.SessionStart("claude");
        var outer = CopyImage(workspace, "codex", "outer");
        var inner = CopyImage(workspace, "claude", "inner");
        var script = Path.Combine(workspace.Root, "inner.cmd");
        File.WriteAllText(script, $"@echo off\r\n\"{inner}\" {Under("-p", Hook(entry, "claude", workspace))}\r\n");
        var run = await Launch(outer, Under(outerArguments, $"\"{script}\""), fixture.PayloadText);
        AssertRan(run);
        if (entry == Ingress)
        {
            new StateStore(workspace.Paths).Drain();
        }

        var attachments = new AttachmentStore(workspace.Paths);
        Assert.Null(attachments.Get(NativeAdoption.OwnId("codex", run.Agent)));
        var claimed = attachments.List().Where(item => item.Process is not null).ToList();
        var own = Assert.Single(claimed);
        Assert.True(own.Provider == "claude", run.Message);
        Assert.NotEqual(run.Agent.Pid, own.Process!.Pid);
        Assert.Equal(AgentModes.Headless, own.Evidence!.Mode);
        Assert.Equal(LifecycleOwners.AgentProcess, own.Evidence.LifecycleOwner);
    }

    // A Codex hook above a nested claude.exe image resolves the nearest codex.exe, not the closer image of another provider.
    [Theory]
    [InlineData(Event)]
    [InlineData(Ingress)]
    public async Task ACodexHookAboveANestedClaudeImageClaimsTheNearestCodexProcess(string entry)
    {
        using var workspace = new TestWorkspace();
        var fixture = NativeFixture.SessionStart("codex");
        var outer = CopyImage(workspace, "codex", "outer");
        var middle = CopyImage(workspace, "claude", "inner");
        var script = Path.Combine(workspace.Root, "inner.cmd");
        File.WriteAllText(script, $"@echo off\r\n\"{middle}\" {Under("-p", Hook(entry, "codex", workspace))}\r\n");
        var run = await Launch(outer, Under("", $"\"{script}\""), fixture.PayloadText);
        AssertRan(run);
        if (entry == Ingress)
        {
            new StateStore(workspace.Paths).Drain();
        }

        var attachments = new AttachmentStore(workspace.Paths);
        var own = attachments.Get(NativeAdoption.OwnId("codex", run.Agent));
        Assert.True(own is not null, run.Message);
        Assert.Equal(run.Agent.Pid, own.Process!.Pid);
        Assert.Equal(AgentModes.EmbeddedRuntime, own.Evidence!.Mode);
        Assert.Single(attachments.List(), item => item.Process is not null);
    }
}
