using BellTower.Core;
using BellTower.IO;

namespace BellTower.Sessions;

// Tray-owned resolver. The launch directory, the planning root/store and the working checkout are separate facts;
// checkout discovery is bounded and cached independently of process ownership, and the shared CheckoutPolicy
// decides (user pin > verified provider cwd > unique work mapping > launch).
public sealed class IdentityResolver(DataPaths paths, CheckoutCache checkout, Func<DateTimeOffset>? clock = null, Func<string, IReadOnlyList<string>?>? listing = null)
{
    private static readonly TimeSpan CacheLife = TimeSpan.FromSeconds(30);
    // A failed listing is cached as a failure (never as an empty success) for a shorter life, so a launch directory outside Git or a
    // listing that times out is not retried on every 2 s pass.
    private static readonly TimeSpan FailureLife = TimeSpan.FromSeconds(10);
    private readonly Func<DateTimeOffset> clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Dictionary<string, (DateTimeOffset At, IReadOnlyList<string>? Paths)> worktrees = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (DateTimeOffset At, string? Top)> toplevels = new(StringComparer.OrdinalIgnoreCase);
    private readonly GitReader git = new();

    public async Task<GitContext> RefreshAsync(SessionState session, CancellationToken cancellation = default)
    {
        var store = new IdentityStore(paths);
        var identity = store.Get(session.Key, session.Directory);
        var launch = identity.LaunchDirectory ?? session.Directory;
        var pin = identity.CheckoutPin;
        var provider = (string?)null;
        var roots = (IReadOnlyList<string>)[];
        // Null means the listing failed; that is not evidence about the worktrees (see CheckoutPolicy.RetainedCheckout).
        IReadOnlyList<string>? listed = [];
        if (pin is null)
        {
            listed = await Task.Run(() => Worktrees(launch), cancellation).ConfigureAwait(false);
            roots = listed ?? [];
            provider = identity.ProviderCwd is { } cwd ? await Task.Run(() => VerifiedProvider(cwd, launch, roots), cancellation).ConfigureAwait(false) : null;
        }
        var work = identity.WorkSubject;
        Func<string, bool> hasChange = root => Directory.Exists(Path.Combine(root, "openspec", "changes", work!));
        var choice = CheckoutPolicy.Choose(pin, provider, work, roots, hasChange, Directory.Exists);
        if (choice.Source == CheckoutPolicy.Launch && pin is null && await RetainedAsync(identity, listed, cancellation).ConfigureAwait(false) is { } retained)
        { choice = CheckoutPolicy.Choose(pin, provider, work, roots, hasChange, Directory.Exists, retained); }
        var selected = choice.Path ?? launch;
        var source = choice.Source;
        if (!Directory.Exists(selected))
        { selected = launch; source = "unavailable: launch fallback"; }
        var context = await checkout.ReadAsync(selected, cancellation).ConfigureAwait(false);
        // The planning root only locates change folders; it is never Git context or a working checkout.
        var changes = Path.Combine(identity.SelectionRoot ?? selected, "openspec", "changes");
        var ticketText = CheckoutPolicy.TicketOf(context.Branch);
        var matching = ticketText is not null && Directory.Exists(changes)
            ? Directory.EnumerateDirectories(changes).Take(512).Select(Path.GetFileName).Where(name => name != "archive" && name!.Contains(ticketText, StringComparison.OrdinalIgnoreCase)).ToArray() : [];
        store.Update(session.Key, session.Directory, value =>
        {
            var before = Json.Encode(value);
            var hadWork = value.WorkSubject is not null;
            CheckoutPolicy.ApplyBranch(value, value.WorkingCheckout, selected, context.Branch, clock());
            // A branch change in the checkout the work verified clears the work; that checkout stays the working checkout.
            var branchCleared = hadWork && value.WorkSubject is null && source == CheckoutPolicy.UniqueWork;
            value.LaunchDirectory ??= session.Directory;
            value.WorkingCheckout = selected;
            // The final retention decision is made here, against locked fresh state: a Clear, Reset or pin that landed while this pass
            // awaited Git must win, so retained is written only while the stored source is still the one this pass started from.
            var retain = branchCleared || source == CheckoutPolicy.Retained;
            var kept = retain && value.CheckoutPin is null && value.CheckoutSource == identity.CheckoutSource;
            value.CheckoutSource = retain ? (kept ? CheckoutPolicy.Retained : CheckoutPolicy.Launch) : source;
            if (source == CheckoutPolicy.UniqueWork)
            { value.CheckoutWork = work; }
            else if (!retain)
            { value.CheckoutWork = null; }
            value.ObservedBranch = context.Branch ?? value.ObservedBranch;
            value.AutomaticSubject = matching.Length == 1 ? matching[0] : ticketText ?? context.Branch;
            return before != Json.Encode(value);
        });
        return context;
    }

    // The checkout that fills the place of the launch fallback. Two cases, both for the checkout a unique work mapping verified:
    // it is already retained and no work has appeared since (any new work supersedes it), or the branch of that checkout changed and
    // the pass is about to clear the branch-bound work, so retention must not depend on the verifying change folder surviving the switch.
    private async Task<string?> RetainedAsync(SessionIdentity identity, IReadOnlyList<string>? listed, CancellationToken cancellation)
    {
        if (CheckoutPolicy.RetainedCheckout(identity.WorkingCheckout, listed) is not { } kept)
        { return null; }
        if (identity.CheckoutSource == CheckoutPolicy.Retained)
        { return identity.WorkSubject is null ? kept : null; }
        // Only work that verified this checkout may be cleared by its branch change; unrelated new work rebinds to the launch checkout instead.
        if (identity.CheckoutSource != CheckoutPolicy.UniqueWork || identity.PinnedChange is not null || identity.ActiveWork is null || identity.WorkBranch is null ||
            identity.ActiveWork != identity.CheckoutWork)
        { return null; }
        var branch = (await checkout.ReadAsync(kept, cancellation).ConfigureAwait(false)).Branch;
        return branch is not null && branch != identity.WorkBranch ? kept : null;
    }

    // Provider evidence counts only when the reported cwd belongs to a different checkout than the launch one and
    // that checkout is a worktree of the launch repository family (listed by Git); a cwd inside the launch
    // checkout, or in an unrelated repository, proves nothing about the working checkout.
    private string? VerifiedProvider(string cwd, string launch, IReadOnlyList<string> roots)
    {
        if (!Path.IsPathFullyQualified(cwd) || !Directory.Exists(cwd))
        { return null; }
        var top = Toplevel(cwd);
        var launchTop = Toplevel(launch);
        if (top is null || launchTop is null || CheckoutPolicy.SamePath(top, launchTop))
        { return null; }
        return roots.FirstOrDefault(root => CheckoutPolicy.SamePath(root, top));
    }

    private string? Toplevel(string directory)
    {
        if (toplevels.TryGetValue(directory, out var hit) && clock() - hit.At < CacheLife)
        { return hit.Top; }
        var top = git.Toplevel(directory);
        if (toplevels.Count >= 128)
        { toplevels.Remove(toplevels.MinBy(pair => pair.Value.At).Key); }
        toplevels[directory] = (clock(), top);
        return top;
    }

    private IReadOnlyList<string>? Worktrees(string launch)
    {
        if (worktrees.TryGetValue(launch, out var hit) && clock() - hit.At < (hit.Paths is null ? FailureLife : CacheLife))
        { return hit.Paths; }
        var roots = (listing ?? git.TryWorktrees)(launch); // null is cached as a failure, which retention reads as "no evidence", not as an empty listing
        if (worktrees.Count >= 128)
        { worktrees.Remove(worktrees.MinBy(pair => pair.Value.At).Key); }
        worktrees[launch] = (clock(), roots);
        return roots;
    }
}
