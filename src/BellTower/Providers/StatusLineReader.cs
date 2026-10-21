using System.Text.Json;
using BellTower.IO;

namespace BellTower.Providers;

public sealed class StatusLineReader(DataPaths paths)
{
    public string Render(TextReader reader, string provider, bool color)
    {
        using var document = JsonDocument.Parse(HookInput.ReadBounded(reader));
        var input = document.RootElement;
        var session = HookInput.Text(input, "session_id") ?? throw new InvalidDataException("Status line session ID missing.");
        var directory = HookInput.Text(input, "cwd") ?? Environment.CurrentDirectory;
        if (input.TryGetProperty("workspace", out var workspace))
        {
            directory = HookInput.Text(workspace, "current_dir") ?? directory;
        }

        var identity = new IdentityStore(paths).Get(provider + ":" + session, directory);
        return StatusLine.Render(identity, new GitReader().Read(directory), color);
    }
}
