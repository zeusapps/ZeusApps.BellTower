namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// Delivery faults injected at the capture spool of a sandbox data directory while its tray is stopped. The ingress names each capture
/// <c>&lt;ingress ticks, 19 digits&gt;-&lt;envelope id&gt;.json</c> and the tray drains them in file-name order; the consumer only requires the
/// name to end with <c>-&lt;envelope id&gt;</c>. Renaming the prefix therefore changes the order the tray receives them in (shuffled delivery),
/// and a second file with the same envelope id is the same capture delivered twice (duplicate delivery). The envelope itself, and the ingress time
/// it carries, are never touched, so the product has to restore the causal order and drop the duplicate itself. This models a delivery fault, not a
/// producer behavior: both providers' hooks write the same spool.
/// </summary>
public static class SpoolInjection
{
    /// <summary>The captures currently waiting in the spool, in the order the tray would drain them.</summary>
    public static IReadOnlyList<string> Pending(string inbox) =>
        Directory.Exists(inbox) ? [.. Directory.EnumerateFiles(inbox, "*.json").OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)] : [];

    /// <summary>The envelope id of a spool file name (<c>&lt;ticks&gt;-&lt;id&gt;.json</c>).</summary>
    public static string EnvelopeId(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name[(name.IndexOf('-') + 1)..];
    }

    /// <summary>
    /// Reorders the pending captures by giving them new prefixes so that the new drain order is <paramref name="order"/> (indexes into the
    /// current drain order). Returns the old and new names in the new drain order.
    /// </summary>
    public static IReadOnlyList<(string Old, string New)> Reorder(string inbox, IReadOnlyList<int> order)
    {
        var pending = Pending(inbox);
        if (order.Count != pending.Count || order.Distinct().Count() != order.Count || order.Any(index => index < 0 || index >= pending.Count))
        {
            throw new ArgumentException("The order must be a permutation of the " + pending.Count + " pending captures.");
        }
        var moves = new List<(string Old, string New)>();
        for (var rank = 0; rank < order.Count; rank++)
        {
            var source = pending[order[rank]];
            // Digits only, 19 wide like the ingress ticks: a name no real capture can collide with, ranked by the new position.
            moves.Add((source, Path.Combine(inbox, "9" + rank.ToString("D18") + "-" + EnvelopeId(source) + ".json")));
        }
        foreach (var (old, renamed) in moves)
        {
            File.Move(old, renamed);
        }
        return moves;
    }

    /// <summary>A deterministic non-identity shuffle: the order reversed, then the first half rotated (never the original order for two or more captures).</summary>
    public static IReadOnlyList<int> ShuffledOrder(int count)
    {
        var order = Enumerable.Range(0, count).Reverse().ToList();
        if (count > 3)
        {
            var half = count / 2;
            order = [.. order.Skip(half).Concat(order.Take(half))];
        }
        return order;
    }

    /// <summary>Delivers the captures at the given positions a second time (a copy with the same envelope id and a later drain position).</summary>
    public static IReadOnlyList<string> Duplicate(string inbox, IEnumerable<int> positions)
    {
        var pending = Pending(inbox);
        var copies = new List<string>();
        var serial = 0;
        foreach (var position in positions)
        {
            var source = pending[position];
            var copy = Path.Combine(inbox, "99999999999999999" + (serial++).ToString("D2") + "-" + EnvelopeId(source) + ".json");
            File.Copy(source, copy, overwrite: false);
            copies.Add(copy);
        }
        return copies;
    }
}
