using BellTower.FieldAcceptance.Evidence;
using BellTower.FieldAcceptance.Harness;

namespace BellTower.FieldAcceptance.Scenarios;

/// <summary>
/// Task 2.4 and ledger rows F2, F4, U1 and the rollback half of the upgrade risk. Per provider and once with both providers together, one
/// disposable installation starts as the published 0.1.9 with live fake agents, is upgraded to the fixture-version candidate by the real update
/// transaction while the tray runs and hooks keep arriving, gets <c>setup --host-bridge --apply</c> (the hooks then name the candidate's ingress
/// image) and is then fault-injected: lost and late pre hooks, duplicate hooks, null-process startups, a stopped tray with shuffled and duplicated
/// spool delivery, and a branch change that leaves a retained checkout. It is then rolled back to the real 0.1.9 with the hooks still naming the
/// candidate's ingress image, read back by 0.1.9's own commands, hit with more faults while 0.1.9 consumes the candidate's captures, and upgraded
/// again. Throughout, pending waits stay, resolved waits never come back, liveness is repaired or honestly unavailable, no agent session ends and
/// nothing leaves the sandbox. The group run lives in <see cref="FaultGroupRun"/>.
/// </summary>
public sealed class FaultsAndRollbackScenario : IScenario
{
    /// <summary>Developer filter: a comma separated subset of the groups (<c>claude</c>, <c>codex</c>, <c>mixed</c>) to run. Unset runs all three.</summary>
    public const string GroupFilterVariable = "FIELD_ACCEPTANCE_GROUPS";

    public string Id => "faults-and-rollback";
    public string Title => "Lost and late pre hooks, shuffled and duplicate delivery, null-process startup, tray downtime, upgrade and rollback to 0.1.9 with candidate-image hooks";

    public void Run(RunContext context)
    {
        var filter = Environment.GetEnvironmentVariable(GroupFilterVariable)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var groups = new (string Name, ProviderModel[] Models, int First)[]
        {
            (ProviderNames.Claude, [ProviderModel.Claude], 8100),
            (ProviderNames.Codex, [ProviderModel.Codex], 8200),
            (ProviderNames.Mixed, [ProviderModel.Claude, ProviderModel.Codex], 8300)
        };
        foreach (var (name, models, first) in groups)
        {
            if (filter is null || filter.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                new FaultGroupRun(context, Id, name, models, first).Run();
            }
        }
        AddNotes(context);
    }

    private static void AddNotes(RunContext context)
    {
        context.Report.Notes.Add("faults-and-rollback, what is real and what is modeled: the 0.1.9 and candidate installations, their ingress images, trays, update transaction, spool, state and identity files are the real packaged ones under disposable roots; the provider processes are the harness's fake agents. " +
            "The approval-flow tool hooks (PreToolUse, PermissionRequest, PostToolUse) are modeled payloads in the documented shapes (no native sequence capture exists for these commands), and the Codex 0.1.9 hooks run through the labeled modeled cmd fallback (the 0.1.9 Codex command only parses in the command shell). " +
            "The rollback is a manual selection of the retained 0.1.9 payload (the documented manual restore of `current.json`, with the product's own capture and attachment-health compatibility check); the product has no rollback command, and the update coordinator's recovery performs the same two steps. " +
            "Shuffled and duplicate delivery are injected into the capture spool while the tray is stopped (file names reordered, a capture file copied under another name); the envelopes themselves are untouched, so ordering and duplicate suppression are the product's.");
        context.Report.Notes.Add("faults-and-rollback, rollback effects recorded as expected results (ANALYSIS-186, no product change): the identity data stays compatible (the `retained` checkout source and the additive `checkoutWork` field read back through 0.1.9, which keeps unknown fields), but the behavior is one-way. " +
            "The first 0.1.9 pass over a retained conversation records the launch checkout (0.1.9 knows no retained input), so upgrading again does not restore the retained checkout; a conversation 0.1.9 already moved to the launch checkout before an upgrade has lost the evidence, and stays there until new work or a pin. The checks record the source before the rollback, right after it (before any 0.1.9 tray pass), after 0.1.9's passes and after the second upgrade.");
        context.Report.Notes.Add("faults-and-rollback, 0.1.9 state is read only through 0.1.9's own commands (`list`, `doctor`, `statusline`) and the raw files; no in-process surface or policy of the candidate judges 0.1.9 state (ANALYSIS-162). `ConversationSurfaces` now refuses an installation that is not the candidate.");
        context.Report.Notes.Add("faults-and-rollback, exposure: every hook goes through `AgentSession.Hook` (so a hook-started tray is noted by the instance's exposure guard), every tray-down phase follows the user's Exit (the recorded user stop keeps hooks from starting a tray in the real profile environment), and each group fails a check if a hook started a tray or a shared-backend Codex ran.");
    }
}
