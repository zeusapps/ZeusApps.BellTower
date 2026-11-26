using BellTower.Core;
using BellTower.IO;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// The runtime record of a Bell Tower owned Codex runtime (<c>CodexRuntimeStore</c>): the attachment, the endpoint, the manager that started the
/// <c>app-server</c> backend and the launching shell. The native-adoption check only reads this record and the process identities (no WebSocket), and requires
/// the backend's parent to be the recorded manager. A dedicated idle helper process (<c>--manager</c>) is both: never the harness process, so no product
/// presentation or focus action can target the harness console.
/// </summary>
public static class OwnedRuntimeRecord
{
    public static void Write(Instance instance, string attachmentId, int helperProcessId)
    {
        var helper = ProcessProbe.Identify(helperProcessId) ?? throw new IOException("The runtime helper process identity could not be read.");
        var store = new CodexRuntimeStore(new DataPaths(instance.Layout.Data));
        AtomicFile.Write(store.RecordPath(attachmentId), Json.Encode(new CodexRuntime(attachmentId, "ws://127.0.0.1:1", helper, helper)));
    }
}
