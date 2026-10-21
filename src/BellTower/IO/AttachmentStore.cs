using BellTower.Core;

namespace BellTower.IO;

public sealed class AttachmentStore(DataPaths paths)
{
    public Attachment? Get(string id) => AtomicFile.Read<Attachment>(FilePath(id));
    public void Save(Attachment attachment) => AtomicFile.Write(FilePath(attachment.Id), Json.Encode(attachment));
    private string FilePath(string id) => Path.Combine(paths.Attachments, ContentHash.Compute(id) + ".json");
}
