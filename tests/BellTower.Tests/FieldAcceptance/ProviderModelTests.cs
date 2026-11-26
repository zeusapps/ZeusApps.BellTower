using System.Text.Json.Nodes;
using BellTower.FieldAcceptance.Harness;
using BellTower.FieldAcceptance.Isolation;
using BellTower.Protocol;
using Xunit;

namespace BellTower.Tests.FieldAcceptance;

/// <summary>The provider model builds hook events from the native captures and finds Codex's hook shell the way Codex 0.164.0 does.</summary>
public sealed class ProviderModelTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "native");
    private static readonly InstanceLayout Layout = new("upgrade", @"D:\repo\artifacts\field-acceptance\runs\1\sandbox\upgrade");

    [Theory]
    [InlineData("claude", "SessionStart")]
    [InlineData("claude", "UserPromptSubmit")]
    [InlineData("codex", "SessionStart")]
    [InlineData("codex", "UserPromptSubmit")]
    public void ThePayloadHasExactlyTheFieldsOfTheNativeCapture(string provider, string hookEvent)
    {
        var fixture = Directory.EnumerateFiles(Fixtures, provider + "-*-" + hookEvent.ToLowerInvariant() + ".json").Single();
        var native = JsonNode.Parse(File.ReadAllText(fixture))!["payload"]!.AsObject().Select(pair => pair.Key).Order(StringComparer.Ordinal).ToList();

        var built = JsonNode.Parse(ProviderModel.For(provider).Payload(hookEvent, "session-1", @"D:\repo\artifacts\field-acceptance\runs\1\sandbox\upgrade\work\main", Layout, Fixtures))!.AsObject();

        Assert.Equal(native, built.Select(pair => pair.Key).Order(StringComparer.Ordinal).ToList());
        Assert.Equal("session-1", built["session_id"]!.GetValue<string>());
        Assert.StartsWith(Layout.Root, built["transcript_path"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Layout.Root, built["cwd"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(built["turn_id"]);
    }

    [Fact]
    public void TheHarnessCopyOfTheShellRouteNameMatchesTheProduct() =>
        Assert.Equal(TrayProcess.RouteShell, HookStartRoute.Shell);

    private const string ProgramFiles = @"C:\Program Files";
    private const string SystemDir = @"C:\Windows\System32";
    private const string Pwsh = @"C:\Program Files\PowerShell\7\pwsh.exe";
    private const string WindowsPowerShell = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";

    private static HashSet<string> Existing(params string[] files) => new(files, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void PowerShell7WinsEvenWhenWindowsPowerShellComesFirstOnPath()
    {
        // The pinned Codex order searches the whole PATH for pwsh.exe before it looks for powershell.exe at all.
        var existing = Existing(Pwsh, WindowsPowerShell, @"C:\Windows\System32\cmd.exe");
        var shell = CodexShell.Resolve(@"C:\Windows\System32\WindowsPowerShell\v1.0;C:\Program Files\PowerShell\7", ProgramFiles, SystemDir, existing.Contains);
        Assert.Equal(Pwsh, shell.Executable);
        Assert.Equal(HookShell.PowerShell, shell.Kind);
        Assert.Null(shell.Note);
    }

    [Fact]
    public void ThePinnedCodexOrderIsPwshOnPathThenFixedPwshThenPowerShellOnPathThenFixedWindowsPowerShellThenCmd()
    {
        var existing = Existing(Pwsh, WindowsPowerShell, @"C:\Windows\System32\cmd.exe");

        // 2: pwsh is not on PATH but exists at its fixed path, and that wins over a powershell.exe on PATH.
        Assert.Equal(Pwsh, CodexShell.Resolve(@"C:\Windows\System32\WindowsPowerShell\v1.0", ProgramFiles, SystemDir, existing.Contains).Executable);
        Assert.Equal(Pwsh, CodexShell.Resolve(@"C:\tools", ProgramFiles, SystemDir, existing.Contains).Executable);

        // 3: without PowerShell 7 anywhere, powershell.exe on PATH.
        existing.Remove(Pwsh);
        Assert.Equal(WindowsPowerShell, CodexShell.Resolve(@"C:\Windows\System32\WindowsPowerShell\v1.0", ProgramFiles, SystemDir, existing.Contains).Executable);

        // 4: powershell.exe at its fixed path when PATH does not list it.
        Assert.Equal(WindowsPowerShell, CodexShell.Resolve(@"C:\tools", ProgramFiles, SystemDir, existing.Contains).Executable);

        // 5: the command shell.
        existing.Remove(WindowsPowerShell);
        var fallback = CodexShell.Resolve(@"C:\tools", ProgramFiles, SystemDir, existing.Contains);
        Assert.Equal(HookShell.Cmd, fallback.Kind);
        Assert.Equal(@"C:\Windows\System32\cmd.exe", fallback.Executable);
    }

    [Fact]
    public void APowerShellOutsideTheAllowedSystemOnesIsSkippedAndTheNoteNamesTheEditionCodexWouldHaveUsed()
    {
        var existing = Existing(WindowsPowerShell, @"C:\Users\someone\bin\pwsh.exe");
        var shell = CodexShell.Resolve(@"C:\Users\someone\bin", ProgramFiles, SystemDir, existing.Contains);
        Assert.Equal(WindowsPowerShell, shell.Executable);
        Assert.NotNull(shell.Note);
        Assert.Contains("PowerShell 7 (pwsh.exe)", shell.Note);
        Assert.DoesNotContain("someone", shell.Note);

        var windows = CodexShell.Resolve(@"C:\Users\someone\bin", ProgramFiles, SystemDir, Existing(@"C:\Users\someone\bin\powershell.exe", Pwsh).Contains);
        Assert.Equal(Pwsh, windows.Executable);
        Assert.Null(windows.Note);
    }

    [Fact]
    public void TheModelsOwnTheirSettingsPathsShellsAndAgentArguments()
    {
        Assert.Equal(Layout.ClaudeSettings, ProviderModel.Claude.SettingsPath(Layout));
        Assert.Equal(Layout.CodexHooks, ProviderModel.Codex.SettingsPath(Layout));
        Assert.Equal(["--no-daemon"], ProviderModel.Codex.AgentArguments);
        Assert.Empty(ProviderModel.Claude.AgentArguments);
        Assert.Equal(["claude", "codex"], ProviderModel.All.Select(model => model.Name));
        Assert.False(ProviderModel.Codex.UsesSharedBackend(["--no-daemon"]));
        Assert.True(ProviderModel.Codex.UsesSharedBackend([]));
        Assert.False(ProviderModel.Claude.UsesSharedBackend([]));
    }
}
