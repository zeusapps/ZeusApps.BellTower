using BellTower.Hosts;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Providers;

namespace BellTower.Ingress;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args is ["--protocol-info"])
        {
            Console.WriteLine("{\"protocol\":1,\"minimumConsumer\":1,\"maximumConsumer\":1}");
            return 0;
        }
        string? root = null;
        try
        {
            // The installed adapter has exactly these arguments; parsing never starts the desktop CLI.
            if (args is not ["--provider", var provider, "--data-dir", var dataDirectory])
            {
                throw new ArgumentException("arguments");
            }
            root = Path.GetFullPath(dataDirectory);
            if (provider is not ("claude" or "codex"))
            {
                throw new ArgumentException("provider");
            }
            var at = DateTimeOffset.UtcNow;
            using var input = CaptureProtocol.ReadInput(Console.OpenStandardInput());
            var attachment = AttachmentCapture.Capture(provider, HookInput.Text(input.RootElement, "cwd") ?? Environment.CurrentDirectory);
            var dedicated = provider != "codex" || Environment.GetEnvironmentVariable("BELLTOWER_DEDICATED") == "1";
            if (!dedicated)
            {
                var session = HookInput.Text(input.RootElement, "session_id") ?? throw new InvalidDataException("session");
                attachment = attachment with
                {
                    Id = "codex-session-" + ContentHash.Compute(session)[..24],
                    Process = null,
                    Host = new("unknown"),
                    ManagedTitle = false,
                    ManagedBackground = false
                };
            }
            var ancestors = ProcessProbe.Ancestors(Environment.ProcessId)
                .Select(p => (Process: ProcessProbe.Identify(p.Pid), p.Name))
                .Where(p => p.Process is not null)
                .Select(p => new ProcessEvidence(p.Process!, p.Name)).ToList();
            var shell = ancestors.TakeWhile(p => !p.Name.Equals(provider + ".exe", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(p => p.Name.Equals("bash.exe", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals("sh.exe", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase));
            var envelope = CaptureFactory.Create(provider, input.RootElement, attachment, at, shell?.Identity, ancestors,
                dedicated, Environment.GetEnvironmentVariable("BELLTOWER_NAME"), Environment.GetEnvironmentVariable("BELLTOWER_CHANGE"));
            if (envelope is not null)
            {
                CaptureSpool.Write(root, envelope);
            }
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
        {
            CaptureSpool.RecordRejection(root, exception);
        }
        // This boundary never emits provider decisions, payloads or exception messages.
        Console.WriteLine("{}");
        return 0;
    }
}
