using System.Reflection;
using System.Runtime.ExceptionServices;
using BellTower.Core;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Tray;

// Equal native-name status and identity controls for both clients in the overview (task 4.1, intake I-11).
public sealed class OverviewControlTests
{
    private static OverviewRow Row(string provider, string status = "synchronized") => new(provider + ":one",
        new SessionState { Key = provider + ":one", Provider = provider, SessionId = "one", AttachmentId = "attachment-" + provider, Directory = @"C:\Code\D360" },
        new SessionIdentity { Key = provider + ":one", DefaultName = "D360", Palette = new("#101c27", "#4b91bb", "#000000"), NativeSyncStatus = status, CheckoutSource = "unique-work", WorkingCheckout = @"C:\Code\D360-worktrees\DEP-7070" },
        new GitContext("DEP-7070-login", "DEP-7070"), "terminal");

    private static IEnumerable<T> All<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match)
            { yield return match; }
            foreach (var nested in All<T>(child))
            { yield return nested; }
        }
    }

    private static void Click(Button button) => typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(button, [EventArgs.Empty]);

    private static void Select(OverviewForm form, int index)
    {
        var grid = All<DataGridView>(form).Single();
        grid.ClearSelection();
        grid.CurrentCell = grid.Rows[index].Cells[0];
        grid.Rows[index].Selected = true;
    }

    [Fact]
    public void NativeStatusAndWordingAreTheSameForBothClients()
    {
        Assert.StartsWith("Claude Code native name: unfinished", OverviewForm.NativeStatus(Row("claude")));
        Assert.Contains("sync-native-session-titles", OverviewForm.NativeStatus(Row("claude")));
        Assert.StartsWith("Codex native name: synchronized", OverviewForm.NativeStatus(Row("codex")));
        Assert.Contains("checkout (unique-work): C:\\Code\\D360-worktrees\\DEP-7070", OverviewForm.NativeStatus(Row("claude")));
        var pinned = Row("codex");
        pinned.Identity.CheckoutPin = @"C:\Code\Pinned";
        Assert.Contains("checkout (user): C:\\Code\\Pinned", OverviewForm.NativeStatus(pinned));
    }

    [Fact]
    public void ClaudeAndCodexRowsExposeTheSameNativeNameAndIdentityControls()
    {
        InSta(() =>
        {
            using var form = new OverviewForm { Opacity = 0 };
            _ = form.Handle;
            var native = new List<(string Key, bool Enabled)>();
            var released = new List<string>();
            form.UseBellTowerName += key => released.Add(key);
            var configure = new List<(string Key, string? Checkout, string? Color, bool Reset)>();
            form.SetNativeSync += (key, enabled) => native.Add((key, enabled));
            form.ConfigureIdentity += (key, checkout, color, reset) => configure.Add((key, checkout, color, reset));
            form.RefreshRows([Row("claude", "unavailable: supported Claude metadata API"), Row("codex", "paused: native chat name was set outside Bell Tower")], DateTimeOffset.UtcNow);

            var buttons = All<Button>(form).ToDictionary(button => button.Text);
            Assert.DoesNotContain(buttons.Keys, text => text.Contains("Codex", StringComparison.Ordinal));
            foreach (var label in new[] { "Use Bell Tower name", "Use name in native chat", "Pause native names", "Apply checkout/color", "Clear checkout", "Random color", "Reset pins", "Save", "Focus session" })
            { Assert.True(buttons.ContainsKey(label), label); }

            var texts = All<TextBox>(form).ToArray();
            // Name, change, checkout and color only: Bell Tower has no session role box.
            Assert.Equal(4, texts.Length);
            Assert.DoesNotContain(All<Label>(form), label => label.Text == "Role");
            var checkout = texts[2];
            var color = texts[3];
            foreach (var (index, key, expected) in new[] { (0, "claude:one", "Claude Code native name: unfinished"), (1, "codex:one", "Codex native name: paused") })
            {
                Select(form, index);
                Assert.StartsWith(expected, form.StatusText);
                // Claude's native title write is unfinished, so its native switches are disabled; Use Bell Tower name is shared.
                var codex = key.StartsWith("codex", StringComparison.Ordinal);
                Assert.Equal(codex, buttons["Pause native names"].Enabled);
                Assert.Equal(codex, buttons["Use name in native chat"].Enabled);
                Assert.True(buttons["Use Bell Tower name"].Enabled);
                if (codex)
                {
                    Click(buttons["Pause native names"]);
                    Click(buttons["Use name in native chat"]);
                }
                Click(buttons["Use Bell Tower name"]);
                Assert.Equal(key, released[^1]);
                checkout.Text = @"C:\Code\D360-worktrees\DEP-7070";
                color.Text = "red";
                Click(buttons["Apply checkout/color"]);
                Click(buttons["Clear checkout"]);
                Click(buttons["Random color"]);
                Click(buttons["Reset pins"]);
                if (codex)
                { Assert.Equal(new[] { (key, false), (key, true) }, native.TakeLast(2).ToArray()); }
                else
                { Assert.DoesNotContain(native, item => item.Key == key); }
                Assert.Equal((key, @"C:\Code\D360-worktrees\DEP-7070", "red", false), configure[^4]);
                Assert.Equal((key, "", null, false), configure[^3]);
                Assert.Equal((key, null, "random", false), configure[^2]);
                Assert.Equal((key, null, "random", true), configure[^1]);
            }
        });
    }

    private static void InSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            { action(); }
            catch (Exception error) { failure = error; }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Form check timed out.");
        if (failure is not null)
        { ExceptionDispatchInfo.Capture(failure).Throw(); }
    }
}
