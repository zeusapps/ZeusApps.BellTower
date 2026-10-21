using System.CommandLine;
using BellTower.IO;

namespace BellTower.Cli;

internal sealed class ToggleVoiceCommand : Command
{
    public ToggleVoiceCommand(Option<string?> dataDirectory) : base("toggle-voice", "Toggle the persisted voice preference.")
    {
        SetAction(result => new ConfigStore(CommonOptions.Paths(result, dataDirectory)).Update(config => config.Voice = !config.Voice));
    }
}
