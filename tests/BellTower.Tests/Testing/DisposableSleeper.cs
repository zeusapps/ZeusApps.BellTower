using System.Diagnostics;

namespace BellTower.Tests.Testing;

// A live stand-in agent process. It is ping.exe itself (no cmd.exe parent that could be killed on its own) and disposal
// kills the whole tree, so nothing outlives the test or holds its inherited handles (ANALYSIS-901).
public sealed class DisposableSleeper : IDisposable
{
    private readonly Process process;

    private DisposableSleeper(Process process) => this.process = process;

    public int Id => process.Id;

    public static DisposableSleeper Start()
    {
        var process = Process.Start(new ProcessStartInfo("ping.exe", "-n 60 127.0.0.1")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return new DisposableSleeper(process);
    }

    public void WaitForExit() => process.WaitForExit();

    public void Kill()
    {
        try
        {
            if (!process.HasExited)
            { process.Kill(entireProcessTree: true); }
            process.WaitForExit();
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
    }

    public void Dispose()
    {
        Kill();
        process.Dispose();
    }
}
