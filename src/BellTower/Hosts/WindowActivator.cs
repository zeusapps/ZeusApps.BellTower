namespace BellTower.Hosts;

public sealed class WindowActivator(IWindowActivationApi api)
{
    public bool Activate(IntPtr window)
    {
        // Restoring a visible maximized/fullscreen window changes its show state.
        if (api.IsMinimized(window))
        {
            api.Restore(window);
        }
        return api.SetForeground(window);
    }
}
