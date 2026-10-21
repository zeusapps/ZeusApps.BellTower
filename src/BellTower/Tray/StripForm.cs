using System.Runtime.InteropServices;

namespace BellTower.Tray;

public sealed class StripForm : Form
{
    private readonly BufferedFlowLayoutPanel panel = new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = true,
        Padding = new Padding(3),
        Margin = Padding.Empty
    };
    private readonly Dictionary<string, ChipControl> buttons = [];
    private readonly Label grip = new()
    {
        Text = "⋮",
        AutoSize = false,
        Size = new Size(18, 40),
        ForeColor = Color.Gray,
        TextAlign = ContentAlignment.MiddleCenter,
        Cursor = Cursors.SizeAll
    };
    private bool fitting;
    private IReadOnlyList<ChipView> currentChips = [];
    public event Action<string>? Acknowledge;
    public event Action<string>? Navigate;
    public event Action<StripPosition>? PositionChanged;
    private readonly ToolTip tooltip = new();
    public StripForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(24, 27, 34);
        Font = new Font("Segoe UI", 10);
        Controls.Add(panel);
        panel.Controls.Add(grip);
        grip.MouseDown += Drag;
        panel.SizeChanged += (_, _) => Refit();
        panel.MouseDown += Drag;
        MouseDown += Drag;
        Move += (_, _) =>
        {
            if (Visible)
            {
                PositionChanged?.Invoke(new(Left, Top));
            }
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var value = base.CreateParams;
            value.ExStyle |= 0x08000080;
            return value;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x21)
        {
            m.Result = new IntPtr(3);
            return;
        }

        base.WndProc(ref m);
    }

    public void UpdateChips(IReadOnlyList<ChipView> chips)
    {
        if (chips.SequenceEqual(currentChips))
        {
            return;
        }
        currentChips = chips.ToArray();
        if (chips.Count == 0)
        {
            Hide();
        }
        SuspendLayout();
        panel.SuspendLayout();
        var keys = chips.Select(chip => chip.Key).ToHashSet();
        foreach (var key in buttons.Keys.Where(key => !keys.Contains(key)).ToArray())
        {
            var removed = buttons[key];
            tooltip.SetToolTip(removed, null);
            buttons.Remove(key);
            removed.Dispose();
        }
        var index = 1;
        foreach (var chip in chips)
        {
            if (!buttons.TryGetValue(chip.Key, out var button))
            {
                button = new ChipControl(chip) { TabStop = false };
                button.MouseUp += (_, e) =>
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        Acknowledge?.Invoke(chip.Key);
                    }
                    else if (e.Button == MouseButtons.Left)
                    {
                        Navigate?.Invoke(chip.Key);
                    }
                };
                buttons.Add(chip.Key, button);
                panel.Controls.Add(button);
                _ = button.Handle;
            }
            button.UpdateChip(chip);
            if (index == 1)
            {
                grip.Height = button.Height;
                grip.Margin = button.Margin;
            }
            var details = chip.ToolTip;
            if (tooltip.GetToolTip(button) != details)
            {
                tooltip.SetToolTip(button, details);
            }
            if (panel.Controls.GetChildIndex(button) != index)
            {
                panel.Controls.SetChildIndex(button, index);
            }
            index++;
        }
        panel.ResumeLayout(true);
        ResumeLayout(true);
        if (chips.Count > 0)
        {
            Refit();
            if (!Visible)
            {
                Show();
                Refit();
            }
        }
    }

    private void Refit()
    {
        if (fitting || buttons.Count == 0 || IsDisposed)
        {
            return;
        }
        fitting = true;
        try
        {
            panel.MaximumSize = new Size(Math.Max(240, Screen.FromPoint(Location).WorkingArea.Width - 20), 0);
            var preferred = panel.PreferredSize;
            if (ClientSize != preferred)
            {
                ClientSize = preferred;
            }
            Clamp();
        }
        finally
        {
            fitting = false;
        }
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Refit();
    }

    public void Clamp()
    {
        var area = Screen.FromPoint(Location).WorkingArea;
        Location = ClampPosition(Location, Size, area);
    }

    public static Point ClampPosition(Point position, Size size, Rectangle area) => new(
        Math.Clamp(position.X, area.Left, Math.Max(area.Left, area.Right - size.Width)),
        Math.Clamp(position.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));
    private void Drag(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            tooltip.Dispose();
        }

        base.Dispose(disposing);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
