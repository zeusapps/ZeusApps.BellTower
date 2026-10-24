using System.Diagnostics;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Cli;

// CLI controls mean the same for Claude Code and Codex (task 4.1, intake I-11); disposable data directory only.
public sealed class IdentityControlCliTests
{
    private static (int Code, string Output, string Error) Run(TestWorkspace workspace, params string[] args)
    {
        var info = new ProcessStartInfo(BuiltApplication.Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.ArgumentList.Add("--data-dir");
        info.ArgumentList.Add(workspace.Paths.Root);
        foreach (var argument in args)
        { info.ArgumentList.Add(argument); }
        info.Environment.Remove("BELLTOWER_ATTACHMENT");
        using var process = Process.Start(info)!;
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill();
            throw new TimeoutException("Disposable Bell Tower process timed out.");
        }
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    private static string? Prop(JsonElement row, string name) => row.EnumerateObject()
        .Single(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)).Value.GetString();

    private static void Register(TestWorkspace workspace, string provider, string directory) =>
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Permission) with
        { Provider = provider, SessionId = "one", AttachmentId = "attachment-" + provider, Directory = directory });

    [Fact]
    public void CheckoutColorAndResetControlsBehaveIdenticallyForBothClients()
    {
        using var workspace = new TestWorkspace();
        var family = WorktreeFamily.Create(workspace.Root);
        var seven = family.Add("DEP-7070", "login");
        foreach (var provider in new[] { "claude", "codex" })
        { Register(workspace, provider, family.Main); }
        new StateStore(workspace.Paths).Drain();
        var store = new IdentityStore(workspace.Paths);
        foreach (var provider in new[] { "claude", "codex" })
        {
            var key = provider + ":one";
            Assert.Equal(0, Run(workspace, "rename", "--session", key, "--name", "Mine", "--change", "pinned", "--role", "PM").Code);
            // --role is deprecated: accepted and ignored, so no role reaches the name or the saved record.
            Assert.Null(store.Get(key, family.Main).Role);
            Assert.Equal("Mine", store.Get(key, family.Main).Name);
            Assert.Equal(0, Run(workspace, "rename", "--session", key, "--checkout", seven).Code);
            Assert.True(WorktreeFamily.Same(seven, store.Get(key, family.Main).CheckoutPin));
            Assert.Equal(0, Run(workspace, "rename", "--session", key, "--color", "#12ab34").Code);
            var colored = store.Get(key, family.Main);
            Assert.Equal("#12ab34", colored.Palette.Accent);
            Assert.Equal("#12ab34", colored.ColorPin);
            Assert.Equal(0, Run(workspace, "rename", "--session", key, "--color", "random").Code);
            Assert.Null(store.Get(key, family.Main).ColorPin);
            Assert.Equal(0, Run(workspace, "rename", "--session", key, "--color", "blue").Code);
            Assert.Equal("#438bd1", store.Get(key, family.Main).Palette.Accent);
            var rejected = Run(workspace, "rename", "--session", key, "--color", "nonsense");
            Assert.NotEqual(0, rejected.Code);
            Assert.Equal("#438bd1", store.Get(key, family.Main).Palette.Accent);
            Assert.Equal(0, Run(workspace, "rename", "--session", key, "--checkout", "").Code);
            Assert.Null(store.Get(key, family.Main).CheckoutPin);
            Assert.NotEqual(0, Run(workspace, "rename", "--session", key, "--checkout", workspace.Root + Path.DirectorySeparatorChar + "missing").Code);
            Assert.Equal(0, Run(workspace, "rename", "--session", key, "--checkout", seven, "--reset").Code);
            var reset = store.Get(key, family.Main);
            Assert.Null(reset.ManualName);
            Assert.Null(reset.Change);
            Assert.Null(reset.CheckoutPin);
            Assert.Null(reset.ColorPin);
            Assert.Null(reset.Role);
        }
    }

    [Fact]
    public void NativeNameControlsAndListingAreAvailableForBothClients()
    {
        using var workspace = new TestWorkspace();
        foreach (var provider in new[] { "claude", "codex" })
        { Register(workspace, provider, workspace.Root); }
        new StateStore(workspace.Paths).Drain();
        {
            var disabled = Run(workspace, "native-sync", "--session", "codex:one", "--mode", "disable");
            Assert.Equal(0, disabled.Code);
            Assert.Equal("disabled", disabled.Output.Trim());
            // Enabling takes ownership; with no verified provider target the status is an honest unavailable diagnostic.
            var enabled = Run(workspace, "native-sync", "--session", "codex:one", "--mode", "enable");
            Assert.Equal(0, enabled.Code);
            Assert.StartsWith("unavailable", enabled.Output.Trim());
            // Claude has no native write yet: the switches refuse instead of pretending to work.
            foreach (var mode in new[] { "enable", "disable" })
            {
                var refused = Run(workspace, "native-sync", "--session", "claude:one", "--mode", mode);
                Assert.NotEqual(0, refused.Code);
                Assert.Contains("sync-native-session-titles", refused.Output + refused.Error);
            }
            // The shared "use Bell Tower name" control works for both clients.
            foreach (var provider in new[] { "claude", "codex" })
            { Assert.Equal(0, Run(workspace, "rename", "--session", provider + ":one", "--use-bell-tower-name").Code); }
        }
        var list = JsonDocument.Parse(Run(workspace, "list").Output).RootElement;
        var rows = list.EnumerateArray().ToDictionary(row => Prop(row, "Key")!);
        Assert.Equal(2, rows.Count);
        Assert.Equal("pending", Prop(rows["codex:one"], "NativeNameSynchronization"));
        Assert.StartsWith("unfinished", Prop(rows["claude:one"], "NativeNameSynchronization"));
        foreach (var row in rows.Values)
        {
            Assert.False(string.IsNullOrEmpty(Prop(row, "NativePresentationName")));
            Assert.False(string.IsNullOrEmpty(Prop(row, "Checkout")));
            Assert.False(string.IsNullOrEmpty(Prop(row, "CheckoutSource")));
        }
        Assert.Contains("native", Run(workspace, "native-sync", "--help").Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Codex chat-name", Run(workspace, "native-sync", "--help").Output);
    }
}
