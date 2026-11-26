using System.Text.Json;
using BellTower.FieldAcceptance.Evidence;
using BellTower.FieldAcceptance.Isolation;
using Xunit;

namespace BellTower.Tests.FieldAcceptance;

public sealed class RunReportTests
{
    private static RunReport Report(bool isolationPassed = true)
    {
        var report = new RunReport { Command = "scripts/field-acceptance.ps1 -Scenario demo" };
        report.Baseline = new("v0.1.9", "published", "bell-tower-0.1.9-win-x64.zip", "aa", "aa", "aa", true, "Matched.");
        report.Candidate = new("fixture-version candidate", "0.1.11", "bell-tower-0.1.11-win-x64.zip", "bb", "scripts/package.ps1 -FixtureVersion 0.1.11");
        report.Isolation = new([], [], [], [@"C:\Users\someone\.claude"], [new SnapshotChange("file", @"C:\Users\someone\.claude\settings.json", "length changed", true)], isolationPassed, "note");
        report.Checks.Add(new("demo", "one", ProviderNames.Claude, EvidenceLabel.InstalledHarness, true, "accepted 7 of 7"));
        report.Checks.Add(new("demo", "two", ProviderNames.Codex, EvidenceLabel.InstalledHarness, true, "a | pipe\nline"));
        report.Checks.Add(new("demo", "three", ProviderNames.Mixed, EvidenceLabel.Fixture, true, "mixed"));
        return report;
    }

    [Fact]
    public void ThePassedFlagNeedsEveryCheckAndTheIsolationResult()
    {
        Assert.True(Report().Passed);
        Assert.False(Report(isolationPassed: false).Passed);
        var failing = Report();
        failing.Checks.Add(new("demo", "four", ProviderNames.Codex, EvidenceLabel.InstalledHarness, false, "bad"));
        Assert.False(failing.Passed);
        Assert.False(new RunReport().Passed);
    }

    [Fact]
    public void TheMarkdownSeparatesProvidersAndEvidenceLabelsAndAppliesTheSanitizer()
    {
        var text = Report().ToMarkdown(value => value.Replace(@"C:\Users\someone", "%USERPROFILE%"));
        Assert.Contains("| one | claude | InstalledHarness | pass | accepted 7 of 7 |", text);
        Assert.Contains("| two | codex | InstalledHarness | pass | a \\| pipe line |", text);
        Assert.Contains("| three | mixed | Fixture | pass | mixed |", text);
        Assert.Contains("fixture-version candidate", text);
        Assert.Contains("%USERPROFILE%", text);
        Assert.DoesNotContain("someone", text);
    }

    [Fact]
    public void TheJsonCarriesEveryCheckAndTheBaselineProvenance()
    {
        using var document = JsonDocument.Parse(Report().ToJson());
        var root = document.RootElement;
        Assert.True(root.GetProperty("passed").GetBoolean());
        Assert.Equal(3, root.GetProperty("checks").GetArrayLength());
        Assert.Equal("v0.1.9", root.GetProperty("baseline").GetProperty("tag").GetString());
        Assert.Equal("InstalledHarness", root.GetProperty("checks")[0].GetProperty("label").GetString());
    }

    [Fact]
    public void TheSanitizerReplacesPrivatePathsNamesAndForms()
    {
        var sanitizer = new EvidenceSanitizer(@"D:\Code\Repo", @"C:\Users\alice", @"C:\Users\alice\AppData\Local", @"C:\Users\alice\AppData\Roaming", @"C:\Users\alice\AppData\Local\Temp", "alice", "WORKSTATION-7");
        var text = sanitizer.Sanitize(@"run D:\Code\Repo\artifacts and D:/Code/Repo/x, C:\Users\alice\.claude, C:\Users\alice\AppData\Local\Temp\t, alice on WORKSTATION-7; ""D:\\Code\\Repo""");
        Assert.Equal(@"run <repo>\artifacts and <repo>/x, %USERPROFILE%\.claude, %TEMP%\t, <user> on <host>; ""<repo>""", text);
        Assert.False(sanitizer.ContainsPrivateValue(text));
        Assert.True(sanitizer.ContainsPrivateValue("leftover alice"));
    }

    [Fact]
    public void TheSanitizerKeepsNamesThatOnlyContainTheUserName()
    {
        var sanitizer = new EvidenceSanitizer(@"D:\Code\Repo", @"C:\Users\alice", @"C:\Users\alice\AppData\Local", @"C:\Users\alice\AppData\Roaming", @"C:\Temp", "alice", "WORKSTATION-7");
        Assert.Equal("malicers and <user>", sanitizer.Sanitize("malicers and alice"));
    }

    [Fact]
    public void AnExpectedFailureCheckIsReportedSeparatelyFromHarnessFailures()
    {
        var report = Report();
        report.Checks.Add(new("demo", "known-defect", ProviderNames.Codex, EvidenceLabel.InstalledHarness, true, "0 of 7 accepted", ExpectedFailure: true));
        Assert.True(report.Passed);
        Assert.Equal(1, report.ExpectedFailures);
        Assert.Equal(0, report.HarnessFailures);
        var text = report.ToMarkdown();
        Assert.Contains("| known-defect | codex | InstalledHarness | expected failure (observed) | 0 of 7 accepted |", text);
        Assert.Contains("0 harness failures; 1 expected-failure checks", text);

        // An expected failure that did not show up is a failure of the harness' prediction.
        report.Checks.Add(new("demo", "known-defect-2", ProviderNames.Codex, EvidenceLabel.InstalledHarness, false, "7 of 7 accepted", ExpectedFailure: true));
        Assert.False(report.Passed);
        Assert.Equal(1, report.HarnessFailures);
        Assert.Contains("FAIL (expected failure not observed)", report.ToMarkdown());
    }

    [Fact]
    public void ThePreflightRemovalsAreListedInTheIsolationSection()
    {
        var report = Report();
        report.Isolation = report.Isolation! with { PreflightRemovals = [@"scheduled task ZeusApps.BellTowerHookStart-aaaa (deleted)"] };
        Assert.Contains("Preflight sweep", report.ToMarkdown());
        Assert.Contains("HookStart-aaaa", report.ToMarkdown());
        Assert.Contains("none found", Report().ToMarkdown());
    }
}
