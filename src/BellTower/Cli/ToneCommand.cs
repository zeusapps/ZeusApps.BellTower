using System.CommandLine;
using BellTower.IO;

namespace BellTower.Cli;

internal sealed class ToneCommand : Command
{
    public ToneCommand(Option<string?> dataDirectory) : base("tone", "Select or reset a notification WAV without playing it.")
    {
        var file = CommonOptions.Text("--file", "WAV tone to copy into Bell Tower's private data.");
        var reset = new Option<bool>("--reset") { Description = "Use the bundled event tones." };
        var chime = new Option<bool>("--chime") { Description = "Use the classic generated chime for every event." };
        Options.Add(file);
        Options.Add(reset);
        Options.Add(chime);
        SetAction(result =>
        {
            var paths = CommonOptions.Paths(result, dataDirectory);
            var source = result.GetValue(file);
            if ((source is not null ? 1 : 0) + (result.GetValue(reset) ? 1 : 0) + (result.GetValue(chime) ? 1 : 0) > 1)
            {
                throw new ArgumentException("Choose one of --file, --reset or --chime.");
            }
            var library = new ToneLibrary(paths);
            if (source is not null)
            {
                var tone = library.Select(source);
                result.InvocationConfiguration.Output.WriteLine($"Selected {Path.GetFileName(source)} ({tone.Milliseconds} ms). No sound played.");
            }
            else
            {
                if (result.GetValue(reset))
                {
                    library.Reset();
                }
                if (result.GetValue(chime))
                {
                    library.UseClassicChime();
                }
                result.InvocationConfiguration.Output.WriteLine(new ConfigStore(paths).Read().ToneName ?? "Default event tones");
            }
        });
    }
}
