using System.CommandLine;
using System.CommandLine.Invocation;

namespace BellTower.Cli;

internal sealed class ApplicationVersionAction : SynchronousCommandLineAction
{
    public override int Invoke(ParseResult parseResult)
    {
        parseResult.InvocationConfiguration.Output.WriteLine(ApplicationInfo.Version);
        return 0;
    }
}
