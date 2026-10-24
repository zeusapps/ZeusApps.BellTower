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
        var snapshot = new IdentityPresentationStore(paths).Read(identity.Key);
        if (snapshot is not null && snapshot.Title == identity.Name && snapshot.Palette == identity.Palette)
        { return StatusLine.Render(identity, snapshot.Git, color); }
        // Missing, stale or outdated snapshot (tray not running, wedged or mid-update): read this conversation's own
        // checkout straight from its Git metadata files. No process is started and no other conversation's data is
        // consulted; the identity's checkout is the user pin, the resolver's saved choice or the launch directory.
        var checkout = identity.Checkout(directory);
        return StatusLine.Render(identity, GitHeadReader.Read(Directory.Exists(checkout) ? checkout : directory), color);
    }
}
