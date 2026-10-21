using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace BellTower.Hosts;

public sealed class AutomationTerminalTabSource : ITerminalTabSource
{
    public IReadOnlyList<ITerminalTab> ReadTabs(long window)
    {
        try
        {
            var root = AutomationElement.FromHandle(new IntPtr(window));
            var matches = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
            return matches.Cast<AutomationElement>().Select(element => (ITerminalTab)new AutomationTerminalTab(element)).ToArray();
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            return [];
        }
    }
}
