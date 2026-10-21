using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace BellTower.Hosts;

public sealed class AutomationTerminalTab(AutomationElement element) : ITerminalTab
{
    public string Name => element.Current.Name;

    public bool Select()
    {
        try
        {
            if (!element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern))
            {
                return false;
            }
            var selection = (SelectionItemPattern)pattern;
            selection.Select();
            return selection.Current.IsSelected;
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or ElementNotEnabledException or InvalidOperationException or COMException)
        {
            return false;
        }
    }
}
