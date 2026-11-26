using System.Text.RegularExpressions;

namespace BellTower.Core;

// Provider-neutral working-checkout decision. The launch directory, the planning root/store and the working
// checkout are separate facts: only a user pin, provider-verified evidence or a unique work mapping can pick
// a working checkout, and an external planning store never does.
public static partial class CheckoutPolicy
{
    public const string Launch = "launch";
    public const string User = "user";
    public const string Provider = "provider";
    public const string UniqueWork = "unique-work";
    public const string Ambiguous = "ambiguous: attach a checkout";
    // The checkout a unique work mapping verified stays the working checkout after a branch change in that same checkout cleared the work.
    // DATA CONTRACT: this token is persisted as SessionIdentity.CheckoutSource, read back by the resolver and shown by `list`.
    // Never reword it; show people RetainedLabel through Describe instead. A change needs a migration (pinned by a unit test).
    public const string Retained = "retained";
    public const string RetainedLabel = "retained: branch changed in the verified checkout";
    public const string PinMissing = "unavailable: pinned checkout is missing; launch fallback";

    public sealed record Choice(string? Path, string Source);

    // Presentation text for a persisted source token.
    public static string Describe(string source) => source == Retained ? RetainedLabel : source;

    // "Clear checkout", attaching a checkout and Reset release a retained checkout so the next pass cannot bring it back.
    public static void Release(SessionIdentity identity)
    {
        if (identity.CheckoutSource == Retained)
        { identity.CheckoutSource = Launch; }
    }

    // Priority: user pin > verified provider evidence > unique work mapping > the retained checkout > honest launch fallback.
    // The retained checkout is the one a unique work mapping verified before a branch change in that same checkout cleared the work;
    // it only fills the place of the launch fallback, so new work evidence, ambiguity and every stronger tier still win.
    public static Choice Choose(string? pin, string? verifiedProvider, string? work, IReadOnlyList<string> worktrees,
        Func<string, bool> hasChange, Func<string, bool> exists, string? retained = null)
    {
        if (pin is not null)
        {
            return exists(pin) ? new(pin, User) : new(null, PinMissing);
        }
        if (verifiedProvider is not null && exists(verifiedProvider))
        {
            return new(verifiedProvider, Provider);
        }
        var candidates = Candidates(work, worktrees, hasChange);
        return candidates.Count switch
        {
            1 => new(candidates[0], UniqueWork),
            > 1 => new(null, Ambiguous),
            _ => retained is not null && exists(retained) ? new(retained, Retained) : new(null, Launch)
        };
    }

    // A worktree named for the work's ticket is decisive; otherwise every worktree that holds the change counts.
    public static IReadOnlyList<string> Candidates(string? work, IReadOnlyList<string> worktrees, Func<string, bool> hasChange)
    {
        if (work is null)
        { return []; }
        var ticket = TicketOf(work);
        if (ticket is not null)
        {
            var named = worktrees.Where(root => string.Equals(System.IO.Path.GetFileName(root.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)), ticket, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (named.Length > 0)
            { return named; }
        }
        return worktrees.Where(hasChange).ToArray();
    }

    // Branch-bound automatic work evidence is invalidated only when the same checkout changes branch. Moving to a
    // different checkout rebinds the evidence instead; a failed branch read never invalidates anything.
    public static bool ApplyBranch(SessionIdentity identity, string? previousCheckout, string selected, string? branch, DateTimeOffset? now = null)
    {
        if (branch is null || identity.ActiveWork is null)
        { return false; }
        if (identity.WorkBranch is null || identity.WorkBranch == branch)
        {
            var bind = identity.WorkBranch is null;
            identity.WorkBranch = branch;
            return bind;
        }
        if (!SamePath(previousCheckout, selected))
        {
            identity.WorkBranch = branch;
            return true;
        }
        WorkPolicy.Clear(identity, WorkClear.Branch, now);
        return true;
    }

    // The checkout to keep when the previous pass kept one or a unique work mapping chose it. It must still be one of the launch
    // repository's worktrees. A null listing means the Git read failed (not that the repository has no worktrees): failure is not
    // evidence, so the checkout is kept while its directory exists; only a successful listing without it, or a missing directory, releases it.
    public static string? RetainedCheckout(string? checkout, IReadOnlyList<string>? worktrees) =>
        checkout is null ? null
        : worktrees is null ? (Directory.Exists(checkout) ? checkout : null)
        : worktrees.FirstOrDefault(root => SamePath(root, checkout));

    public static bool SamePath(string? left, string? right) => left is not null && right is not null &&
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string path) => System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

    public static string? TicketOf(string? text) => text is not null && TicketPattern().Match(text) is { Success: true } match ? match.Value : null;

    [GeneratedRegex(@"\b[A-Z][A-Z0-9]{1,15}-\d+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TicketPattern();
}

public static class CheckoutEvidence
{
    // Provider hook metadata (`cwd` is a required field of the Claude Code and Codex hook inputs). Only the main
    // conversation counts: a subagent working in an isolated worktree must not move the conversation's checkout.
    // The value is just a candidate; the tray resolver verifies it against Git before it can select a checkout.
    public static bool Observe(SessionIdentity identity, string? cwd, bool mainThread)
    {
        if (!mainThread || string.IsNullOrWhiteSpace(cwd) || cwd.Length > 520 || !Path.IsPathFullyQualified(cwd) ||
            string.Equals(identity.ProviderCwd, cwd, StringComparison.Ordinal))
        {
            return false;
        }
        identity.ProviderCwd = cwd;
        return true;
    }
}
