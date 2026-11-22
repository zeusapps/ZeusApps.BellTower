using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.IO;

// The tray refresh failure log: one entry per reason change or ten minutes, a recovery entry, and a health record written on
// transitions only. The refresh is the shared state drain, so the policy is the same for Claude Code and Codex sessions.
public sealed class RefreshHealthTests
{
    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static string[] Log(TestWorkspace workspace, string code)
    {
        var path = Path.Combine(workspace.Paths.Root, "bell-tower.log");
        return File.Exists(path) ? File.ReadAllLines(path).Where(line => line.Contains(code, StringComparison.Ordinal)).ToArray() : [];
    }

    private static DateTimeOffset Start => new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RepeatedIdenticalFailuresLogOncePerTenMinutesAndAChangedReasonLogsAtOnce()
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        var health = new RefreshHealth(workspace.Paths, clock);
        var failure = new InvalidDataException("State exceeds the supported snapshot budget");

        for (var tick = 0; tick < 310; tick++)
        {
            health.Failed(failure);
            clock.Now = clock.Now.AddSeconds(2);
        }
        // 310 ticks at 2 seconds span just over 10 minutes: the first entry and one repeat.
        Assert.Equal(2, Log(workspace, "tray-refresh-failed").Length);
        Assert.Contains("tray-refresh-failed: InvalidDataException: State exceeds the supported snapshot budget", Log(workspace, "tray-refresh-failed")[0]);

        health.Failed(new IOException("another reason"));

        var entries = Log(workspace, "tray-refresh-failed");
        Assert.Equal(3, entries.Length);
        Assert.EndsWith("IOException: another reason", entries[2]);
    }

    [Fact]
    public void FirstSuccessAfterFailureLogsRecoveryAndUpdatesTheRecordOnTransitionsOnly()
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        var health = new RefreshHealth(workspace.Paths, clock);
        health.Succeeded();
        Assert.False(File.Exists(workspace.Paths.RefreshHealth));

        clock.Now = Start.AddMinutes(1);
        health.Failed(new InvalidDataException("first"));
        var failing = RefreshHealth.Read(workspace.Paths)!;
        Assert.Equal(clock.Now, failing.FailingSince);
        Assert.Equal(Start, failing.LastSuccess);
        Assert.Equal("InvalidDataException: first", failing.Reason);

        // Repeats of the same failure do not rewrite the record.
        var stamp = File.GetLastWriteTimeUtc(workspace.Paths.RefreshHealth);
        clock.Now = Start.AddMinutes(2);
        health.Failed(new InvalidDataException("first"));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(workspace.Paths.RefreshHealth));
        Assert.Equal(Start.AddMinutes(1), RefreshHealth.Read(workspace.Paths)!.FailingSince);

        clock.Now = Start.AddMinutes(3);
        health.Succeeded();
        health.Succeeded();
        Assert.Single(Log(workspace, "tray-refresh-recovered"));
        var recovered = RefreshHealth.Read(workspace.Paths)!;
        Assert.Null(recovered.FailingSince);
        Assert.Null(recovered.Reason);
        Assert.Equal(clock.Now, recovered.LastSuccess);
    }

    [Fact]
    public void ARestartedTrayRecoversAFailureRecordedByTheEarlierRun()
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        new RefreshHealth(workspace.Paths, clock).Failed(new IOException("earlier run"));

        clock.Now = Start.AddMinutes(5);
        var restarted = new RefreshHealth(workspace.Paths, clock);
        restarted.Succeeded();

        Assert.Single(Log(workspace, "tray-refresh-recovered"));
        Assert.Null(RefreshHealth.Read(workspace.Paths)!.FailingSince);
    }

    [Fact]
    public void ReasonAndRecordNeverContainTheDataDirectoryOrOtherPathsAndAreBounded()
    {
        using var workspace = new TestWorkspace();
        var health = new RefreshHealth(workspace.Paths, new ManualTime(Start));
        var message = $"Could not write '{workspace.Paths.State}' and '{workspace.Paths.Root.Replace('\\', '/')}/x' beside C:\\Users\\someone\\secret\\file.txt " +
            "or \\\\server\\share\\a and /home/someone/b" + new string('x', 500);

        health.Failed(new IOException(message));

        var log = string.Join('\n', Log(workspace, "tray-refresh-failed"));
        var record = File.ReadAllText(workspace.Paths.RefreshHealth);
        foreach (var text in new[] { log, record })
        {
            Assert.DoesNotContain(workspace.Paths.Root, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("someone", text);
            Assert.DoesNotContain("server", text);
            Assert.DoesNotContain("secret", text);
        }
        Assert.Contains("<data>", log);
        Assert.Contains("<path>", log);
        var reason = RefreshHealth.Read(workspace.Paths)!.Reason!;
        Assert.True(reason.Length <= RefreshHealth.MaxReasonLength);
        Assert.StartsWith("IOException: ", reason);
    }

    // Overwrites the record with a marker: a later write by the health recorder replaces it, so the marker proves no write happened.
    private static void Mark(TestWorkspace workspace)
    {
        File.WriteAllText(workspace.Paths.RefreshHealth, Json.Encode(new RefreshHealthRecord(Start, "marker", null)));
    }

    private static bool Marked(TestWorkspace workspace) => RefreshHealth.Read(workspace.Paths)!.Reason == "marker";

    [Fact]
    public void ARepeatedFailureAfterTheIntervalLogsAgainButDoesNotRewriteTheRecord()
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        var health = new RefreshHealth(workspace.Paths, clock);
        health.Failed(new InvalidDataException("same"));
        Mark(workspace);

        clock.Now = Start.AddMinutes(11);
        health.Failed(new InvalidDataException("same"));
        clock.Now = Start.AddMinutes(23);
        health.Failed(new InvalidDataException("same"));

        Assert.Equal(3, Log(workspace, "tray-refresh-failed").Length);
        Assert.True(Marked(workspace));
    }

    [Fact]
    public void AlternatingReasonsLogOncePerReasonAndWriteTheRecordOnlyOnTransitions()
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        var health = new RefreshHealth(workspace.Paths, clock);
        health.Failed(new IOException("first reason"));
        Assert.Equal("IOException: first reason", RefreshHealth.Read(workspace.Paths)!.Reason);
        Mark(workspace);

        for (var tick = 0; tick < 100; tick++)
        {
            clock.Now = Start.AddSeconds(tick * 5 + 1);
            health.Failed(tick % 2 == 0 ? new InvalidDataException("second reason") : new IOException("first reason"));
        }

        // 500 seconds: one entry for each distinct reason, and no record write after the one that started the window.
        var entries = Log(workspace, "tray-refresh-failed");
        Assert.Equal(2, entries.Length);
        Assert.EndsWith("IOException: first reason", entries[0]);
        Assert.EndsWith("InvalidDataException: second reason", entries[1]);
        Assert.True(Marked(workspace));

        // A persisted reason change is written at most once per interval, with the current reason.
        clock.Now = Start.AddMinutes(11);
        health.Failed(new InvalidDataException("second reason"));
        var record = RefreshHealth.Read(workspace.Paths)!;
        Assert.Equal("InvalidDataException: second reason", record.Reason);
        Assert.Equal(Start, record.FailingSince);
        Assert.Equal(3, Log(workspace, "tray-refresh-failed").Length);
        Mark(workspace);
        clock.Now = Start.AddMinutes(12);
        health.Failed(new IOException("first reason"));
        health.Failed(new IOException("third reason"));
        Assert.True(Marked(workspace));
    }

    // ANALYSIS-102: only entries older than the interval are evicted, so no stream of reasons can log once per tick.
    [Fact]
    public void ManyDistinctReasonsLogAtMostTheRememberedNumberPlusOneNoticePerInterval()
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        var health = new RefreshHealth(workspace.Paths, clock);

        for (var i = 0; i < 40; i++)
        {
            clock.Now = Start.AddSeconds(i);
            health.Failed(new IOException("reason " + i));
        }

        var entries = Log(workspace, "tray-refresh-failed");
        Assert.Equal(9, entries.Length);
        Assert.EndsWith("IOException: reason 7", entries[7]);
        Assert.EndsWith("tray-refresh-failed: further reasons suppressed", entries[8]);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(12)]
    [InlineData(100)]
    public void RotatingOrEverChangingReasonsStayBoundedOverOneHundredTicksWithinTenMinutes(int distinct)
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        var health = new RefreshHealth(workspace.Paths, clock);

        for (var tick = 0; tick < 100; tick++)
        {
            clock.Now = Start.AddSeconds(tick * 5);
            // The policy is shared by both providers' refreshes, so exception types vary as well as texts.
            health.Failed(tick % 2 == 0 ? new IOException("reason " + tick % distinct) : new InvalidDataException("reason " + tick % distinct));
        }

        Assert.InRange(Log(workspace, "tray-refresh-failed").Length, 1, 9);
    }

    [Fact]
    public void EntriesOlderThanTheIntervalAreEvictedSoNewReasonsLogAgainAndTheNoticeRepeatsOncePerInterval()
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        var health = new RefreshHealth(workspace.Paths, clock);
        for (var i = 0; i < 12; i++)
        {
            clock.Now = Start.AddSeconds(i);
            health.Failed(new IOException("reason " + i));
        }
        Assert.Equal(9, Log(workspace, "tray-refresh-failed").Length);

        // After the interval the remembered reasons are stale: a new reason logs, and later suppression gives a new notice.
        clock.Now = Start.AddMinutes(11);
        health.Failed(new IOException("fresh reason"));
        Assert.EndsWith("IOException: fresh reason", Log(workspace, "tray-refresh-failed")[^1]);
        for (var i = 0; i < 12; i++)
        {
            clock.Now = Start.AddMinutes(11).AddSeconds(i + 1);
            health.Failed(new IOException("later reason " + i));
        }

        var entries = Log(workspace, "tray-refresh-failed");
        Assert.Equal(2, entries.Count(line => line.EndsWith("further reasons suppressed", StringComparison.Ordinal)));
        // 8 + notice, then the fresh reason, 7 later reasons and a second notice.
        Assert.Equal(18, entries.Length);
    }

    private const string Profile = @"C:\Users\First Last";

    private static string Mask(string message) => RefreshHealth.Describe(new IOException(message), @"D:\data\belltower", Profile, "jdoe");

    [Theory]
    [InlineData(@"Access to the path 'C:\Users\First Last\.codex\sessions\x.jsonl' is denied.", "IOException: Access to the path '<path>' is denied.")]
    [InlineData(@"Access to the path 'c:/users/first last/.claude/x' is denied.", "IOException: Access to the path '<path>' is denied.")]
    [InlineData(@"Could not read C:\Users\First Last\.codex\config.toml now", "IOException: Could not read <path> now")]
    [InlineData(@"Access to the path 'D:\work\repo name\sub dir\file.txt' is denied.", "IOException: Access to the path '<path>' is denied.")]
    [InlineData(@"Could not open D:\work\repo name\sub dir\file.txt now", "IOException: Could not open <path> now")]
    [InlineData(@"Share \\server\my share\dir\a.txt unavailable", "IOException: Share <path> unavailable")]
    [InlineData("Denied '/root/secret dir/a' here", "IOException: Denied '<path>' here")]
    [InlineData("Denied /root/secret/a today", "IOException: Denied <path> today")]
    [InlineData("Denied /mnt/c/Some Dir/b today", "IOException: Denied <path> today")]
    [InlineData("Home /home/First Last/x failed", "IOException: Home <path> failed")]
    [InlineData("Root /root failed", "IOException: Root <path> failed")]
    [InlineData(@"Profile C:\Users\First Last is full", "IOException: Profile <path> is full")]
    [InlineData("Quota for jdoe exceeded, not jdoes", "IOException: Quota for <user> exceeded, not jdoes")]
    public void PathsInAReasonAreMaskedWhateverTheirRootOrSpacesAndTheRestOfTheTextIsKept(string message, string expected)
    {
        var text = Mask(message);

        Assert.Equal(expected, text);
        foreach (var fragment in new[] { "\\", "First", "Last", "repo", "sub dir", "my share", "secret", "Some", "server", "worker" })
        {
            Assert.DoesNotContain(fragment, text);
        }
    }

    [Fact]
    public void TheDataDirectoryAndItsFilesAreReplacedAndOtherTextIsKept()
    {
        var text = RefreshHealth.Describe(new InvalidDataException(@"Cannot write 'D:\data\belltower\state.json' beside D:\data\belltower\state.json.bak: disk full"),
            @"D:\data\belltower", Profile, "jdoe");

        Assert.Equal("InvalidDataException: Cannot write '<data>' beside <data>: disk full", text);
    }

    [Fact]
    public void TheCurrentUsersProfileAndNameAreMaskedWithoutPassingThem()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var text = RefreshHealth.Describe(new IOException($@"Cannot read '{profile}\x y\z' for {Environment.UserName}"), @"D:\data\belltower");

        Assert.DoesNotContain(profile, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\", text);
        Assert.Contains("<path>", text);
    }

    // ANALYSIS-100: a name with an apostrophe must not end the quoted segment before the exact profile and name rules run.
    [Theory]
    [InlineData(@"Access to the path 'C:\Users\O'Brien\.codex\sessions\x.jsonl' is denied.", "IOException: Access to the path '<path>' is denied.")]
    [InlineData(@"Access to the path 'D:\work\O'Brien\file.txt' is denied.", "IOException: Access to the path '<path>' is denied.")]
    [InlineData("Quota for O'Brien exceeded", "IOException: Quota for <user> exceeded")]
    [InlineData(@"Could not read C:\Users\O'Brien\.claude\x now", "IOException: Could not read <path> now")]
    public void AProfileOrUserNameWithAnApostropheLeaksNeitherTheNameNorTheRestOfThePath(string message, string expected)
    {
        var text = RefreshHealth.Describe(new IOException(message), @"E:\data\belltower", @"C:\Users\O'Brien", "O'Brien");

        Assert.Equal(expected, text);
        foreach (var fragment in new[] { "\\", "Brien", "sessions", ".codex", ".claude" })
        {
            Assert.DoesNotContain(fragment, text);
        }
    }

    // ANALYSIS-200: an apostrophe inside any path must not end the masked segment early.
    [Theory]
    [InlineData(@"Access to the path 'D:\Code\Bob's app\.git\index.lock' is denied.", "IOException: Access to the path '<path>' is denied.")]
    [InlineData(@"Could not open D:\Code\Bob's app\.git\index.lock now", "IOException: Could not open <path> now")]
    [InlineData(@"Could not find file 'D:\Clients\Smith's Project\notes\plan.md'.", "IOException: Could not find file '<path>'.")]
    [InlineData(@"Access to the path 'E:\codex-home\Bob's\sessions\x.jsonl' is denied.", "IOException: Access to the path '<path>' is denied.")]
    [InlineData(@"Denied 'D:\a\Bob's x\f' and 'E:\b\Al's y\g' today", "IOException: Denied '<path>' today")]
    public void AnApostropheInAPathOutsideTheProfileLeaksNoFragmentOfThePath(string message, string expected)
    {
        var text = Mask(message);

        Assert.Equal(expected, text);
        foreach (var fragment in new[] { "\\", "s app", "Project", "plan", "sessions", "index", "Smith", "Bob", "Al's" })
        {
            Assert.DoesNotContain(fragment, text);
        }
    }

    private static int Entries(TestWorkspace workspace) => Log(workspace, "tray-refresh-").Length;

    // ANALYSIS-201: a recovery must not reset the limiter, whatever the mix of failures and successes.
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void FailuresInterleavedWithSuccessesStayWithinTheBoundOfAContinuousFailure(int successEvery)
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        var health = new RefreshHealth(workspace.Paths, clock);

        for (var tick = 0; tick < 100; tick++)
        {
            clock.Now = Start.AddSeconds(tick * 5);
            // successEvery 1 strictly alternates; 4 fails one tick in four. The last tick is a success in both.
            var fails = successEvery == 1 ? tick % 2 == 0 : tick % 4 == 0;
            if (fails)
            {
                health.Failed(new IOException("flaky"));
            }
            else
            {
                health.Succeeded();
            }
        }

        // 500 seconds: one failure entry and one recovery entry, and a record that matches the final outcome.
        Assert.Single(Log(workspace, "tray-refresh-failed"));
        Assert.InRange(Entries(workspace), 1, 9);
        var record = RefreshHealth.Read(workspace.Paths)!;
        Assert.Null(record.FailingSince);
        Assert.Null(record.Reason);
        // The record changes on transitions only: it holds the recovery that ended the one logged failure.
        Assert.Equal(Start.AddSeconds(5), record.LastSuccess);
    }

    // ANALYSIS-300 and QA-300: an apostrophe in any position of a path (before a space, a digit or a separator) leaks nothing.
    [Theory]
    [InlineData(@"Could not find file 'D:\Clients\Jones' Project\notes\plan.md'.")]
    [InlineData(@"Could not open D:\Clients\Jones' Project\notes\plan.md now")]
    [InlineData(@"Access to the path 'D:\Shared\Students' Work\grades.xlsx' is denied.")]
    [InlineData(@"Access to the path 'D:\Music\Rock 'n' Roll\x.mp3' is denied.")]
    [InlineData(@"Could not open D:\Music\Rock 'n' Roll\x.mp3 now")]
    [InlineData(@"Could not open D:\Music\'90s\mix.mp3 now")]
    [InlineData(@"Could not open 'D:\Music\'90s\mix.mp3' now")]
    [InlineData(@"Failed 'D:\a\Bob'\f' end")]
    [InlineData(@"Failed D:\a\Bob'\f end")]
    [InlineData(@"Failed C:\Users\First Last\Students' Work\notes\plan.md now")]
    [InlineData(@"Failed 'C:\Users\First Last\Students' Work\notes\plan.md' now")]
    [InlineData(@"Failed \\server\share\Students' Work\x.txt now")]
    [InlineData(@"Failed '\\server\share\Students' Work\x.txt' now")]
    [InlineData("Failed '/srv/Students' Work/y.txt' now")]
    [InlineData("Failed /srv/Students' Work/y.txt now")]
    [InlineData("Failed /home/First Last/Students' Work/y.txt now")]
    [InlineData("Failed /mnt/c/Rock 'n' Roll/y.txt now")]
    [InlineData("Failed /srv/'90s/y.txt now")]
    [InlineData(@"Access to the path 'D:\Shared\Students' Work.xlsx'.")]
    [InlineData(@"Could not find 'D:\Music\Rock 'n' Roll.mp3'")]
    [InlineData(@"Could not open 'C:\Users\First Last\source\My Repo'")]
    [InlineData(@"Could not open 'C:\Users\First Last\source\My Repo' now")]
    [InlineData(@"Denied '\\server\share\Rock 'n' Roll.mp3'.")]
    [InlineData("Denied '/srv/Students' Work.txt'.")]
    [InlineData("Denied '/home/First Last/My Repo/Rock 'n' Roll.txt';")]
    public void AnApostropheBeforeASpaceDigitOrSeparatorInAPathLeaksNoFragment(string message)
    {
        var text = Mask(message);

        foreach (var fragment in new[] { "\\", "/", "Project", "Work", "Roll", "90s", "plan", "grades", "mix", "First", "Last", "notes", "Jones", "Students", "Repo", "source", "xlsx", "mp3", "txt" })
        {
            Assert.DoesNotContain(fragment, text);
        }
        Assert.Contains("<path>", text);
    }

    // ANALYSIS-300 residual: a quoted segment from a path root is masked through the last closing quote on its line, so two quoted paths
    // on one line leave no separator and no name fragment; the text between them may be masked too, the text after the last close is kept.
    [Theory]
    [InlineData(@"Denied 'D:\a\x' and 'E:\b\y' today")]
    [InlineData(@"Denied 'D:\a\Bob's x\f' and 'E:\b\Al's y\g' today")]
    [InlineData(@"Denied 'D:\Clients\Jones' Project\x' and 'E:\Al's y\g' today")]
    [InlineData(@"Denied 'C:\Users\First Last\My Repo\a' and '\\server\Rock 'n' Roll\b' today")]
    [InlineData("Denied '/srv/Students' Work/a' and '/home/First Last/b' today")]
    public void TwoQuotedPathsOnOneLineLeaveNoFragmentAndKeepTheTextAfterTheLastClose(string message)
    {
        var text = Mask(message);

        Assert.Equal("IOException: Denied '<path>' today", text);
        foreach (var fragment in new[] { "\\", "/", "Bob", "Al", "Jones", "Project", "Students", "Work", "Rock", "Roll", "First", "Repo", "server", "Clients" })
        {
            Assert.DoesNotContain(fragment, text);
        }
    }

    [Fact]
    public void ATrailingMessageAfterTheLastClosingQuoteIsKept()
    {
        Assert.Equal("IOException: Access to the path '<path>' is denied.", Mask(@"Access to the path 'D:\Shared\Students' Work.xlsx' is denied."));
        Assert.Equal("IOException: Could not read '<path>', retry later", Mask(@"Could not read 'C:\Users\First Last\source\My Repo', retry later"));
    }

    // QA-500: a closing quote is an apostrophe not followed by a letter or digit, so a close before any punctuation (] } ? " > - / or an ellipsis)
    // ends the masked segment at the last close; the text after it is kept.
    [Theory]
    [InlineData(@"Denied 'C:\a\My Repo']", "Denied '<path>']")]
    [InlineData(@"Denied ['C:\a\My Repo']", "Denied ['<path>']")]
    [InlineData(@"Denied 'D:\Secret' dir\Secret One'?", "Denied '<path>'?")]
    [InlineData(@"Denied 'C:\Users\Jo Doe\x y'/z q", "Denied '<path>'/z q")]
    [InlineData(@"Denied {'C:\a\My Repo'} now", "Denied {'<path>'} now")]
    [InlineData("Denied \"'C:\\a\\My Repo'\" now", "Denied \"'<path>'\" now")]
    [InlineData(@"Denied <'C:\a\My Repo'> now", "Denied <'<path>'> now")]
    [InlineData(@"Denied 'C:\a\My Repo'- now", "Denied '<path>'- now")]
    [InlineData("Denied 'C:\\a\\My Repo'\u2026 now", "Denied '<path>'\u2026 now")]
    public void AClosingQuoteBeforeAnyPunctuationLeavesNoNameFragmentAndKeepsTheTrailingText(string message, string expected)
    {
        var text = Mask(message);

        Assert.Equal("IOException: " + expected, text);
        foreach (var fragment in new[] { "\\", "Secret", "Jo", "Doe", "Repo", "My", "dir", "One", "C:" })
        {
            Assert.DoesNotContain(fragment, text);
        }
    }

    // Bounded cost: a crafted message cannot hold the refresh for long, and a rule that runs out keeps only the exception type.
    [Theory]
    [InlineData(@"\'a")]
    [InlineData(@"'a/")]
    [InlineData(@"/'a\ ")]
    [InlineData("'x\\ ")]
    [InlineData("'/a ")]
    [InlineData("'C:\\a ")]
    [InlineData("'\\\\ ")]
    public void ACraftedMessageCompletesQuicklyAndLeaksNoPath(string unit)
    {
        var message = "C:\\Secret\\" + string.Concat(Enumerable.Repeat(unit, 2000 / unit.Length + 1))[..2000];
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var text = RefreshHealth.Describe(new IOException(message), @"D:\data\belltower", Profile, "jdoe");

        watch.Stop();
        Assert.True(watch.ElapsedMilliseconds < 1000, watch.ElapsedMilliseconds + " ms");
        Assert.DoesNotContain("Secret", text);
        // Text after the last close is kept (QA-500), so a message cut right after a close may end in the separators the crafted unit repeats.
        Assert.DoesNotContain("\\", text.TrimEnd('\\'));
        Assert.StartsWith("IOException", text);
    }

    // ANALYSIS-301: a failure that goes on is recorded after RecordAfter even when its log line is suppressed.
    [Fact]
    public void AFailureWindowIsRecordedOnceItHasLastedThirtySecondsEvenWhenItsLineIsSuppressed()
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        var health = new RefreshHealth(workspace.Paths, clock);
        health.Failed(new IOException("flaky"));
        clock.Now = Start.AddSeconds(1);
        health.Succeeded();

        // The window starts at 5 seconds; its line is suppressed by the limiter.
        clock.Now = Start.AddSeconds(5);
        health.Failed(new IOException("flaky"));
        Assert.Null(RefreshHealth.Read(workspace.Paths)!.FailingSince);
        clock.Now = Start.AddSeconds(34);
        health.Failed(new IOException("flaky"));
        Assert.Null(RefreshHealth.Read(workspace.Paths)!.FailingSince);
        clock.Now = Start.AddSeconds(36);
        health.Failed(new IOException("flaky"));

        var record = RefreshHealth.Read(workspace.Paths)!;
        Assert.Equal(Start.AddSeconds(5), record.FailingSince);
        Assert.Equal("IOException: flaky", record.Reason);
        Assert.Single(Log(workspace, "tray-refresh-failed"));

        // Recovery is recorded because the record shows a failure, although its line was never logged.
        clock.Now = Start.AddSeconds(40);
        health.Succeeded();
        Assert.Null(RefreshHealth.Read(workspace.Paths)!.FailingSince);
        Assert.Single(Log(workspace, "tray-refresh-recovered"));
    }

    [Fact]
    public void ALoggedFailureThatRecoveredAndFailsAgainForFifteenMinutesIsRecordedWithItsWindowStart()
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        var health = new RefreshHealth(workspace.Paths, clock);
        health.Failed(new IOException("A"));
        clock.Now = Start.AddSeconds(2);
        health.Succeeded();
        var windowStart = Start.AddSeconds(4);

        for (var second = 4; second <= 15 * 60; second += 2)
        {
            clock.Now = Start.AddSeconds(second);
            health.Failed(new IOException("A"));
        }

        var record = RefreshHealth.Read(workspace.Paths)!;
        Assert.NotNull(record.FailingSince);
        Assert.InRange((record.FailingSince!.Value - windowStart).TotalSeconds, 0, 60);
        Assert.Equal(windowStart, record.FailingSince);
        Assert.Equal("IOException: A", record.Reason);
    }

    // QA-301: failure and suppression lines are limited per reason; recovery lines add at most one per logged failure.
    [Fact]
    public void AlternatingManyReasonsWithSuccessesStaysWithinSeventeenLinesPerTenMinutes()
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        var health = new RefreshHealth(workspace.Paths, clock);

        for (var tick = 0; tick < 2400; tick++)
        {
            clock.Now = Start.AddMilliseconds(tick * 250);
            if (tick % 2 == 0)
            {
                health.Failed(new IOException("reason " + tick));
            }
            else
            {
                health.Succeeded();
            }
        }

        // 600 seconds: at most 8 remembered reasons and 1 suppression notice, and one recovery for each logged failure.
        Assert.InRange(Log(workspace, "tray-refresh-failed").Length, 1, 9);
        Assert.InRange(Log(workspace, "tray-refresh-recovered").Length, 1, 8);
        Assert.InRange(Entries(workspace), 2, 17);
    }

    [Fact]
    public void AFailureStillFailingAfterTheIntervalIsLoggedAndRecordedAndEachEndedFailureIsRecovered()
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        var health = new RefreshHealth(workspace.Paths, clock);
        health.Failed(new IOException("flaky"));
        clock.Now = Start.AddSeconds(1);
        health.Succeeded();
        clock.Now = Start.AddSeconds(2);
        health.Failed(new IOException("flaky"));
        clock.Now = Start.AddSeconds(3);
        health.Succeeded();

        // The second failure was not logged, so its recovery is not logged either.
        Assert.Single(Log(workspace, "tray-refresh-failed"));
        Assert.Single(Log(workspace, "tray-refresh-recovered"));
        Assert.Null(RefreshHealth.Read(workspace.Paths)!.FailingSince);

        // A suppressed failure that goes on is recorded after 30 seconds, and logged again after the interval.
        clock.Now = Start.AddSeconds(4);
        health.Failed(new IOException("flaky"));
        Assert.Single(Log(workspace, "tray-refresh-failed"));
        Assert.Null(RefreshHealth.Read(workspace.Paths)!.FailingSince);
        clock.Now = Start.AddSeconds(40);
        health.Failed(new IOException("flaky"));
        Assert.Single(Log(workspace, "tray-refresh-failed"));
        Assert.Equal(Start.AddSeconds(4), RefreshHealth.Read(workspace.Paths)!.FailingSince);
        clock.Now = Start.AddMinutes(11);
        health.Failed(new IOException("flaky"));
        Assert.Equal(2, Log(workspace, "tray-refresh-failed").Length);
        var record = RefreshHealth.Read(workspace.Paths)!;
        Assert.Equal(Start.AddSeconds(4), record.FailingSince);
        Assert.Equal("IOException: flaky", record.Reason);

        clock.Now = Start.AddMinutes(12);
        health.Succeeded();
        Assert.Null(RefreshHealth.Read(workspace.Paths)!.FailingSince);
        Assert.Equal(2, Log(workspace, "tray-refresh-recovered").Length);

        // A first failure after a healthy period longer than the interval logs at once.
        clock.Now = Start.AddMinutes(40);
        health.Failed(new IOException("flaky"));
        Assert.Equal(3, Log(workspace, "tray-refresh-failed").Length);
        Assert.NotNull(RefreshHealth.Read(workspace.Paths)!.FailingSince);
    }

    // A window recorded after RecordAfter whose own line was suppressed gets one extra recovery line after a restart (documented, bounded).
    [Fact]
    public void ARestartAfterARecordedWindowWithASuppressedLineLogsOneRecoveryLine()
    {
        using var workspace = new TestWorkspace();
        var clock = new ManualTime(Start);
        var health = new RefreshHealth(workspace.Paths, clock);
        health.Failed(new IOException("flaky"));
        clock.Now = Start.AddSeconds(1);
        health.Succeeded();
        clock.Now = Start.AddSeconds(5);
        health.Failed(new IOException("flaky"));
        clock.Now = Start.AddSeconds(40);
        health.Failed(new IOException("flaky"));
        Assert.Equal(Start.AddSeconds(5), RefreshHealth.Read(workspace.Paths)!.FailingSince);
        Assert.Single(Log(workspace, "tray-refresh-failed"));
        Assert.Single(Log(workspace, "tray-refresh-recovered"));

        var restarted = new RefreshHealth(workspace.Paths, clock);
        clock.Now = Start.AddSeconds(50);
        restarted.Succeeded();

        Assert.Null(RefreshHealth.Read(workspace.Paths)!.FailingSince);
        Assert.Equal(2, Log(workspace, "tray-refresh-recovered").Length);
        restarted.Succeeded();
        Assert.Equal(2, Log(workspace, "tray-refresh-recovered").Length);
    }
}
