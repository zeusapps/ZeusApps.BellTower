using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BellTower.Core;
using BellTower.Diagnostics;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Providers;

// A cached ingress image provisioned before the human-wait fields keeps delivering captures in the legacy shape after the
// tray updates. The consumer must still confirm a Claude permission_prompt and doctor must ask for a setup refresh.
// Fixtures only: current captures with the human-wait fields stripped, as an older image would have written them.
public sealed class LegacyIngressTests
{
    private static readonly string[] HumanWaitFields = ["humanPrompt", "questionTool", "providerDelay", "permissionMode", "adapterRevision", "runtimeApproval"];

    private static CaptureEnvelope Capture(string json, DateTimeOffset at, bool legacy)
    {
        using var document = JsonDocument.Parse(json);
        var envelope = CaptureFactory.Create("claude", document.RootElement,
            new("fixture-attachment", "claude", null, new(), "D:/example", at), at)!;
        if (!legacy)
        {
            return envelope;
        }
        var node = JsonNode.Parse(CaptureProtocol.Encode(envelope))!;
        var observation = node["observation"]!.AsObject();
        foreach (var property in observation.Select(pair => pair.Key).Where(key => HumanWaitFields.Contains(key, StringComparer.OrdinalIgnoreCase)).ToList())
        {
            observation.Remove(property);
        }
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString()));
        return CaptureProtocol.Decode(stream);
    }

    private const string Request = """{"session_id":"fixture","cwd":"D:/example","hook_event_name":"PermissionRequest","tool_name":"Bash","tool_input":{"command":"example"}}""";
    private const string Prompt = """{"session_id":"fixture","cwd":"D:/example","hook_event_name":"Notification","notification_type":"permission_prompt"}""";

    private static JsonElement Coverage(TestWorkspace workspace, out JsonDocument document)
    {
        document = JsonDocument.Parse(Json.Encode(new DiagnosticsReporter(workspace.Paths).Report()));
        var root = document.RootElement;
        return Get(root, "confirmationCoverage");
    }

    private static JsonElement Get(JsonElement element, string name) =>
        element.GetProperty(element.EnumerateObject().Single(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)).Name);

    [Fact]
    public void ALegacyShapedCaptureHasNoAdapterRevisionAndTheCurrentAdapterStampsOne()
    {
        var at = DateTimeOffset.UtcNow;
        var legacy = Capture(Prompt, at, legacy: true).Observation;
        Assert.Null(legacy.AdapterRevision);
        Assert.False(legacy.HumanPrompt);
        Assert.Null(legacy.ProviderDelay);
        Assert.Equal(EventKind.Permission, legacy.Kind);
        var current = Capture(Prompt, at, legacy: false).Observation;
        Assert.Equal(Observation.HumanWaitRevision, current.AdapterRevision);
        Assert.True(current.HumanPrompt);
    }

    [Fact]
    public void ALegacyPermissionPromptStillConfirmsAProvisionalRequestAndDoctorAsksForSetup()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.UtcNow;
        CaptureSpool.Write(workspace.Paths.Root, Capture(Request, now.AddSeconds(-10), legacy: true));
        CaptureSpool.Write(workspace.Paths.Root, Capture(Prompt, now, legacy: true));
        var session = new StateStore(workspace.Paths).Drain().Sessions["claude:fixture"];
        var wait = Assert.Single(session.Waits);
        Assert.Equal(HumanConfirmation.Confirmed, wait.Confirmation);
        Assert.Equal(AttentionReason.Permission, session.Reason);
        var coverage = Coverage(workspace, out var document);
        using (document)
        {
            var claude = Get(coverage, "claude");
            Assert.Contains("rerun setup", Get(claude, "status").GetString());
            Assert.Contains("rerun setup", Get(claude, "ingressImage").GetString());
        }
    }

    [Fact]
    public void ALegacyToolBearingPermissionRequestIsNeverTurnedIntoAConfirmation()
    {
        using var workspace = new TestWorkspace();
        CaptureSpool.Write(workspace.Paths.Root, Capture(Request, DateTimeOffset.UtcNow.AddMinutes(-2), legacy: true));
        var session = new StateStore(workspace.Paths).Drain().Sessions["claude:fixture"];
        var wait = Assert.Single(session.Waits);
        Assert.Equal(HumanConfirmation.Provisional, wait.Confirmation);
        Assert.Null(session.Reason);
        Assert.False(session.RequiresAttention(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ALegacyPromptAfterAResolutionIsIgnoredLikeACurrentOne()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.UtcNow;
        CaptureSpool.Write(workspace.Paths.Root, Capture(Request, now.AddSeconds(-10), legacy: true));
        CaptureSpool.Write(workspace.Paths.Root, Capture(Request.Replace("PermissionRequest", "PostToolUse"), now.AddSeconds(-2), legacy: true));
        CaptureSpool.Write(workspace.Paths.Root, Capture(Prompt, now, legacy: true));
        var session = new StateStore(workspace.Paths).Drain().Sessions["claude:fixture"];
        Assert.Empty(session.Waits);
        Assert.Null(session.Reason);
    }

    [Fact]
    public void DoctorReportsACurrentIngressImageOnceCurrentCapturesArrive()
    {
        using var workspace = new TestWorkspace();
        var now = DateTimeOffset.UtcNow;
        CaptureSpool.Write(workspace.Paths.Root, Capture(Prompt, now.AddSeconds(-5), legacy: true));
        new StateStore(workspace.Paths).Drain();
        CaptureSpool.Write(workspace.Paths.Root, Capture(Request, now.AddSeconds(-4), legacy: false));
        new StateStore(workspace.Paths).Drain();
        var coverage = Coverage(workspace, out var document);
        using (document)
        {
            var claude = Get(coverage, "claude");
            Assert.Equal("current", Get(claude, "ingressImage").GetString());
            Assert.DoesNotContain("rerun setup", Get(claude, "status").GetString());
        }
    }
}
