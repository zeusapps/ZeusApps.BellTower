using System.Diagnostics;
using System.Runtime.InteropServices;
using BellTower.Core;
using BellTower.IO;

namespace BellTower.Hosts;

public sealed class WindowsTerminalNavigator(DataPaths paths) : ITerminalNavigator
{
    private readonly WindowActivator activation = new(new NativeWindowActivationApi());
    public string HostKind => "windows-terminal";
    public string Precision(Attachment attachment) => !IsAvailable(attachment.Host) ? "unavailable" : attachment.ManagedTitle ? "tab or window" : "window only";
    public async Task<FocusResult> FocusAsync(Attachment attachment)
    {
        if (!IsAvailable(attachment.Host))
        {
            return new(false, "unavailable", "The owning Windows Terminal window cannot be verified.");
        }
        // Keep foreground activation in the process that received the user's click.
        if (!activation.Activate(new IntPtr(attachment.Host.WindowHandle)))
        {
            return new(false, "window only", "Windows declined foreground activation.");
        }
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "focus-terminal", "--attachment", attachment.Id, "--data-dir", paths.Root })
        {
            start.ArgumentList.Add(argument);
        }
        using var child = Process.Start(start);
        if (child is null)
        {
            return FocusWindow(attachment.Host);
        }
        var output = child.StandardOutput.ReadToEndAsync();
        var errors = child.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        try
        {
            await child.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await errors.ConfigureAwait(false);
            return System.Text.Json.JsonSerializer.Deserialize<FocusResult>(await output.ConfigureAwait(false), IO.Json.Options)
                ?? FocusWindow(attachment.Host);
        }
        catch (OperationCanceledException)
        {
            // Only the disposable helper is stopped. UI Automation cannot block the tray indefinitely.
            if (!child.HasExited)
            {
                child.Kill();
            }
            RepairTitle(attachment);
            new LocalLog(paths).Write("terminal-selection-timeout");
            return FocusWindow(attachment.Host);
        }
        catch (System.Text.Json.JsonException)
        {
            return FocusWindow(attachment.Host);
        }
    }

    private void RepairTitle(Attachment attachment)
    {
        var session = new StateStore(paths).Read().Sessions.Values.Where(session => session.AttachmentId == attachment.Id && !session.Ended)
            .MaxBy(session => session.LastObserved);
        if (session is null)
        {
            return;
        }
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var argument in new[] { "sync-title", "--session", session.Key, "--data-dir", paths.Root })
        {
            start.ArgumentList.Add(argument);
        }
        Process.Start(start)?.Dispose();
    }

    private FocusResult FocusWindow(HostTarget host)
    {
        if (!IsAvailable(host))
        {
            return new(false, "unavailable", "The owning Windows Terminal window cannot be verified.");
        }

        var handle = new IntPtr(host.WindowHandle);
        return activation.Activate(handle)
            ? new(true, "window only", "Windows Terminal focused; select the session's tab or pane.")
            : new(false, "window only", "Windows declined foreground activation.");
    }

    public static bool IsAvailable(HostTarget host)
    {
        if (host.WindowHandle == 0 || host.WindowProcess is not { } identity || ProcessProbe.Identify(identity.Pid) != identity)
        {
            return false;
        }

        var handle = new IntPtr(host.WindowHandle);
        if (!IsWindow(handle) || WindowProbe.SingleVisibleWindow(identity.Pid) != host.WindowHandle)
        {
            return false;
        }

        GetWindowThreadProcessId(handle, out var pid);
        if (pid != identity.Pid)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(identity.Pid);
            return process.ProcessName.Equals("WindowsTerminal", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint pid);
}
