using BellTower.Core;
using Xunit;

namespace BellTower.Tests.Core;

public sealed class ShellGrammarTests
{
    private static readonly string Cwd = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "launch"));
    private static readonly string Target = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "work tree", "DEP-1"));

    [Theory]
    [InlineData("openspec new change a")]
    [InlineData("& openspec new change a")]
    [InlineData("pwsh -Command 'openspec new change a'")]
    [InlineData("powershell -NoProfile -Command \"openspec new change a\"")]
    [InlineData("powershell.exe -NoProfile -Command 'openspec new change a'")]
    [InlineData("openspec new change a | Out-String")]
    [InlineData("openspec new change a | cat")]
    [InlineData("openspec new change a | ConvertTo-Json")]
    [InlineData("openspec new change a | head -20")]
    [InlineData("openspec new change a | head -n 20")]
    [InlineData("openspec new change a | Select-Object -First 5")]
    public void WrapperAndPipeShapesAreRecognizedWithoutADirectoryChange(string command)
    {
        var parsed = ShellGrammar.Parse(command, Cwd);
        Assert.NotNull(parsed);
        Assert.Equal(["openspec", "new", "change", "a"], parsed.Words);
        Assert.Null(parsed.WorkDirectory);
        Assert.False(parsed.Redirected);
    }

    [Theory]
    [InlineData("openspec new change a > out.txt")]
    [InlineData("openspec new change a >> 'out file.txt'")]
    [InlineData("pwsh -NoProfile -Command 'openspec new change a > out.txt'")]
    public void SingleRedirectionIsRecognizedAndMarked(string command)
    {
        var parsed = ShellGrammar.Parse(command, Cwd);
        Assert.NotNull(parsed);
        Assert.True(parsed.Redirected);
        Assert.Equal(["openspec", "new", "change", "a"], parsed.Words);
    }

    [Theory]
    [InlineData("cd \"{T}\" && openspec new change a")]
    [InlineData("cd '{T}'; openspec new change a")]
    [InlineData("Set-Location '{T}'; openspec new change a")]
    [InlineData("Set-Location -Path '{T}' && openspec new change a")]
    [InlineData("cd /d \"{T}\" && openspec new change a")]
    [InlineData("pwsh -NoProfile -Command \"Set-Location '{T}'; openspec new change a | Out-String\"")]
    [InlineData("cd \"{T}\" && openspec new change a | cat")]
    public void LiteralDirectoryChangeIsKeptAsRootEvidence(string template)
    {
        var parsed = ShellGrammar.Parse(template.Replace("{T}", Target), Cwd);
        Assert.NotNull(parsed);
        Assert.Equal(Target, parsed.WorkDirectory);
        Assert.Equal(["openspec", "new", "change", "a"], parsed.Words);
    }

    [Fact]
    public void RelativeDirectoryChangeResolvesAgainstTheHookDirectoryOnly()
    {
        var parsed = ShellGrammar.Parse("cd ../sibling && openspec new change a", Cwd);
        Assert.Equal(Path.GetFullPath(Path.Combine(Cwd, "..", "sibling")), parsed!.WorkDirectory);
        Assert.Null(ShellGrammar.Parse("cd ../sibling && openspec new change a", null));
    }

    [Theory]
    [InlineData("cd $(Get-Location) && openspec new change a")]
    [InlineData("cd `pwd` && openspec new change a")]
    [InlineData("cd $target && openspec new change a")]
    [InlineData("cd \"$HOME/x\" && openspec new change a")]
    [InlineData("openspec new change $(name)")]
    [InlineData("openspec new change `name`")]
    [InlineData("openspec new change $name")]
    [InlineData("cd /a && cd /b && openspec new change a")]
    [InlineData("cd /a; Set-Location /b; openspec new change a")]
    [InlineData("openspec new change a && echo done")]
    [InlineData("echo hi && openspec new change a")]
    [InlineData("openspec new change a || true")]
    [InlineData("openspec new change a & ")]
    [InlineData("openspec new change a 2>&1")]
    [InlineData("openspec new change a | Out-File x")]
    [InlineData("openspec new change a | cat | cat")]
    [InlineData("openspec new change a > out > again")]
    [InlineData("openspec new change a > out | cat")]
    [InlineData("(openspec new change a)")]
    [InlineData("cd ~ && openspec new change a")]
    [InlineData("cd - && openspec new change a")]
    [InlineData("cd %TEMP% && openspec new change a")]
    [InlineData("cd D:rel && openspec new change a")]
    [InlineData("cd && openspec new change a")]
    [InlineData("pwsh -File x.ps1")]
    [InlineData("pwsh -Command openspec new change a")]
    [InlineData("pwsh -Command 'openspec new change a")]
    [InlineData("openspec new change 'a")]
    public void SubstitutionVariablesMultipleChangesAndUnboundedProgramsAreRejected(string command)
    {
        Assert.Null(ShellGrammar.Parse(command, Cwd));
    }

    [Fact]
    public void OversizedAndMultilineCommandsAreRejected()
    {
        Assert.Null(ShellGrammar.Parse(new string('x', ShellGrammar.MaximumLength + 1), Cwd));
        Assert.Null(ShellGrammar.Parse("openspec new change a\nrm -rf x", Cwd));
    }
}
