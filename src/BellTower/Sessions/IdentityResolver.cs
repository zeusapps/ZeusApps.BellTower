using BellTower.Core;
using BellTower.IO;

namespace BellTower.Sessions;

// Tray-owned resolver. The launch directory, the planning root/store and the working checkout are separate facts;
// checkout discovery is bounded and cached independently of process ownership, and the shared CheckoutPolicy
// decides (user pin > verified provider cwd > unique work mapping > launch).
public sealed class IdentityResolver(DataPaths paths, CheckoutCache checkout, Func<DateTimeOffset>? clock = null)
{
    private static readonly TimeSpan CacheLife = TimeSpan.FromSeconds(30);
    private readonly Func<DateTimeOffset> clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Dictionary<string, (DateTimeOffset At, IReadOnlyList<string> Paths)> worktrees = new(StringComparer.OrdinalIgnoreCase);
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
        if (pin is null)
        {
            roots = await Task.Run(() => Worktrees(launch), cancellation).ConfigureAwait(false);
            provider = identity.ProviderCwd is { } cwd ? await Task.Run(() => VerifiedProvider(cwd, launch, roots), cancellation).ConfigureAwait(false) : null;
        }
        var work = identity.WorkSubject;
        var choice = CheckoutPolicy.Choose(pin, provider, work, roots,
            root => Directory.Exists(Path.Combine(root, "openspec", "changes", work!)), Directory.Exists);
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
            CheckoutPolicy.ApplyBranch(value, value.WorkingCheckout, selected, context.Branch, clock());
            value.LaunchDirectory ??= session.Directory;
            value.WorkingCheckout = selected;
            value.CheckoutSource = source;
            value.ObservedBranch = context.Branch ?? value.ObservedBranch;
            value.AutomaticSubject = matching.Length == 1 ? matching[0] : ticketText ?? context.Branch;
            return before != Json.Encode(value);
        });
        return context;
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

    private IReadOnlyList<string> Worktrees(string launch)
    {
        if (worktrees.TryGetValue(launch, out var hit) && clock() - hit.At < CacheLife)
        { return hit.Paths; }
        var roots = git.Worktrees(launch);
        if (worktrees.Count >= 128)
        { worktrees.Remove(worktrees.MinBy(pair => pair.Value.At).Key); }
        worktrees[launch] = (clock(), roots);
        return roots;
    }
}
