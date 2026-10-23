using System.Text;
using BellTower.Cli;
using BellTower.Distribution;

namespace BellTower;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);
        try
        {
            CaptureCompatibility.EnsureTrayInvocation(args, 1);
            var installation = Installation.ForPayload(Environment.ProcessPath!);
            using var lifetime = installation?.Lease(Path.GetFileName(Path.GetDirectoryName(Environment.ProcessPath!))!);
            if (args is ["__apply-update", var plan, var token])
            { return Updates.UpdateTransaction.Apply(plan, token); }
            if (args is ["--distribution-info"])
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new ReleaseContract(ApplicationInfo.Version, Capture: 1), DistributionJson.Options));
                return 0;
            }
            return CliApplication.Run(args);
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
        {
            Console.Error.WriteLine("bell-tower: " + exception.Message);
            return 1;
        }
    }
}
