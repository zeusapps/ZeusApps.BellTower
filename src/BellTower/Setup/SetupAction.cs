using System.Diagnostics;

namespace BellTower.Setup;

public sealed record SetupAction(string Description, string Executable, IReadOnlyList<string> Arguments)
{
    public void Apply()
    {
        var start = new ProcessStartInfo(Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.Environment["ELECTRON_RUN_AS_NODE"] = "1";
        foreach (var argument in Arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var child = Process.Start(start) ?? throw new IOException("Could not start the VS Code extension installer.");
        var output = child.StandardOutput.ReadToEndAsync();
        var error = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(45000))
        {
            throw new IOException("VS Code extension setup has not finished. Review extensions and rerun setup; no editor process was stopped.");
        }
        Task.WaitAll(output, error);
        if (child.ExitCode != 0)
        {
            throw new IOException("VS Code extension setup failed. File changes were backed up; rerun setup after checking the VSIX and extension permissions.");
        }
    }
}
