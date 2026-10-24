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
    public const string PinMissing = "unavailable: pinned checkout is missing; launch fallback";

    public sealed record Choice(string? Path, string Source);

    // Priority: user pin > verified provider evidence > unique work mapping > honest launch fallback.
    public static Choice Choose(string? pin, string? verifiedProvider, string? work, IReadOnlyList<string> worktrees,
        Func<string, bool> hasChange, Func<string, bool> exists)
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
            _ => new(null, Launch)
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
