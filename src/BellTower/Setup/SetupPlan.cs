using BellTower.IO;

namespace BellTower.Setup;

public sealed class SetupPlan(List<FileChange> changes, List<SetupAction>? actions = null, List<string>? notes = null,
    FileChange? commitAfterActions = null, IngressProvision? ingress = null)
{
    public IReadOnlyList<FileChange> Changes => changes;
    public IReadOnlyList<SetupAction> Actions => actions ?? [];
    public IReadOnlyList<string> Notes => notes ?? [];

    public void Apply(DataPaths paths)
    {
        using var gate = new DataLock(paths, "setup");
        foreach (var change in changes)
        {
            if ((File.Exists(change.Path) ? File.ReadAllText(change.Path) : null) != change.Before)
            {
                throw new IOException("Configuration changed since planning; rerun setup.");
            }
        }

        ingress?.Apply(paths);
        var backupRoot = Path.Combine(paths.Root, "integration-backups", DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(backupRoot);
        for (var i = 0; i < changes.Count; i++)
        {
            var change = changes[i];
            if (change.Before is not null)
            {
                File.WriteAllText(Path.Combine(backupRoot, i + ".json"), change.Before);
            }
            if (change == commitAfterActions)
            {
                continue;
            }

            // Each replacement is atomic; an interrupted multi-file setup is safe to rerun.
            if (change.After is null)
            {
                File.Delete(change.Path);
            }
            else
            {
                AtomicFile.Write(change.Path, change.Utf8Bom ? "\uFEFF" + change.After : change.After);
            }
        }
        foreach (var action in Actions)
        {
            action.Apply();
        }
        if (commitAfterActions is not null)
        {
            AtomicFile.Write(commitAfterActions.Path, commitAfterActions.After!);
        }
    }
}
