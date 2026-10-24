using BellTower.Core;

namespace BellTower.Sessions;

public sealed record SessionSummary(string Key, SessionIdentity Identity, string Activity, AttentionReason? Reason, DateTimeOffset? Since, string Navigation)
{
    public string AttachmentHealth { get; init; } = "unverified";
    public string? NativePresentationName { get; init; }
    public string? NativeNameSynchronization { get; init; }
    public string? Checkout { get; init; }
    public string? CheckoutSource { get; init; }
}
