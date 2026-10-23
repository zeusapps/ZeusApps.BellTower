using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Protocol;

namespace BellTower.Providers;

public sealed class OpenSpecSelectionObserver(DataPaths paths)
{
    public void Observe(CaptureEnvelope envelope)
    {
        if (envelope.Selection is not { } selection)
        {
            return;
        }
        var root = envelope.SelectionRoot;
        if (root is null && envelope.CreatedScaffold)
        {
            for (var current = new DirectoryInfo(envelope.Observation.Directory); current is not null; current = current.Parent)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "openspec")))
                {
                    if (File.Exists(Path.Combine(current.FullName, "openspec", "changes", selection.Change, ".openspec.yaml")))
                    {
                        root = current.FullName;
                    }
                    break;
                }
            }
        }
        Apply(envelope.Observation.Key, envelope.Observation.Directory, selection, root,
            envelope.SelectionPre, envelope.Observation.At, envelope.Id);
    }

    public void Observe(string key, string directory, JsonElement input)
    {
        var selection = OpenSpecSelectionReader.Read(input);
        if (selection is null)
        {
            return;
        }
        var pre = HookInput.Text(input, "hook_event_name") == "PreToolUse";
        Apply(key, directory, selection, pre ? null : OpenSpecSelectionReader.SuccessfulRoot(input, selection),
            pre, DateTimeOffset.UtcNow, Guid.NewGuid().ToString("N"));
    }

    private void Apply(string key, string directory, OpenSpecSelection selection, string? root, bool pre, DateTimeOffset at, string id)
    {
        if (!pre && root is null)
        {
            return;
        }
        new IdentityStore(paths).Update(key, directory, identity =>
        {
            if (identity.SelectionFloorAt >= at || identity.SelectionEvidence.Any(e => e.Operation == selection.Operation && e.Pre == pre))
            {
                return false;
            }
            identity.SelectionEvidence.Add(new(id, selection.Operation, at, pre, selection.Change, root, selection.Store));
            identity.SelectionEvidence = identity.SelectionEvidence.OrderBy(e => e.At).ThenBy(e => e.Pre ? 0 : 1)
                .ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
            if (identity.SelectionEvidence.Count > 128)
            {
                identity.SelectionFloorAt = identity.SelectionEvidence[0].At;
                identity.SelectionEvidence.RemoveAt(0);
            }
            var started = identity.SelectionEvidence.Where(e => e.Pre).Select(e => e.Operation).ToHashSet(StringComparer.Ordinal);
            var completed = new HashSet<string>(StringComparer.Ordinal);
            string? active = null;
            SelectionEvidence? selected = null;
            foreach (var evidence in identity.SelectionEvidence)
            {
                if (evidence.Pre)
                {
                    if (!completed.Contains(evidence.Operation))
                    {
                        active = evidence.Operation;
                    }
                }
                else if (active == evidence.Operation || !started.Contains(evidence.Operation) && active is null)
                {
                    completed.Add(evidence.Operation);
                    selected = evidence;
                    active = null;
                }
            }
            identity.SelectionOperation = active;
            if (!identity.SelectionReceipts.Contains(selection.Operation))
            {
                identity.SelectionReceipts.Add(selection.Operation);
                if (identity.SelectionReceipts.Count > 64)
                {
                    identity.SelectionReceipts.RemoveAt(0);
                }
            }
            if (selected is not null)
            {
                identity.SelectionAt = selected.At;
                identity.SelectionEvent = selected.Id;
                identity.Change = selected.Change;
                identity.SelectionRoot = selected.Root;
                identity.SelectionStore = selected.Store;
            }
            return true;
        });
    }
}
