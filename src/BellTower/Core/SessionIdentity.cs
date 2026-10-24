namespace BellTower.Core;

public sealed record SessionIdentity
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }
    public required string Key { get; init; }
    public string DefaultName { get; init; } = "Session";
    public string? ManualName { get; set; }
    public string? Change { get; set; }
    // Where Change came from (ChangeOrigin): user, launch, legacy-selection or legacy-superseded. ChangeSourceValue is the Change the origin
    // was recorded for; a different Change (for example written by an older build after a rollback) is classified again.
    public string? ChangeSource { get; set; }
    public string? ChangeSourceValue { get; set; }
    // Legacy: Bell Tower no longer uses a session role. Saved values stay on disk (rollback-safe) but are never read for display or speech.
    public string? Role { get; set; }
    public string? NativeTitle { get; set; }
    public string? NativeTitleSource { get; set; }
    public string? ActiveWork { get; set; }
    public string? WorkBranch { get; set; }
    public string? AutomaticSubject { get; set; }
    public string? LaunchDirectory { get; set; }
    public string? WorkingCheckout { get; set; }
    public string? CheckoutPin { get; set; }
    // Latest working directory reported by a main-thread provider hook; verified by the resolver before use.
    public string? ProviderCwd { get; set; }
    public string CheckoutSource { get; set; } = "launch";
    public string? ObservedBranch { get; set; }
    public string? TranscriptPath { get; set; }
    public long HistoryOffset { get; set; }
    public long HistoryLength { get; set; }
    public string? HistoryFingerprint { get; set; }
    public long NativeTitleOffset { get; set; } = -1;
    // Native title the user released with "use Bell Tower name"; the same title is not applied again.
    public string? NativeTitleReleased { get; set; }
    // Verified parent conversation (transcript forkedFrom) and the one-time identity inheritance receipt.
    public string? ForkParent { get; set; }
    public string? ForkInheritedFrom { get; set; }
    // Codex fork: byte offset of the first thread-settings record owned by this thread. Everything before it was copied from the parent.
    public long ForkBoundaryOffset { get; set; } = -1;
    // History evidence stamped before this provider-clock time predates the last branch invalidation and is never work
    // evidence. It is a time, not a byte offset, so it survives fresh passes over the same conversation.
    public DateTimeOffset? WorkFloorAt { get; set; }
    public string? ColorPin { get; set; }
    public string? WorkSource { get; set; }
    public bool WorkWeak { get; set; }
    public long HistoryWorkOffset { get; set; } = -1;
    public List<HistoryResult> HistoryResults { get; set; } = [];
    public List<HistoryWork> HistoryCalls { get; set; } = [];
    public string? SelectionOperation { get; set; }
    public DateTimeOffset? SelectionAt { get; set; }
    public string? SelectionEvent { get; set; }
    public List<string> SelectionReceipts { get; init; } = [];
    public List<SelectionEvidence> SelectionEvidence { get; set; } = [];
    public DateTimeOffset? SelectionFloorAt { get; set; }
    public string? SelectionRoot { get; set; }
    public string? SelectionStore { get; set; }
    public bool NativeSyncEnabled { get; set; } = true;
    public bool NativeSyncTakeOwnership { get; set; }
    public string? NativeLastName { get; set; }
    // Name Bell Tower is about to write (or just wrote) to the provider; recorded before the write so that a
    // concurrent history pass never mistakes Bell Tower's own title for a user rename.
    public string? NativePendingName { get; set; }
    public string NativeSyncStatus { get; set; } = "unavailable";
    public required Palette Palette { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? PinnedChange => ChangeOrigin.IsPin(this) ? Change : null;
    // Pinned change, then verified work, then a legacy automatically selected change (lowest work tier). A legacy
    // change that verified work superseded stays on disk for rollback but is never a subject again.
    [System.Text.Json.Serialization.JsonIgnore]
    public string? WorkSubject => PinnedChange ?? ActiveWork ?? ChangeOrigin.Legacy(this);
    public string Subject => WorkSubject ?? AutomaticSubject ?? DefaultName;
    public string Name => ManualName ?? NativeTitle ?? Subject;
    public string Checkout(string fallback) => CheckoutPin ?? WorkingCheckout ?? LaunchDirectory ?? fallback;
}
