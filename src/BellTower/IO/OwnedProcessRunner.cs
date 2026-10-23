using System.Diagnostics;

namespace BellTower.IO;

// Owns only processes it starts. An unreaped child blocks further launches.
public sealed class OwnedProcessRunner : IDisposable
{
    private Process? active;
    public int LaunchCount { get; private set; }
    public int? ActiveProcessId => active is { HasExited: false } child ? child.Id : null;

    public async Task<bool> RunAsync(ProcessStartInfo start, string input, TimeSpan timeout, CancellationToken cancellation)
    {
        Reap();
        cancellation.ThrowIfCancellationRequested();
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.WindowStyle = ProcessWindowStyle.Hidden;
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        active = Process.Start(start) ?? throw new IOException("owned-helper-start-failed");
        LaunchCount++;
        var child = active;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(timeout);
        var output = child.StandardOutput.BaseStream.CopyToAsync(Stream.Null, deadline.Token);
        var error = child.StandardError.BaseStream.CopyToAsync(Stream.Null, deadline.Token);
        try
        {
            await child.StandardInput.WriteAsync(input.AsMemory(), deadline.Token).ConfigureAwait(false);
            child.StandardInput.Close();
            await child.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            await Task.WhenAll(output, error).WaitAsync(deadline.Token).ConfigureAwait(false);
            return child.ExitCode == 0;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            Reap();
            try
            {
                await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or IOException or TimeoutException) { }
        }
    }

    private void Reap()
    {
        if (active is not { } child)
        {
            return;
        }
        if (!child.HasExited)
        {
            try
            {
                child.Kill(entireProcessTree: false);
            }
            catch (InvalidOperationException) when (child.HasExited) { }
            if (!child.WaitForExit(2000))
            {
                throw new IOException("owned-helper-exit-unverified");
            }
        }
        child.Dispose();
        active = null;
    }

    public void Dispose() => Reap();
}
