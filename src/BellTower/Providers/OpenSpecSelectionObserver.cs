using System.Text.Json;
using BellTower.IO;

namespace BellTower.Providers;

public sealed class OpenSpecSelectionObserver(DataPaths paths)
{
    public void Observe(string key, string directory, JsonElement input)
    {
        var selection = OpenSpecSelectionReader.Read(input);
        if (selection is null)
        {
            return;
        }
        var pre = HookInput.Text(input, "hook_event_name") == "PreToolUse";
        var root = pre ? null : OpenSpecSelectionReader.SuccessfulRoot(input, selection);
        if (!pre && root is null)
        {
            return;
        }
        new IdentityStore(paths).Update(key, directory, identity =>
        {
            if (pre)
            {
                if (identity.SelectionReceipts.Contains(selection.Operation))
                {
                    return false;
                }
                identity.SelectionOperation = selection.Operation;
                identity.SelectionReceipts.Add(selection.Operation);
                if (identity.SelectionReceipts.Count > 64)
                {
                    identity.SelectionReceipts.RemoveAt(0);
                }
                return true;
            }
            if (identity.SelectionOperation != selection.Operation)
            {
                return false;
            }
            identity.SelectionOperation = null;
            identity.Change = selection.Change;
            identity.SelectionRoot = root;
            identity.SelectionStore = selection.Store;
            return true;
        });
    }
}
