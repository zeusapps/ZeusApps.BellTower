using System.CommandLine;
using BellTower.Tray;

namespace BellTower.Cli;

internal sealed class TestSoundCommand : Command
{
    public TestSoundCommand(Option<string?> dataDirectory) : base("test-sound", "Explicitly request a test sound from the running tray.")
    {
        SetAction(result => new TrayController(CommonOptions.Paths(result, dataDirectory)).RequestTestSound());
    }
}
