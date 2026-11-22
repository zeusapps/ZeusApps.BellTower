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
        var persisted = false;
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
            // Process evidence decides adoption; an inherited BELLTOWER_* flag is never ownership evidence.
            // One process table per hook serves the ancestry, the agent's neighbors and the envelope ancestors below.
            var table = ProcessTable.Take();
            var outcome = AttachmentCapture.Resolve(provider, HookInput.Text(input.RootElement, "cwd") ?? Environment.CurrentDirectory,
                HookInput.Text(input.RootElement, "session_id"), table);
            var attachment = outcome.Attachment;
            var dedicated = outcome.Dedicated;
            var ancestors = table.Ancestors(Environment.ProcessId)
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
            persisted = true;
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
        {
            CaptureSpool.RecordRejection(root, exception);
        }
        // This boundary never emits provider decisions, payloads or exception messages.
        Console.WriteLine("{}");
        // Decision 9: after the capture is durable (or a benign event needs none) and the neutral output is out, a stopped tray is
        // started detached. It never waits, never throws and is skipped after a rejected capture or an argument error.
        if (persisted && root is not null)
        {
            HookTrayStart.TryStart(root);
        }
        return 0;
    }
}
