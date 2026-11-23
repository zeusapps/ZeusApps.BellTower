namespace BellTower.Tray;

/// <summary>
/// A short message that never takes focus and is topmost from creation, shown next to the chip strip or the Sessions window for
/// every click that needs a reason. Topmost comes from the extended style, not from the TopMost property: that property
/// re-positions without SWP_NOACTIVATE and can activate the form, taking the keyboard from the terminal the click just focused.
/// Clicking it or waiting a few seconds hides it.
/// </summary>
public sealed class NoticeForm : Form
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(8);
    public const int WsExTopmost = 0x00000008;
    public const int WsExToolWindow = 0x00000080;
    public const int WsExNoActivate = 0x08000000;
    private const int TextWidth = 420;
    private readonly Label message = new()
    {
        AutoSize = true,
        MaximumSize = new Size(TextWidth - 24, 0),
        ForeColor = Color.White,
        Padding = new Padding(0)
    };
    private readonly System.Windows.Forms.Timer hide = new() { Interval = (int)Lifetime.TotalMilliseconds };

    public NoticeForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(40, 44, 56);
        Font = new Font("Segoe UI", 10);
        Padding = new Padding(12, 10, 12, 10);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Controls.Add(message);
        message.Click += (_, _) => Dismiss();
        Click += (_, _) => Dismiss();
        hide.Tick += (_, _) => Dismiss();
    }

    protected override bool ShowWithoutActivation => true;

    /// <summary>The extended window style the notice is created with.</summary>
    public int ExtendedStyle => CreateParams.ExStyle;

    /// <summary>True when the form is created never activating (the framework reads this when showing it).</summary>
    public bool NeverActivates => ShowWithoutActivation;

    public string MessageText => message.Text;

    protected override CreateParams CreateParams
    {
        get
        {
            var value = base.CreateParams;
            value.ExStyle |= WsExTopmost | WsExToolWindow | WsExNoActivate;
            return value;
        }
    }

    /// <summary>Hides the notice and stops its timer; used by a click and by the lifetime timer.</summary>
    public void Dismiss()
    {
        hide.Stop();
        Hide();
    }

    /// <summary>The lifetime timer is running while the notice is meant to be visible.</summary>
    public bool Expiring => hide.Enabled;

    /// <summary>Shows the text below the anchor, or above it when there is no room below, inside the anchor's screen.</summary>
    public void ShowNotice(string text, Rectangle anchor)
    {
        message.Text = text;
        PerformLayout();
        var area = Screen.FromRectangle(anchor).WorkingArea;
        var size = PreferredSize;
        Location = Place(anchor, size, area);
        if (!Visible)
        {
            Show();
        }
        hide.Stop();
        hide.Start();
    }

    public static Point Place(Rectangle anchor, Size size, Rectangle area)
    {
        var below = anchor.Bottom + 6;
        var top = below + size.Height <= area.Bottom ? below : Math.Max(area.Top, anchor.Top - size.Height - 6);
        var left = Math.Clamp(anchor.Left, area.Left, Math.Max(area.Left, area.Right - size.Width));
        return new Point(left, top);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            hide.Dispose();
        }
        base.Dispose(disposing);
    }
}
