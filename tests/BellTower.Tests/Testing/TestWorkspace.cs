using BellTower.Core;
using BellTower.IO;

namespace BellTower.Tests.Testing;

public sealed class TestWorkspace : IDisposable
{
    public string Root { get; }
    public DataPaths Paths { get; }

    public TestWorkspace()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ZeusApps.BellTower.slnx")))
        {
            directory = directory.Parent;
        }

        var artifacts = Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Project root missing."), "artifacts", "unit-tests");
        Root = Path.Combine(artifacts, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Paths = new(Path.Combine(Root, "data"));
        Paths.Ensure();
    }

    public void Dispose()
    {
        var parent = Directory.GetParent(Root)!.FullName;
        var relative = Path.GetRelativePath(parent, Path.GetFullPath(Root));
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) || relative.Length != 32)
        {
            throw new InvalidOperationException("Unsafe test cleanup.");
        }

        // Git marks object files read-only; clear that so repositories created by tests can be removed.
        foreach (var file in new DirectoryInfo(Root).EnumerateFiles("*", SearchOption.AllDirectories).Where(file => file.IsReadOnly))
        {
            file.IsReadOnly = false;
        }

        Directory.Delete(Root, true);
    }

    public static Observation Event(EventKind kind, DateTimeOffset? at = null, string? operation = null, string? turn = "turn-1") => new()
    {
        Provider = "claude",
        SessionId = "session-1",
        AttachmentId = "attachment-1",
        Kind = kind,
        At = at ?? DateTimeOffset.UtcNow,
        OperationId = operation,
        TurnId = turn,
        AdapterRevision = Observation.HumanWaitRevision
    };

    // A human-confirmed permission wait (a provider human-prompt signal with no earlier approval start), eligible on arrival.
    public static Observation ConfirmedPermission(DateTimeOffset? at = null, string? operation = null, string? turn = "turn-1") =>
        Event(EventKind.Permission, at, operation, turn) with { HumanPrompt = true, ProviderDelay = TimeSpan.FromSeconds(5) };
}
