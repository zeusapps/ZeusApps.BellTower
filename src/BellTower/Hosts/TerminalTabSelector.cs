using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace BellTower.Hosts;

public sealed class TerminalTabSelector(ITerminalTabSource tabs)
{
    public bool Select(long window, string marker, Func<bool> stillOwned)
    {
        var elapsed = Stopwatch.StartNew();
        do
        {
            try
            {
                var matches = tabs.ReadTabs(window).Where(tab => tab.Name == marker).ToArray();
                if (matches.Length > 1)
                {
                    return false;
                }
                if (matches.Length == 1)
                {
                    // Resolve a live element again on every click; never retain an index or match a display name.
                    return stillOwned() && matches[0].Name == marker && matches[0].Select();
                }
            }
            catch (Exception exception) when (exception is ElementNotAvailableException or InvalidOperationException or COMException)
            {
                return false;
            }
            Thread.Sleep(40);
        } while (elapsed.ElapsedMilliseconds < 800 && stillOwned());
        return false;
    }
}
