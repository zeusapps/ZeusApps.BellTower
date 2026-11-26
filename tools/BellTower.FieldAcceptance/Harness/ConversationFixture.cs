namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// The conversation fixture shared by the scenarios that need the same conversations (ANALYSIS-170): a ticket worktree with its branch and
/// OpenSpec change folders, and the provider's modeled transcript history of one successful write into that worktree. Scenarios keep the
/// controls and the checks; the per-provider transcript shapes live in <see cref="ProviderModel.HistoryRecords"/>.
/// </summary>
public static class ConversationFixture
{
    /// <summary>A linked worktree of <paramref name="main"/> on a new branch with a proposal file in each named OpenSpec change folder.</summary>
    public static void BuildWorktree(Instance instance, string main, string worktree, string branch, params string[] changes)
    {
        SandboxGit.AddWorktree(instance, main, worktree, branch);
        foreach (var change in changes)
        {
            var folder = Path.Combine(worktree, "openspec", "changes", change);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "proposal.md"), "# " + change + "\n");
        }
    }

    /// <summary>Writes the conversation's modeled transcript (its historical work in <paramref name="worktree"/>), at the path its hook payloads name.</summary>
    public static void WriteHistory(Instance instance, ProviderModel model, string sessionId, string toolKey, string worktree, string change, DateTimeOffset at, string? nativeTitle = null) =>
        TranscriptModel.Write(ProviderModel.TranscriptPath(instance.Layout, sessionId), model.HistoryRecords(sessionId, toolKey, worktree, change, at, nativeTitle));
}
