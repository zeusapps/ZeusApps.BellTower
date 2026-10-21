namespace BellTower.Tray;

public static class StripPreview
{
    public static void Save(string output)
    {
        ApplicationConfiguration.Initialize();
        using var strip = new StripForm();
        strip.UpdateChips([
            new("claude:pm", "a", "session attention", new("#101c27", "#4b91bb", "#000000"),
                "has a question", "00:03:41", "branch: feature/attention  worktree: bell-pm"),
            new("codex:dev", "b", "codex event adapter", new("#22172a", "#9b68bd", "#000000"),
                "needs permission", "00:00:08", "branch: feature/codex"),
            new("claude:qa", "c", "terminal navigation", new("#14231d", "#4eab85", "#000000"),
                "finished", "00:01:22", "branch: feature/focus")
        ]);
        strip.Refresh();
        using var bitmap = new Bitmap(strip.Width, strip.Height);
        strip.DrawToBitmap(bitmap, new Rectangle(Point.Empty, strip.Size));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        bitmap.Save(output, System.Drawing.Imaging.ImageFormat.Png);
    }
}
