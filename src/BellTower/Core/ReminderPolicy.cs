namespace BellTower.Core;

public static class ReminderPolicy
{
    public static ReminderAdvance Advance(
        AppState state,
        IReadOnlyCollection<SessionState> actionableSessions,
        DateTimeOffset now,
        TimeSpan? currentIdle,
        ReminderSettings settings,
        string runId,
        IReadOnlySet<string>? retainedEpisodes = null)
    {
        settings.Validate();
        var changed = false;
        var scheduled = new List<ScheduledReminder>();

        if (state.ReminderRunId != runId)
        {
            foreach (var pair in state.ReminderReceipts.ToArray())
            {
                if (pair.Value.NextReminderAt <= now)
                {
                    state.ReminderReceipts[pair.Key] = pair.Value with
                    {
                        NextReminderAt = now + TimeSpan.FromMinutes(settings.RepeatIntervalMinutes)
                    };
                    changed = true;
                }
            }

            state.ReminderRunId = runId;
            changed = true;
        }

        if (state.ReminderSettingsEnabled == false && settings.Enabled)
        {
            foreach (var pair in state.ReminderReceipts.ToArray())
            {
                state.ReminderReceipts[pair.Key] = pair.Value with
                {
                    NextReminderAt = now + TimeSpan.FromMinutes(settings.InitialDelayMinutes),
                    Escalation = null
                };
                changed = true;
            }
        }
        if (state.ReminderSettingsEnabled != settings.Enabled)
        {
            state.ReminderSettingsEnabled = settings.Enabled;
            changed = true;
        }

        if (!settings.Enabled)
        {
            foreach (var pair in state.ReminderReceipts.Where(pair => pair.Value.Escalation is not null).ToArray())
            {
                state.ReminderReceipts[pair.Key] = pair.Value with { Escalation = null };
                changed = true;
            }
        }

        var active = actionableSessions
            .Where(session => session.AttachmentHealth == "verified-live" && session.RequiresAttention(now) && session.Episode is not null && session.Reason is not null)
            .ToDictionary(Key, StringComparer.Ordinal);

        var retained = retainedEpisodes ?? active.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var pair in state.ReminderReceipts.ToArray())
        {
            if (!retained.Contains(pair.Key) || pair.Key != pair.Value.Key + "\n" + pair.Value.Episode)
            {
                state.ReminderReceipts.Remove(pair.Key);
                changed = true;
            }
        }

        if (settings.Enabled)
        {
            foreach (var pair in active)
            {
                if (!state.ReminderReceipts.TryGetValue(pair.Key, out var receipt))
                {
                    if (state.ReminderReceipts.Count >= 10000)
                    {
                        continue;
                    }

                    var start = now + TimeSpan.FromMinutes(settings.InitialDelayMinutes);
                    state.ReminderReceipts[pair.Key] = new(pair.Value.Key, pair.Value.Episode!, now, start, null, 0, Guid.NewGuid().ToString("N"));
                    changed = true;
                    continue;
                }

                if (receipt.Ordinal >= settings.MaxPerEpisode || receipt.NextReminderAt > now || currentIdle is not { } idle ||
                    idle < TimeSpan.FromMinutes(settings.IdleThresholdMinutes) || idle > TimeSpan.FromMilliseconds(int.MaxValue))
                {
                    continue;
                }

                var ordinal = receipt.Ordinal + 1;
                var eligibleAt = now;
                var escalation = receipt.Escalation;
                if (escalation is null && ordinal >= settings.EscalationAfterReminder)
                {
                    escalation = new(1, pair.Value.Key, pair.Value.Episode!, pair.Value.Reason!.Value,
                        pair.Value.EpisodeStarted ?? receipt.FirstEligibleAt, ordinal, eligibleAt, receipt.ReceiptId);
                }

                state.ReminderReceipts[pair.Key] = receipt with
                {
                    Ordinal = ordinal,
                    LastReminderAt = eligibleAt,
                    NextReminderAt = eligibleAt + TimeSpan.FromMinutes(settings.RepeatIntervalMinutes),
                    Escalation = escalation
                };
                scheduled.Add(new(pair.Value.Key, pair.Value.Episode!, pair.Value.Reason!.Value, ordinal));
                changed = true;
            }
        }

        return new(scheduled, changed);
    }

    public static string Key(SessionState session) => session.Key + "\n" + session.Episode;
}
