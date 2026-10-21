using System.Drawing.Drawing2D;

namespace BellTower.Tray;

public sealed class ChipControl : Control
{
    private ChipView chip;
    private ChipLayout? layout;
    public string Key => chip.Key;
    public ChipControl(ChipView chip)
    {
        this.chip = chip;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.ResizeRedraw, true);
        AccessibleRole = AccessibleRole.PushButton;
        UpdateIdentity();
    }

    public void UpdateChip(ChipView value)
    {
        if (chip == value)
        {
            return;
        }
        var previousElapsed = chip.Elapsed;
        var previousName = chip.Name;
        var previousReason = chip.Reason;
        chip = value;
        UpdateIdentity();
        if (previousElapsed.Length != chip.Elapsed.Length || previousName != chip.Name || previousReason != chip.Reason)
        {
            MeasureLayout();
        }
        Invalidate();
    }

    private void UpdateIdentity()
    {
        Text = chip.Name;
        AccessibleName = string.Join(", ", new[] { chip.Name, chip.Details, chip.Reason, chip.Elapsed }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        MeasureLayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (IsHandleCreated)
        {
            MeasureLayout();
        }
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        MeasureLayout();
    }

    private void MeasureLayout()
    {
        using var graphics = CreateGraphics();
        layout = ChipLayout.Measure(graphics, Font, DeviceDpi, chip.Elapsed, chip.Name, chip.Reason);
        Size = layout.Size;
        Margin = new Padding((int)Math.Round(3 * DeviceDpi / 96d));
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Color.FromArgb(24, 27, 34));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = layout ?? ChipLayout.Measure(e.Graphics, Font, DeviceDpi, chip.Elapsed, chip.Name, chip.Reason);
        using var path = new GraphicsPath();
        var d = bounds.Radius;
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(Width - d - 1, 0, d, d, 270, 90);
        path.AddArc(Width - d - 1, Height - d - 1, d, d, 0, 90);
        path.AddArc(0, Height - d - 1, d, d, 90, 90);
        path.CloseFigure();
        using var brush = new SolidBrush(ColorTranslator.FromHtml(chip.Palette.Accent));
        e.Graphics.FillPath(brush, path);
        var foreground = ColorTranslator.FromHtml(chip.Palette.Foreground);
        TextRenderer.DrawText(e.Graphics, chip.Name, Font, bounds.Name, foreground,
            ChipLayout.TextFlags | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, chip.Reason, Font, bounds.Reason, foreground,
            ChipLayout.TextFlags | TextFormatFlags.EndEllipsis);
        foreach (var separator in new[] { bounds.NameSeparator, bounds.TimeSeparator })
        {
            if (separator.Width > 0)
            {
                TextRenderer.DrawText(e.Graphics, "·", Font, separator, foreground,
                    ChipLayout.TextFlags | TextFormatFlags.HorizontalCenter);
            }
        }
        if (chip.Elapsed.Length > 0)
        {
            TextRenderer.DrawText(e.Graphics, chip.Elapsed, Font, bounds.Elapsed, foreground,
                ChipLayout.TextFlags | TextFormatFlags.Right);
        }
    }
}
