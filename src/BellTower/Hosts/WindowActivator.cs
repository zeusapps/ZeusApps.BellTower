namespace BellTower.Hosts;

public sealed class WindowActivator(IWindowActivationApi api)
{
    public const int ConfirmationAttempts = 20;
    public static readonly TimeSpan ConfirmationStep = TimeSpan.FromMilliseconds(25);

    public bool Activate(IntPtr window)
    {
        // Restoring a visible maximized/fullscreen window changes its show state.
        if (api.IsMinimized(window))
        {
            api.Restore(window);
        }
        return api.SetForeground(window);
    }

    /// <summary>
    /// Restores only a minimized window, requests the foreground and reports success only when Windows
    /// actually made the window the foreground window. The SetForegroundWindow return value is not proof.
    /// </summary>
    public async Task<bool> ActivateConfirmedAsync(IntPtr window, Func<TimeSpan, Task>? delay = null)
    {
        delay ??= step => Task.Delay(step);
        Activate(window);
        for (var attempt = 0; attempt < ConfirmationAttempts; attempt++)
        {
            if (api.IsForeground(window))
            {
                return true;
            }

            await delay(ConfirmationStep).ConfigureAwait(false);
        }

        return api.IsForeground(window);
    }

    public bool IsForeground(IntPtr window) => api.IsForeground(window);
}
