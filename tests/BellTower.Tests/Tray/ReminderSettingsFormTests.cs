using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Tests.Testing;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Tray;

public sealed class ReminderSettingsFormTests
{
    [Fact]
    public void SaveMappingPreservesLatestRootAndNestedUnknownFields()
    {
        using var workspace = new TestWorkspace();
        using var rootExtension = JsonDocument.Parse("{\"version\":1}");
        using var reminderExtension = JsonDocument.Parse("{\"version\":1}");
        var config = new ConfigStore(workspace.Paths);
        config.Update(value =>
        {
            value.AdditionalData = new() { ["futureRoot"] = rootExtension.RootElement.Clone() };
            value.Reminders.AdditionalData = new() { ["futureReminder"] = reminderExtension.RootElement.Clone() };
        });

        // Simulate an independent compatible writer changing fields after the form's values were read.
        config.Update(value =>
        {
            using var latestRoot = JsonDocument.Parse("{\"latest\":true}");
            using var latestReminder = JsonDocument.Parse("{\"latest\":true}");
            value.AdditionalData!["futureRoot"] = latestRoot.RootElement.Clone();
            value.Reminders.AdditionalData!["futureReminder"] = latestReminder.RootElement.Clone();
        });

        var edited = new ReminderSettings
        {
            Enabled = false,
            InitialDelayMinutes = 8,
            IdleThresholdMinutes = 9,
            RepeatIntervalMinutes = 11,
            MaxPerEpisode = 4,
            EscalationAfterReminder = 2
        };
        config.Update(value => ReminderSettingsForm.ApplySettings(value, edited));
        var reloaded = new ConfigStore(workspace.Paths).Read();
        Assert.True(reloaded.AdditionalData!["futureRoot"].GetProperty("latest").GetBoolean());
        Assert.True(reloaded.Reminders.AdditionalData!["futureReminder"].GetProperty("latest").GetBoolean());
        Assert.Equal(edited.Enabled, reloaded.Reminders.Enabled);
        Assert.Equal(edited.InitialDelayMinutes, reloaded.Reminders.InitialDelayMinutes);
        Assert.Equal(edited.IdleThresholdMinutes, reloaded.Reminders.IdleThresholdMinutes);
        Assert.Equal(edited.RepeatIntervalMinutes, reloaded.Reminders.RepeatIntervalMinutes);
        Assert.Equal(edited.MaxPerEpisode, reloaded.Reminders.MaxPerEpisode);
        Assert.Equal(edited.EscalationAfterReminder, reloaded.Reminders.EscalationAfterReminder);
    }

    [Fact]
    public void FailedUpdateValidationDoesNotPoisonLastGoodConfiguration()
    {
        using var workspace = new TestWorkspace();
        var config = new ConfigStore(workspace.Paths);
        var before = config.Read();
        Assert.Throws<ArgumentOutOfRangeException>(() => config.Update(value => value.Reminders.MaxPerEpisode = 0));
        var after = config.Read();
        Assert.Equal(before.Reminders, after.Reminders);
        Assert.False(File.Exists(workspace.Paths.Config));
    }
}
