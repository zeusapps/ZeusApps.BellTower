using BellTower.Core;

namespace BellTower.IO;

public sealed class CodexRuntimeStore(DataPaths paths)
{
    private string Prefix(string attachment) => Path.Combine(paths.Root, "codex-runtimes", ContentHash.Compute(attachment));
    public string TokenPath(string attachment) => Prefix(attachment) + ".token";
    public string StopPath(string attachment) => Prefix(attachment) + ".stop";
    public string RecordPath(string attachment) => Prefix(attachment) + ".json";

    public CodexRuntime? Get(string attachment)
    {
        var runtime = AtomicFile.Read<CodexRuntime>(RecordPath(attachment));
        return runtime is { Manager: { } manager, Shell: { } shell } && runtime.Attachment == attachment &&
            ProcessProbe.IsAlive(manager) && ProcessProbe.IsAlive(shell) &&
            !File.Exists(StopPath(attachment)) ? runtime : null;
    }

    public void Stop(string attachment)
    {
        if (File.Exists(RecordPath(attachment)) || File.Exists(TokenPath(attachment)))
        {
            AtomicFile.Write(StopPath(attachment), "stop");
        }
    }

    public void StopAndWait(string attachment)
    {
        Stop(attachment);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(4);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var runtime = AtomicFile.Read<CodexRuntime>(RecordPath(attachment));
            if (runtime?.Manager is not { } manager || !ProcessProbe.IsAlive(manager))
            {
                File.Delete(RecordPath(attachment));
                File.Delete(TokenPath(attachment));
                File.Delete(StopPath(attachment));
                return;
            }
            Thread.Sleep(50);
        }
        // Preserve the stop marker for the owning runtime if cleanup is delayed.
    }
}
