using System.Text;
using BellTower.Cli;
using BellTower.Distribution;

namespace BellTower;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        UseUtf8Console(static e => Console.OutputEncoding = e, static e => Console.InputEncoding = e);
        try
        {
            CaptureCompatibility.EnsureTrayInvocation(args, 1, attachmentHealth: 1);
            var installation = Installation.ForPayload(Environment.ProcessPath!);
            using var lifetime = installation?.Lease(Path.GetFileName(Path.GetDirectoryName(Environment.ProcessPath!))!);
            if (args is ["__apply-update", var plan, var token])
            { return Updates.UpdateTransaction.Apply(plan, token); }
            if (args is ["--distribution-info"])
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new ReleaseContract(ApplicationInfo.Version, Capture: 1, AttachmentHealth: 1), DistributionJson.Options));
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

    // The inherited console host can disconnect during startup (for example a cancelled status-line run); the
    // encoding setters then throw IOException (0x800700E9). Each setter is independent and only IOException is tolerated.
    internal static void UseUtf8Console(Action<Encoding> setOutput, Action<Encoding> setInput)
    {
        var utf8 = new UTF8Encoding(false);
        try
        { setOutput(utf8); }
        catch (IOException)
        { }
        try
        { setInput(utf8); }
        catch (IOException)
        { }
    }
}
