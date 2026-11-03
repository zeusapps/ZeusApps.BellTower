using System.Diagnostics;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Cli;

public sealed class NativeSetupCliTests
{
    [Fact]
    public void SetupHelpExplainsNativeHostBridgeMode()
    {
        using var workspace = new TestWorkspace();

        var result = Run(workspace, "setup", "--host-bridge", "--help");

        Assert.Equal(0, result.Code);
        Assert.Contains("--host-bridge", result.Output);
        Assert.Contains("without terminal profiles or shell imports", result.Output);
        Assert.False(File.Exists(Path.Combine(workspace.Paths.Root, "integration.json")));
    }

    [Theory]
    [InlineData("--desktop")]
    [InlineData("--status-line")]
    [InlineData("--project", "D:/nonexistent")]
    public void HostBridgeCannotBeCombinedWithProfileOrPresentationOptions(params string[] additional)
    {
        using var workspace = new TestWorkspace();
        var home = Path.Combine(workspace.Root, "home");

        var result = Run(workspace, ["setup", "--host-bridge", "--home", home, .. additional]);

        Assert.NotEqual(0, result.Code);
        Assert.Contains("cannot be combined", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(workspace.Paths.Root, "integration.json")));
        Assert.False(Directory.Exists(home));
    }

    private static string Executable
    {
        get
        {
            var testOutput = new DirectoryInfo(AppContext.BaseDirectory);
            var directory = testOutput;
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ZeusApps.BellTower.slnx")))
            {
                directory = directory.Parent;
            }

            return Path.Combine(directory!.FullName, "src", "BellTower", "bin", testOutput.Parent!.Name, testOutput.Name, "bell-tower.exe");
        }
    }

    private static (int Code, string Output, string Error) Run(TestWorkspace workspace, params string[] args)
    {
        var start = new ProcessStartInfo(Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("--data-dir");
        start.ArgumentList.Add(workspace.Paths.Root);
        foreach (var argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000))
        {
            process.Kill();
            throw new TimeoutException("Disposable Bell Tower process timed out.");
        }

        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
}
