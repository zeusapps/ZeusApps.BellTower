using System.Text.Json;
using BellTower.Core;
using BellTower.Diagnostics;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Diagnostics;

// Doctor's state section: size against the budget, live and ended counts for both providers, and refresh health.
public sealed class StateReporterTests
{
    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static JsonElement Report(TestWorkspace workspace, bool trayRunning, DateTimeOffset now) =>
        JsonDocument.Parse(Json.Encode(new StateReporter(workspace.Paths, new ManualTime(now)).Report(trayRunning))).RootElement;

    private static SessionState Session(string provider, string id, bool ended) => new()
    {
        Key = provider + ":" + id,
        Provider = provider,
        SessionId = id,
        AttachmentId = "attachment-" + id,
        LastObserved = DateTimeOffset.UtcNow,
        Ended = ended
    };

    [Fact]
    public void MissingStateAndHealthFilesAreReportedHonestlyWithoutFailing()
    {
        using var workspace = new TestWorkspace();

        var report = Report(workspace, trayRunning: true, DateTimeOffset.UtcNow);

        Assert.Contains("not present", report.GetProperty("stateFile").GetString());
        Assert.Contains("unavailable", report.GetProperty("sessions").GetString());
        Assert.Contains("no refresh failure recorded", report.GetProperty("refresh").GetString());
    }

    [Fact]
    public void SizeBudgetAndLiveAndEndedCountsForBothProvidersAreReported()
    {
        using var workspace = new TestWorkspace();
        var state = new AppState();
        foreach (var provider in new[] { "claude", "codex" })
        {
            state.Sessions[provider + ":live"] = Session(provider, "live", ended: false);
            state.Sessions[provider + ":ended-a"] = Session(provider, "ended-a", ended: true);
            state.Sessions[provider + ":ended-b"] = Session(provider, "ended-b", ended: true);
        }
        new StateSnapshotStore(workspace.Paths).Save(state);

        var report = Report(workspace, trayRunning: true, DateTimeOffset.UtcNow);

        var file = report.GetProperty("stateFile");
        Assert.Equal(new FileInfo(workspace.Paths.State).Length, file.GetProperty("bytes").GetInt64());
        Assert.Equal(16L * 1024 * 1024, file.GetProperty("budgetBytes").GetInt64());
        var sessions = report.GetProperty("sessions");
        Assert.Equal(2, sessions.GetProperty("live").GetInt32());
        Assert.Equal(4, sessions.GetProperty("ended").GetInt32());
        Assert.Equal(1, sessions.GetProperty("byProvider").GetProperty("codex").GetProperty("live").GetInt32());
        Assert.Equal(2, sessions.GetProperty("byProvider").GetProperty("claude").GetProperty("ended").GetInt32());
        Assert.Contains("200 ended sessions", report.GetProperty("retention").GetString());
    }

    [Theory]
    [InlineData(true, 5, true)]
    [InlineData(true, 0, false)]
    [InlineData(false, 5, false)]
    public void RefreshFailureIsPersistentOnlyAfterAMinuteWhileTheTrayIsRunning(bool trayRunning, int minutesFailing, bool persistent)
    {
        using var workspace = new TestWorkspace();
        var now = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        AtomicFile.Write(workspace.Paths.RefreshHealth, Json.Encode(new RefreshHealthRecord(now.AddMinutes(-minutesFailing).AddSeconds(-1 * (minutesFailing == 0 ? 10 : 0)),
            "InvalidDataException: State exceeds the supported snapshot budget", now.AddDays(-1))));

        var refresh = Report(workspace, trayRunning, now).GetProperty("refresh");

        Assert.True(refresh.GetProperty("failing").GetBoolean());
        Assert.Equal(persistent, refresh.GetProperty("persistent").GetBoolean());
        Assert.Contains("snapshot budget", refresh.GetProperty("reason").GetString());
    }

    [Fact]
    public void RecoveredRefreshIsReportedHealthy()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.UtcNow;
        AtomicFile.Write(workspace.Paths.RefreshHealth, Json.Encode(new RefreshHealthRecord(null, null, now)));

        var refresh = Report(workspace, trayRunning: true, now).GetProperty("refresh");

        Assert.False(refresh.GetProperty("failing").GetBoolean());
        Assert.False(refresh.GetProperty("persistent").GetBoolean());
    }
}
