namespace BellTower.Hosts;

public sealed record FocusResult(bool Success, string Precision, string Message)
{
    /// <summary>The precision values the navigators produce. Anything else read from a helper or a bridge receipt is not trusted as text.</summary>
    public static readonly IReadOnlyList<string> KnownPrecisions = ["exact tab", "exact terminal", "terminal only", "window only", "tab or window", "unavailable"];

    /// <summary>
    /// A result read from a helper process or a bridge receipt can lack fields. Missing text becomes a stated generic value, so
    /// every later consumer (notice, log, viewed rule) can rely on a non-null precision and message.
    /// </summary>
    public FocusResult Normalized() => this with
    {
        Precision = string.IsNullOrWhiteSpace(Precision) ? "unavailable" : Precision,
        Message = string.IsNullOrWhiteSpace(Message) ? Success ? "Focused." : "The terminal could not be focused." : Message
    };
}
