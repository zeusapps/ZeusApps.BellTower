namespace BellTower.Tray;

public sealed record ChipLayout(Size Size, Rectangle Name, Rectangle Reason, Rectangle Elapsed, int Radius,
    Rectangle NameSeparator, Rectangle TimeSeparator)
{
    public const TextFormatFlags TextFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
        TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsClipping;

    public static ChipLayout Measure(IDeviceContext graphics, Font font, int dpi, string elapsed, string name = "Session", string reason = "needs permission")
    {
        int Scale(int value) => (int)Math.Round(value * dpi / 96d);
        var unconstrained = new Size(int.MaxValue, int.MaxValue);
        var line = Math.Max(font.Height, TextRenderer.MeasureText(graphics, "AgÉgj 00:00:00", font, unconstrained, TextFlags).Height);
        var horizontalPadding = Scale(10);
        var verticalPadding = Scale(5);
        int TextWidth(string text) => TextRenderer.MeasureText(graphics, text, font, unconstrained, TextFlags).Width + Scale(2);
        var nameWidth = Math.Clamp(TextWidth(name), Scale(70), Scale(200));
        var reasonWidth = TextWidth(reason);
        var separatorWidth = TextWidth(" · ") + Scale(4);
        // Reserve all digits, including waits over 99 hours, without resizing on each tick.
        var timer = elapsed.Length == 0 ? 0 : TextWidth(new string(elapsed.Select(character => char.IsDigit(character) ? '8' : character).ToArray()));
        var timeSeparatorWidth = elapsed.Length == 0 ? 0 : separatorWidth;
        var width = horizontalPadding * 2 + nameWidth + reasonWidth + timer + separatorWidth + timeSeparatorWidth;
        var height = verticalPadding * 2 + line;
        var reasonLeft = horizontalPadding + nameWidth + separatorWidth;
        var timerLeft = reasonLeft + reasonWidth + timeSeparatorWidth;
        return new(new(width, height),
            new(horizontalPadding, verticalPadding, nameWidth, line),
            new(reasonLeft, verticalPadding, reasonWidth, line),
            new(timerLeft, verticalPadding, timer, line), Scale(10),
            new(horizontalPadding + nameWidth, verticalPadding, separatorWidth, line),
            new(reasonLeft + reasonWidth, verticalPadding, timeSeparatorWidth, line));
    }
}
