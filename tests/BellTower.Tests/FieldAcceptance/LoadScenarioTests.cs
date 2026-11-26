using System.Text.Json.Nodes;
using BellTower.FieldAcceptance.Harness;
using BellTower.FieldAcceptance.Isolation;
using BellTower.Tests.Testing;
using BellTower.Tools;
using Xunit;

namespace BellTower.Tests.FieldAcceptance;

/// <summary>The harness pieces behind the five-conversation and packaged hook benchmark scenarios (tasks 2.2 and 2.3).</summary>
public sealed class LoadScenarioTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "native");
    private static readonly InstanceLayout Layout = new("bench", @"D:\repo\artifacts\field-acceptance\runs\1\sandbox\bench");

    [Fact]
    public void StatisticsUseTheNearestRankRuleOfTheIngressBench()
    {
        var samples = Enumerable.Range(1, 100).Select(number => new TimingSample(number, number, true)).ToList();
        var summary = HookTiming.Summarize(samples, "x", 8, 100);
        Assert.Equal(50, summary.MedianMilliseconds);
        Assert.Equal(95, summary.P95Milliseconds);
        Assert.Equal(99, summary.P99Milliseconds);
        Assert.Equal(100, summary.MaximumMilliseconds);
        Assert.Equal(1, summary.FirstSampleMilliseconds);
        Assert.Equal(0, summary.FailedCalls);
    }

    [Fact]
    public void FailedAndSlowSamplesAreCounted()
    {
        var summary = HookTiming.Summarize([new(0, 10, true), new(1, 3500, false), new(2, 20, true)], "x", 8, 2);
        Assert.Equal(1, summary.FailedCalls);
        Assert.Equal(1, summary.OverThreeSeconds);
    }

    [Fact]
    public void BatchesCoverEverySampleOnceAndLegsAlternate()
    {
        var batches = HookTiming.Batches(120, 8);
        Assert.Equal(15, batches.Count);
        Assert.Equal(Enumerable.Range(1, 120), batches.SelectMany(batch => batch));
        Assert.All(batches, batch => Assert.Equal(8, batch.Select(number => number % 8).Distinct().Count()));
        Assert.True(HookTiming.BaselineFirst(0, 0));
        Assert.False(HookTiming.BaselineFirst(1, 0));
        Assert.False(HookTiming.BaselineFirst(0, 1));
    }

    [Fact]
    public void CaptureNamesParseToTheirIdAndIngressTicks()
    {
        var id = Guid.NewGuid().ToString("N");
        var parsed = CaptureObserver.ParseName(@"C:\x\captures-v1\" + 123456789L.ToString("D19") + "-" + id + ".json");
        Assert.Equal((id, 123456789L), parsed);
        Assert.Null(CaptureObserver.ParseName(@"C:\x\" + 1L.ToString("D19") + "-" + id + ".json.abc.tmp"));
        Assert.Null(CaptureObserver.ParseName(@"C:\x\other.json"));
    }

    [Fact]
    public void TheObserverSeesAFileAndItsNameOnlyOnce()
    {
        var directory = Path.Combine(Path.GetTempPath(), "observer-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var observer = new CaptureObserver(directory);
            var id = Guid.NewGuid().ToString("N");
            File.WriteAllText(Path.Combine(directory, DateTime.UtcNow.Ticks.ToString("D19") + "-" + id + ".json"), "{}");
            Assert.True(SpinWait.SpinUntil(() => observer.Captures.Count == 1, 5000));
            Thread.Sleep(100);
            var capture = Assert.Single(observer.Captures);
            Assert.Equal(id, capture.Id);
            Assert.Null(capture.SessionId);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ResumeChangesOnlyTheSourceOfTheNativeFieldSet()
    {
        var model = ProviderModel.Claude;
        var start = JsonNode.Parse(model.Payload("SessionStart", "s", @"D:\w", Layout, Fixtures))!.AsObject();
        var resume = JsonNode.Parse(model.Payload("SessionStart", "s", @"D:\w", Layout, Fixtures, "resume"))!.AsObject();
        Assert.Equal("startup", start["source"]!.GetValue<string>());
        Assert.Equal("resume", resume["source"]!.GetValue<string>());
        Assert.Equal(start.Select(pair => pair.Key), resume.Select(pair => pair.Key));
        // A prompt event has no start source: nothing is added.
        Assert.Null(JsonNode.Parse(model.Payload("UserPromptSubmit", "s", @"D:\w", Layout, Fixtures, "resume"))!["source"]);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void TheShellToolPayloadIsASuccessfulShellPostToolUse(string provider)
    {
        var payload = JsonNode.Parse(ProviderModel.For(provider).ShellToolPayload("s", @"D:\w", Layout, "t1", "openspec status --change x --json"))!;
        Assert.Equal("PostToolUse", payload["hook_event_name"]!.GetValue<string>());
        Assert.Equal("Bash", payload["tool_name"]!.GetValue<string>());
        Assert.Equal("openspec status --change x --json", payload["tool_input"]!["command"]!.GetValue<string>());
        Assert.Equal(ProviderModel.TranscriptPath(Layout, "s"), payload["transcript_path"]!.GetValue<string>());
    }

    [Fact]
    public void ModeledTranscriptsMatchTheTestFixtureBuilders()
    {
        var root = @"D:\w\wt";
        var at = new DateTimeOffset(2026, 10, 23, 12, 0, 5, TimeSpan.Zero);
        var stamp = TranscriptModel.Timestamp(at);
        Assert.Equal(IdentityFixtures.Timestamp(5), stamp);
        Assert.Equal(IdentityFixtures.Claude.Write("s", "t", root, "c", 5), TranscriptModel.ClaudeWriteUse("s", "t", root, "c", stamp));
        // The fixture builder writes an explicit null is_error on success; the model omits it. Both are the same record without nulls.
        Assert.Equal(WithoutNulls(IdentityFixtures.Claude.Result("s", "t", 5)), WithoutNulls(TranscriptModel.ClaudeResult("s", "t", stamp)));
        Assert.Equal(IdentityFixtures.Claude.Title("s", "A title"), TranscriptModel.ClaudeCustomTitle("s", "A title"));
        Assert.Equal(IdentityFixtures.Codex.Patch("c1", root, "c", 5), TranscriptModel.CodexPatchUse("c1", root, "c", stamp));
        Assert.Equal(IdentityFixtures.Codex.PatchResult("c1", 5), TranscriptModel.CodexPatchResult("c1", stamp));
    }

    private static string WithoutNulls(string json)
    {
        static JsonNode? Clean(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                foreach (var name in obj.Where(pair => pair.Value is null).Select(pair => pair.Key).ToList())
                {
                    obj.Remove(name);
                }
                foreach (var pair in obj.ToList())
                {
                    Clean(pair.Value);
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    Clean(item);
                }
            }
            return node;
        }
        return Clean(JsonNode.Parse(json))!.ToJsonString();
    }

    [Fact]
    public void TheCommandLogAndSessionsAcceptConcurrentAdds()
    {
        var log = new LockedList<string>();
        Parallel.For(0, 2000, number => log.Add("c" + number));
        Assert.Equal(2000, log.Count);
        Assert.Equal(2000, log.Distinct().Count());
    }

    [Fact]
    public void MachineLoadReportsABoundedShareAndNoProcessNames()
    {
        var sample = MachineLoad.Sample(TimeSpan.FromMilliseconds(200));
        Assert.InRange(sample.CpuBusyPercent, 0, 100);
        Assert.True(sample.Processes > 0);
        Assert.DoesNotContain(".exe", sample.Describe());
    }
}
