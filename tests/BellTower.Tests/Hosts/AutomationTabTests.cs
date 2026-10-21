using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using BellTower.Hosts;
using Xunit;
using TabControl = System.Windows.Controls.TabControl;

namespace BellTower.Tests.Hosts;

public sealed class AutomationTabTests
{
    [Fact]
    public async Task NativeAutomationSelectsAHiddenDisposableTabWithoutKeyboardInput()
    {
        var ready = new TaskCompletionSource<(Window Form, TabControl Tabs, long Handle)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var marker = "BellTower-" + Guid.NewGuid().ToString("N");
        var thread = new Thread(() =>
        {
            try
            {
                var form = new Window { Opacity = 0, Width = 500, Height = 300, ShowActivated = false, ShowInTaskbar = false };
                var tabs = new TabControl();
                tabs.Items.Add(new TabItem { Header = "Other session" });
                tabs.Items.Add(new TabItem { Header = marker });
                form.Content = tabs;
                form.Loaded += (_, _) => ready.TrySetResult((form, tabs, new WindowInteropHelper(form).Handle.ToInt64()));
                form.Closed += (_, _) => form.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                form.Show();
                Dispatcher.Run();
            }
            catch (Exception exception)
            {
                ready.TrySetException(exception);
            }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        var fixture = await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var handle = fixture.Handle;
            var source = new AutomationTerminalTabSource();
            var discovered = source.ReadTabs(handle);
            Assert.NotEmpty(discovered);
            Assert.Contains(discovered, tab => tab.Name == marker);
            Assert.True(new TerminalTabSelector(source).Select(handle, marker, () => true), "Discovered names: " + string.Join(", ", discovered.Select(tab => tab.Name)));
            var selected = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = fixture.Form.Dispatcher.BeginInvoke(() => selected.SetResult(fixture.Tabs.SelectedIndex));
            Assert.Equal(1, await selected.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            _ = fixture.Form.Dispatcher.BeginInvoke(() => fixture.Form.Close());
            Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        }
    }
}
