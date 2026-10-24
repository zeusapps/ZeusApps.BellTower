namespace BellTower.Core;

// Derived, receipt-only record of a provider tool call awaiting its result. Proof names the
// result marker required before the call may become work evidence ("result" or "created").
public sealed record HistoryWork(string Operation, string Change, string Root, string Source, DateTimeOffset At,
    long Offset = -1, bool Weak = false, string Proof = "result");

// Derived record of a provider tool result (failed results are kept so their call can be discarded). Only a hash of the call identifier is kept.
public sealed record HistoryResult(string Operation, long Offset, bool Created, bool Ok = true);
