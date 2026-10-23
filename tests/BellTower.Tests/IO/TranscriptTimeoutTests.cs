using System.Text.Json;
using BellTower.Diagnostics;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.IO;

public sealed class TranscriptTimeoutTests
{
    [Fact]
    public void RetainedClaudeTimeoutCaptureSummarizesWithoutPrivateText()
    {
        using var workspace = new TestWorkspace();
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hooks", "claude-2.1.306-timeout.jsonl");
        var path = Path.Combine(workspace.Root, "transcript.jsonl");
        File.WriteAllText(path, "{\"type\":\"user\",\"content\":\"private-prompt\"}\n" + File.ReadAllText(fixture) + "{\"partial\":");
        var before = File.ReadAllBytes(path);
        var output = Json.Encode(new TranscriptTimeoutReader().Read("claude", path));
        using var report = JsonDocument.Parse(output);
        Assert.Equal(2, report.RootElement.GetProperty("timeouts").GetInt32());
        Assert.True(report.RootElement.GetProperty("trailingPartialLineIgnored").GetBoolean());
        Assert.DoesNotContain("private", output);
        Assert.DoesNotContain("command", output);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(Path.Combine(workspace.Paths.Root, "bell-tower.log")));
    }

    [Fact]
    public void ScanIsBoundedAndDoesNotTreatUnverifiedCodexDataAsNoTimeouts()
    {
        using var workspace = new TestWorkspace();
        var path = Path.Combine(workspace.Root, "transcript.jsonl");
        File.WriteAllText(path, new string('x', TranscriptTimeoutReader.MaximumBytes + 100) + "\n" +
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "hooks", "claude-2.1.306-timeout.jsonl")));
        using var report = JsonDocument.Parse(Json.Encode(new TranscriptTimeoutReader().Read("claude", path)));
        Assert.True(report.RootElement.GetProperty("scannedBytes").GetInt32() <= TranscriptTimeoutReader.MaximumBytes);
        Assert.Equal(2, report.RootElement.GetProperty("timeouts").GetInt32());
        var unknown = Json.Encode(new TranscriptTimeoutReader().Read("codex", path));
        Assert.Contains("unverified-transcript-schema", unknown);
        Assert.Contains("null", unknown);
        var missing = Json.Encode(new TranscriptTimeoutReader().Read("claude", "private-nonexistent-path"));
        Assert.Contains("unavailable-io", missing);
        Assert.DoesNotContain("private", missing);
    }

    [Theory]
    [InlineData("cli")]
    [InlineData("exec")]
    public void VerifiedCodexRestrictionUsesCommonHealthFallbackWithoutInventingAZero(string mode)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hooks", "codex-0.164.0-" + mode + "-timeout.jsonl");
        using var report = JsonDocument.Parse(Json.Encode(new TranscriptTimeoutReader().Read("codex", path)));
        Assert.Equal("timeout-records-unavailable-in-verified-cli-mode", report.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, report.RootElement.GetProperty("timeouts").ValueKind);
        Assert.Equal(mode, report.RootElement.GetProperty("verifiedMode").GetString());
        Assert.Contains("capture health", report.RootElement.GetProperty("fallback").GetString());
    }
}
