using BellTower.Hosts;

namespace BellTower.Tray;

/// <summary>
/// What a click shows and logs. Windows decides on its own whether a balloon is displayed (Focus, Do Not Disturb and per-app
/// switches are not detectable by an application), so the reason of every click that does not reach its exact destination is
/// shown in Bell Tower's own notice, which Windows settings cannot hide. Shared by both providers and both hosts.
/// </summary>
public static class NavigationNotice
{
    public const string SessionMissingLog = "navigation-session-missing";
    public const string FailedLog = "navigation-failed";
    public const string SessionMissingMessage = "This session is no longer tracked, so there is nothing to focus. It may have ended.";
    public const string FailedMessage = "Bell Tower could not focus the terminal. Switch to it manually.";

    /// <summary>True when the click's reason is shown: every failure and every result below an exact tab or terminal.</summary>
    public static bool ShowsNotice(FocusResult result) => NavigationOutcome.NeedsNotice(result);

    /// <summary>
    /// The one log code of a click: <c>navigation-ok-</c> or <c>navigation-refused-</c> and a precision from the known set
    /// (anything else, including a free-text value from a bridge receipt, is <c>other</c>), never a path, name or message.
    /// </summary>
    public static string LogCode(FocusResult result)
    {
        var precision = result.Precision is { } value && FocusResult.KnownPrecisions.Contains(value, StringComparer.Ordinal) ? value : "other";
        return "navigation-" + (result.Success ? "ok" : "refused") + "-" + precision.Replace(' ', '-');
    }

    /// <summary>
    /// Runs a navigation and always returns a result with its log code: a navigator that throws is answered with a stated
    /// failure instead of surfacing through the message loop.
    /// </summary>
    public static async Task<(FocusResult Result, string LogCode)> RunAsync(Func<Task<FocusResult>> focus)
    {
        try
        {
            var result = (await focus().ConfigureAwait(true)).Normalized();
            return (result, LogCode(result));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or
            InvalidOperationException or System.Text.Json.JsonException or InvalidDataException)
        {
            return (new FocusResult(false, "unavailable", FailedMessage), FailedLog);
        }
    }

    public static (FocusResult Result, string LogCode) Missing() => (new FocusResult(false, "unavailable", SessionMissingMessage), SessionMissingLog);
}
