using System.Diagnostics;
using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Hosts;

// Task 2.5 / design decision 7: the command-line mode is cross-checked with real process relationships. Stand-in images are
// copies of cmd.exe named like the provider client (as in NativeAgentResolveProcessTests); no provider is launched. Nested
// rows put one image under another through a batch file (cmd.exe runs the batch in-process, so the inner image's parent
// is the outer image). Held rows keep the agent alive with a same-image child beside the hook. Each row runs under one
// entry point (alternating); the representative rows and the mixed row run under both.
public sealed class NativeAgentRelationshipProcessTests
{
    private const string Event = NativeAgentResolveProcessTests.Event;
    private const string Ingress = NativeAgentResolveProcessTests.Ingress;

    // Nested: outer image provider and arguments, inner (hooked) image provider and arguments, and the resulting outcome.
    // Outcome: a mode with "own:" for a claimed direct process, or "shared:" for the fail-closed shared runtime.
    private static readonly (string Hook, string OuterProvider, string OuterArguments, string InnerArguments, string Outcome)[] Nested =
    [
        // A backend started by a Codex client is a verified shared backend.
        ("codex", "codex", "", "app-server", "shared:shared-backend"),
        ("codex", "codex", "\"fixture prompt\"", "app-server", "shared:shared-backend"),
        // A backend started by a backend, a headless run, or any other process is not.
        ("codex", "codex", "app-server", "app-server", "shared:unknown"),
        ("codex", "codex", "exec", "app-server", "shared:unknown"),
        ("codex", "claude", "", "app-server", "shared:unknown"),
        // A client started by a Codex process sits where a backend sits, including a renamed backend subcommand.
        ("codex", "codex", "", "", "shared:unknown"),
        ("codex", "codex", "", "serve", "shared:unknown"),
        ("codex", "codex", "app-server", "serve", "shared:unknown"),
        ("codex", "codex", "", "\"fixture prompt\"", "shared:unknown"),
        // Not contradicted: an embedded client under another provider's process or a headless run under Codex.
        ("codex", "claude", "", "", "own:embedded-runtime"),
        ("codex", "codex", "", "exec", "own:headless"),
        // Claude has no shared backend and no relationship rule, so the same positions keep their classification.
        ("claude", "claude", "", "", "own:interactive-client"),
        ("claude", "claude", "-p", "", "own:interactive-client"),
        ("claude", "claude", "", "-p", "own:headless")
    ];

    // Both entry points call the same AttachmentCapture.Resolve, so each row runs under one entry point (alternating) and one
    // representative row per rule runs under both: R1 (backend under a client), R2 (client under a Codex process) and Claude
    // unchanged. The Claude-under-Codex positions are covered by the Resolve rows (NativeAgentResolveProcessTests).
    private static readonly HashSet<int> NestedBoth = [0, 5, 11];
    private static readonly HashSet<int> HeldBoth = [0, 5];

    private static IEnumerable<string> Entries(int index, HashSet<int> both) =>
        both.Contains(index) ? [Event, Ingress] : [index % 2 == 0 ? Event : Ingress];

    public static TheoryData<string, string, string, string, string, string> NestedRows()
    {
        var data = new TheoryData<string, string, string, string, string, string>();
        for (var index = 0; index < Nested.Length; index++)
        {
            var (hook, outerProvider, outerArguments, innerArguments, outcome) = Nested[index];
            foreach (var entry in Entries(index, NestedBoth))
            {
                data.Add(entry, hook, outerProvider, outerArguments, innerArguments, outcome);
            }
        }
        return data;
    }

    // Held: the agent image, its arguments, the provider and arguments of a same-image child that stays alive beside the hook.
    private static readonly (string Provider, string AgentArguments, string ChildArguments, string Outcome)[] Held =
    [
        // A client that hosts a backend or other clients is contradicted.
        ("codex", "", "app-server", "shared:unknown"),
        ("codex", "", "", "shared:unknown"),
        ("codex", "", "serve", "shared:unknown"),
        ("codex", "\"fixture prompt\"", "app-server", "shared:unknown"),
        // A headless child or the absence of children contradicts nothing.
        ("codex", "", "exec", "own:embedded-runtime"),
        // Claude keeps its classification with the same children.
        ("claude", "", "", "own:interactive-client"),
        ("claude", "", "-p", "own:interactive-client"),
        ("claude", "-p", "", "own:headless")
    ];

    public static TheoryData<string, string, string, string, string> HeldRows()
    {
        var data = new TheoryData<string, string, string, string, string>();
        for (var index = 0; index < Held.Length; index++)
        {
            var (provider, agentArguments, childArguments, outcome) = Held[index];
            foreach (var entry in Entries(index, HeldBoth))
            {
                data.Add(entry, provider, agentArguments, childArguments, outcome);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(NestedRows))]
    public async Task ModeIsCrossCheckedWithTheParentProcess(string entry, string provider, string outerProvider, string outerArguments, string innerArguments, string outcome)
    {
        using var workspace = new TestWorkspace();
        var fixture = NativeFixture.SessionStart(provider);
        var outer = StandIns.CopyImage(workspace, outerProvider, "outer");
        var inner = StandIns.CopyImage(workspace, provider, "inner");
        var script = Path.Combine(workspace.Root, "inner.cmd");
        File.WriteAllText(script, $"@echo off\r\n\"{inner}\" {StandIns.Under(innerArguments, StandIns.Hook(entry, provider, workspace))}\r\n");
        var run = await NativeAgentResolveProcessTests.Launch(outer, StandIns.Under(outerArguments, $"\"{script}\""), fixture.PayloadText);
        NativeAgentResolveProcessTests.AssertRan(run);
        Verify(workspace, entry, provider, fixture, outcome, run.Agent.Pid, run.Message);
    }

    [Theory]
    [MemberData(nameof(HeldRows))]
    public async Task ModeIsCrossCheckedWithTheChildProcesses(string entry, string provider, string agentArguments, string childArguments, string outcome)
    {
        using var workspace = new TestWorkspace();
        var fixture = NativeFixture.SessionStart(provider);
        var run = await LaunchWithChild(workspace, entry, provider, agentArguments, childArguments, fixture.PayloadText, "held");
        Assert.True(run.Output == "{}" && run.Error.Length == 0, run.Message);
        Verify(workspace, entry, provider, fixture, outcome, 0, run.Message);
    }

    // A Codex backend and client and a Claude client run at the same moment against one data folder: each is judged by its
    // own relationships, none disturbs another's conversation, and no one claims another's process.
    [Theory]
    [InlineData(Event)]
    [InlineData(Ingress)]
    public async Task MixedSimultaneousSessionsAreJudgedIndependently(string entry)
    {
        using var workspace = new TestWorkspace();
        var codex = NativeFixture.SessionStart("codex");
        var claude = NativeFixture.SessionStart("claude");
        var backendPayload = codex.PayloadText.Replace(codex.SessionId, "mixed-backend-session", StringComparison.Ordinal);
        var clientPayload = codex.PayloadText.Replace(codex.SessionId, "mixed-client-session", StringComparison.Ordinal);

        Task<NativeAgentResolveProcessTests.Run> Nest(string folder, string provider, string outerProvider, string outerArguments, string innerArguments, string payload)
        {
            var outer = StandIns.CopyImage(workspace, outerProvider, folder + "-outer");
            var inner = StandIns.CopyImage(workspace, provider, folder + "-inner");
            var script = Path.Combine(workspace.Root, folder + ".cmd");
            File.WriteAllText(script, $"@echo off\r\n\"{inner}\" {StandIns.Under(innerArguments, StandIns.Hook(entry, provider, workspace))}\r\n");
            return NativeAgentResolveProcessTests.Launch(outer, StandIns.Under(outerArguments, $"\"{script}\""), payload);
        }

        var runs = await Task.WhenAll(
            Nest("backend", "codex", "codex", "", "app-server", backendPayload),
            Nest("client", "codex", "codex", "", "", clientPayload),
            Nest("claude", "claude", "claude", "", "", claude.PayloadText));
        foreach (var run in runs)
        {
            NativeAgentResolveProcessTests.AssertRan(run);
        }

        new StateStore(workspace.Paths).Drain();
        var attachments = new AttachmentStore(workspace.Paths);
        var backend = attachments.Get(NativeAdoption.SharedId("codex", "mixed-backend-session"));
        Assert.True(backend is not null, runs[0].Message);
        Assert.Null(backend.Process);
        Assert.Equal(AgentModes.SharedBackend, backend.Evidence!.Mode);
        var client = attachments.Get(NativeAdoption.SharedId("codex", "mixed-client-session"));
        Assert.True(client is not null, runs[1].Message);
        Assert.Null(client.Process);
        Assert.Equal(AgentModes.Unknown, client.Evidence!.Mode);
        var own = Assert.Single(attachments.List(), item => item.Process is not null);
        Assert.Equal("claude", own.Provider);
        Assert.Equal(AgentModes.InteractiveClient, own.Evidence!.Mode);
        Assert.NotEqual(runs[2].Agent.Pid, own.Process!.Pid);
        var sessions = new StateStore(workspace.Paths).Drain().Sessions;
        foreach (var key in new[] { "codex:mixed-backend-session", "codex:mixed-client-session", "claude:" + claude.SessionId })
        {
            // A claimed direct agent has exited by now, so only the process-less conversations are still live.
            Assert.True(key.StartsWith("claude:", StringComparison.Ordinal) || !sessions[key].Ended);
            Assert.Empty(sessions[key].Waits);
        }
    }

    private static void Verify(TestWorkspace workspace, string entry, string provider, NativeFixture fixture, string outcome, int excludedPid, string message)
    {
        var (kind, mode) = outcome.Split(':') is [var first, var second] ? (first, second) : throw new ArgumentException(outcome);
        var state = new StateStore(workspace.Paths).Drain();
        var attachments = new AttachmentStore(workspace.Paths);
        var claimed = attachments.List().Where(item => item.Provider == provider && item.Process is not null).ToList();
        // Tracking never depends on the mode: the conversation is live with no invented wait either way.
        var session = state.Sessions[provider + ":" + fixture.SessionId];
        Assert.True(kind == "own" || !session.Ended);
        Assert.Empty(session.Waits);
        if (kind == "shared")
        {
            Assert.True(claimed.Count == 0, message);
            var shared = attachments.Get(NativeAdoption.SharedId(provider, fixture.SessionId));
            Assert.True(shared is not null, message);
            Assert.Null(shared.Process);
            Assert.Equal("unknown", shared.Host.Kind);
            Assert.Null(shared.Host.Shell);
            Assert.False(shared.ManagedTitle);
            Assert.Equal("shared-runtime", shared.Ownership);
            Assert.Equal(mode, shared.Evidence!.Mode);
            Assert.Equal(EvidenceStatus.HookObserved, shared.Evidence.ConversationHealth);
            return;
        }
        var own = Assert.Single(claimed);
        Assert.NotEqual(excludedPid, own.Process!.Pid);
        Assert.Equal(mode, own.Evidence!.Mode);
        Assert.Equal(LifecycleOwners.AgentProcess, own.Evidence.LifecycleOwner);
        Assert.Equal(EvidenceStatus.HookObserved, own.Evidence.ConversationHealth);
    }

    // The agent image runs a batch that starts a same-image child (output and input detached so no pipe is held), runs the
    // hook, writes a marker and then stays alive until the test has seen the marker and kills the whole tree.
    private static async Task<NativeAgentResolveProcessTests.Run> LaunchWithChild(
        TestWorkspace workspace, string entry, string provider, string agentArguments, string childArguments, string payload, string folder)
    {
        var agent = StandIns.CopyImage(workspace, provider, folder + "-agent");
        var child = StandIns.CopyImage(workspace, provider, folder + "-child");
        var marker = Path.Combine(workspace.Root, folder + ".done");
        var script = Path.Combine(workspace.Root, folder + ".cmd");
        File.WriteAllText(script,
            "@echo off\r\n" +
            $"start \"\" /b \"{child}\" {childArguments} /s /c \"ping -n 60 127.0.0.1 >nul\" >nul 2>&1 <nul\r\n" +
            StandIns.Hook(entry, provider, workspace) + "\r\n" +
            $"echo done>\"{marker}\"\r\n" +
            "ping -n 60 127.0.0.1 >nul\r\n");
        var start = new ProcessStartInfo(agent)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Arguments = StandIns.Under(agentArguments, $"\"{script}\"")
        };
        StandIns.ScrubEnvironment(start);
        using var process = Process.Start(start)!;
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            var identity = ProcessProbe.Identify(process.Id)!;
            process.StandardInput.Write(payload);
            process.StandardInput.Close();
            var finished = await StandIns.WaitFor(marker, process);
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            var output = (await stdout).Trim();
            var error = await stderr;
            Assert.True(finished, $"The hook did not finish. Agent arguments {agentArguments}; child arguments {childArguments}; stdout={output}; stderr={error}");
            return new NativeAgentResolveProcessTests.Run(0, output, error, identity);
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
}
