using System.Diagnostics;

namespace BellTower.Setup;

public sealed record SetupAction(string Description, string Executable, IReadOnlyList<string> Arguments)
{
    private const string VsCodeFailure = "VS Code extension setup failed. File changes were backed up; rerun setup after checking the VSIX and extension permissions.";

    /// <summary>The message of a non-zero exit. The default is the VS Code extension installer's, the first action this type ran.</summary>
    public string Failure { get; init; } = VsCodeFailure;

    /// <summary>The VS Code CLI needs ELECTRON_RUN_AS_NODE; other actions do not get it.</summary>
    public bool NodeEnvironment { get; init; } = true;

    /// <summary>Runs only after the action succeeded, before the plan commits ownership (for example deleting a record the action made redundant).</summary>
    public Action? OnSuccess { get; init; }

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
        if (NodeEnvironment)
        {
            start.Environment["ELECTRON_RUN_AS_NODE"] = "1";
        }
        foreach (var argument in Arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var child = Process.Start(start) ?? throw new IOException("Could not start the setup action: " + Description);
        var output = child.StandardOutput.ReadToEndAsync();
        var error = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(45000))
        {
            throw new IOException(NodeEnvironment
                ? "VS Code extension setup has not finished. Review extensions and rerun setup; no editor process was stopped."
                : "Setup action has not finished: " + Description + ". Nothing was stopped; rerun setup.");
        }
        Task.WaitAll(output, error);
        if (child.ExitCode != 0)
        {
            throw new IOException(Failure);
        }
        OnSuccess?.Invoke();
    }
}
