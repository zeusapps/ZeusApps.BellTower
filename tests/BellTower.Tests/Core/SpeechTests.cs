using BellTower.Core;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Core;

// Shared spoken form (task 3.5): at most four subject words, then the reason (no role), for both providers.
public sealed class SpeechTests
{
    private static SessionState Waiting(string provider)
    {
        var state = new AppState();
        SessionReducer.Apply(state, TestWorkspace.ConfirmedPermission() with { Provider = provider, SessionId = "one", AttachmentId = "attachment-" + provider });
        return state.Sessions.Values.Single();
    }

    private static SessionIdentity Identity(string provider, Action<SessionIdentity> configure)
    {
        var identity = new SessionIdentity { Key = provider + ":one", DefaultName = "folder", Palette = Palette.Create() };
        configure(identity);
        return identity;
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void PinnedChangeSpeaksSubjectAndReasonWithoutPunctuationForBothProviders(string provider)
    {
        var identity = Identity(provider, value => value.Change = "ttyd-download-chips");
        Assert.Equal("ttyd-download-chips", identity.Name);
        Assert.Equal("ttyd download chips, needs permission", AlertPolicy.Phrase(Waiting(provider), identity));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ALegacySavedRoleIsNeverSpokenOrShownAndTitlesKeepTheirWords(string provider)
    {
        var session = Waiting(provider);
        // Bell Tower has no session role: a saved legacy value is preserved on disk but ignored by name and speech.
        var legacy = Identity(provider, value => { value.Role = "PM"; value.Change = "ttyd-download-chips"; });
        Assert.Equal("ttyd-download-chips", legacy.Name);
        Assert.Equal("ttyd download chips, needs permission", AlertPolicy.Phrase(session, legacy));
        Assert.Equal("pm migration plan, needs permission", AlertPolicy.Phrase(session, Identity(provider, value => { value.Role = "PM"; value.Change = "pm-migration-plan"; })));
        Assert.Equal("dep 7070 title, needs permission", AlertPolicy.Phrase(session, Identity(provider, value => { value.Role = "QA"; value.NativeTitle = "dep_7070 title"; })));
        // An imported or typed title that happens to start with "PM:" stays the title as-is.
        Assert.Equal("PM: ttyd-download-chips", Identity(provider, value => { value.Role = "PM"; value.ManualName = "PM: ttyd-download-chips"; }).Name);
        Assert.Equal("PM ttyd download chips, needs permission", AlertPolicy.Phrase(session, Identity(provider, value => value.ManualName = "PM: ttyd-download-chips")));
        Assert.Equal("login flow, needs permission", AlertPolicy.Phrase(session, Identity(provider, value => value.Change = "login-flow")));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void SubjectsAreLimitedToFourWordsAfterNormalization(string provider)
    {
        var session = Waiting(provider);
        var identity = Identity(provider, value => value.ActiveWork = "one-two_three-four-five-six");
        Assert.Equal("one two three four, needs permission", AlertPolicy.Phrase(session, identity));
        var title = Identity(provider, value => value.ManualName = "alpha beta gamma delta epsilon");
        Assert.Equal("alpha beta gamma delta, needs permission", AlertPolicy.Phrase(session, title));
        Assert.Equal(4, AlertPolicy.SpokenSubjectWords);
    }
}
