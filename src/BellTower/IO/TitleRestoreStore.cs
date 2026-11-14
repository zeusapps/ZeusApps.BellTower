using BellTower.Core;

namespace BellTower.IO;

/// <summary>
/// Records of native console titles and colors Bell Tower changed, one per shell (process identifier plus creation time) because
/// the console belongs to the shell, not to a session. Callers hold the "presentation" lock. Attachment-keyed records written by
/// earlier unreleased dev builds lived in the <c>title-restore</c> folder; this store never reads or deletes that folder.
/// </summary>
public sealed class TitleRestoreStore(DataPaths paths)
{
    private string Folder => Path.Combine(paths.Root, "shell-title-restore");
    private string FilePath(ProcessIdentity shell) => Path.Combine(Folder, ContentHash.Compute(shell.Pid + ":" + shell.StartedUtcTicks) + ".json");

    public TitleRestore? Get(ProcessIdentity shell) => Read(FilePath(shell)) is { } value && value.Shell == shell ? value : null;

    /// <summary>The records this attachment owns (it was the last writer to each shell); after a resume that can be more than one.</summary>
    public IReadOnlyList<TitleRestore> OwnedBy(string id) => List().Where(record => record.AttachmentId == id).ToList();

    public IReadOnlyList<TitleRestore> List()
    {
        if (!Directory.Exists(Folder))
        {
            return [];
        }
        return Directory.EnumerateFiles(Folder, "*.json").Select(Read).OfType<TitleRestore>().ToList();
    }

    public void Save(TitleRestore record)
    {
        Directory.CreateDirectory(Folder);
        AtomicFile.Write(FilePath(record.Shell), Json.Encode(record));
    }

    public void Remove(ProcessIdentity shell)
    {
        var path = FilePath(shell);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static TitleRestore? Read(string path)
    {
        try
        { return AtomicFile.Read<TitleRestore>(path); }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException) { return null; }
    }
}
