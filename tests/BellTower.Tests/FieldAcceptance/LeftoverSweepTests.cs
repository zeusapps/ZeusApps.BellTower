using BellTower.FieldAcceptance.Isolation;
using Xunit;

namespace BellTower.Tests.FieldAcceptance;

/// <summary>The preflight sweep selects only what the harness owns. These tests use invented task XML and paths and start nothing.</summary>
public sealed class LeftoverSweepTests
{
    private const string Base = @"D:\repo\artifacts\field-acceptance";

    private static string Task(string command, string arguments) => $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <Actions Context="Author">
            <Exec>
              <Command>{command}</Command>
              <Arguments>{arguments}</Arguments>
              <WorkingDirectory>C:\elsewhere</WorkingDirectory>
            </Exec>
          </Actions>
        </Task>
        """;

    [Fact]
    public void ATaskThatTraysADataDirectoryUnderTheHarnessArtifactsIsSelected()
    {
        var xml = Task(@"D:\repo\artifacts\field-acceptance\runs\20261125-1\sandbox\upgrade\install\bell-tower.exe",
            "tray --data-dir &quot;D:\\repo\\artifacts\\field-acceptance\\runs\\20261125-1\\sandbox\\upgrade\\data&quot;");
        Assert.True(LeftoverSweep.IsHarnessTask(xml, Base));
    }

    [Fact]
    public void AProductionTaskIsNeverSelected()
    {
        var xml = Task(@"C:\Users\someone\AppData\Local\Programs\ZeusApps.BellTower\bell-tower.exe",
            "tray --data-dir &quot;C:\\Users\\someone\\AppData\\Local\\ZeusApps.BellTower&quot;");
        Assert.False(LeftoverSweep.IsHarnessTask(xml, Base));
    }

    [Theory]
    [InlineData("tray --data-dir &quot;D:\\other\\place&quot;")]
    [InlineData("tray --data-dir &quot;D:\\repo\\artifacts\\field-acceptance-elsewhere\\data&quot;")]
    [InlineData("tray --data-dir &quot;D:\\repo\\artifacts&quot;")]
    [InlineData("start --silent --data-dir &quot;D:\\repo\\artifacts\\field-acceptance\\runs\\1\\data&quot;")]
    [InlineData("--unrelated &quot;D:\\repo\\artifacts\\field-acceptance\\runs\\1\\data&quot;")]
    [InlineData("")]
    public void AnUnrelatedTaskIsNeverSelected(string arguments) =>
        Assert.False(LeftoverSweep.IsHarnessTask(Task(@"C:\tools\backup.exe", arguments), Base));

    [Theory]
    [InlineData("")]
    [InlineData("not xml at all")]
    [InlineData("<Task><Actions></Task>")]
    public void AnUnreadableDefinitionIsNeverSelected(string xml) => Assert.False(LeftoverSweep.IsHarnessTask(xml, Base));

    [Fact]
    public void OnlyProcessesUnderTheHarnessRunsAreSelected()
    {
        var runs = Base + @"\runs";
        Assert.True(LeftoverSweep.IsHarnessImage(Base + @"\runs\1\sandbox\upgrade\install\bell-tower.exe", runs));
        Assert.True(LeftoverSweep.IsHarnessImage(Base + @"\runs\1\sandbox\upgrade\fake-bin\codex.exe", runs));
        Assert.False(LeftoverSweep.IsHarnessImage(@"C:\Users\someone\AppData\Local\Programs\ZeusApps.BellTower\bell-tower.exe", runs));
        Assert.False(LeftoverSweep.IsHarnessImage(Base + @"\baseline\bell-tower.exe", runs));
        Assert.False(LeftoverSweep.IsHarnessImage(@"D:\repo\tools\BellTower.FieldAcceptance\bin\Release\net10.0-windows\BellTower.FieldAcceptance.exe", runs));
        Assert.False(LeftoverSweep.IsHarnessImage("", runs));
    }

    [Fact]
    public void TheSweepDeletesOnlyTheSelectedTasksAndReportsEachRemoval()
    {
        var ours = Task(@"D:\repo\artifacts\field-acceptance\runs\1\sandbox\upgrade\install\bell-tower.exe", "tray --data-dir &quot;D:\\repo\\artifacts\\field-acceptance\\runs\\1\\sandbox\\upgrade\\data&quot;");
        var users = Task(@"C:\Users\someone\AppData\Local\Programs\ZeusApps.BellTower\bell-tower.exe", "tray --data-dir &quot;C:\\Users\\someone\\AppData\\Local\\ZeusApps.BellTower&quot;");
        var calls = new List<string>();
        var csv = "\"\\ZeusApps.BellTower\\HookStart-aaaa\",\"N/A\",\"Ready\"\r\n\"\\ZeusApps.BellTower\\HookStart-bbbb\",\"N/A\",\"Ready\"\r\n\"\\Other\\Task\",\"N/A\",\"Ready\"\r\n";
        (int, string) Run(string[] arguments)
        {
            calls.Add(string.Join(' ', arguments));
            return arguments switch
            {
                ["/query", "/fo", ..] => (0, csv),
                ["/query", "/tn", @"\ZeusApps.BellTower\HookStart-aaaa", "/xml"] => (0, ours),
                ["/query", "/tn", @"\ZeusApps.BellTower\HookStart-bbbb", "/xml"] => (0, users),
                ["/delete", ..] => (0, ""),
                _ => (1, "")
            };
        }
        var removed = LeftoverSweep.Run(Base, Run, int.MaxValue);
        Assert.Contains(removed, item => item.Contains("HookStart-aaaa", StringComparison.Ordinal) && item.Contains("deleted", StringComparison.Ordinal));
        Assert.DoesNotContain(removed, item => item.Contains("HookStart-bbbb", StringComparison.Ordinal));
        Assert.Contains(calls, call => call == @"/delete /tn \ZeusApps.BellTower\HookStart-aaaa /f");
        Assert.DoesNotContain(calls, call => call.StartsWith("/delete", StringComparison.Ordinal) && call.Contains("bbbb", StringComparison.Ordinal));
        Assert.DoesNotContain(calls, call => call.Contains(@"\Other\Task", StringComparison.Ordinal));
    }
}
