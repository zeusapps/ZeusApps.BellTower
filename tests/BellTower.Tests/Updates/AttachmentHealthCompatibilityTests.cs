using BellTower.Core;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Tests.Testing;
using BellTower.Updates;
using Xunit;

namespace BellTower.Tests.Updates;

public sealed class AttachmentHealthCompatibilityTests
{
    [Fact]
    public void FailedCommitDoesNotDeclareHealthAndMalformedOrFutureRequirementsFailClosed()
    {
        using var workspace = new TestWorkspace();
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Permission));
        Directory.CreateDirectory(workspace.Paths.State);
        var failure = Record.Exception(() => new StateStore(workspace.Paths).Drain());
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Assert.False(File.Exists(workspace.Paths.State));
        Directory.Delete(workspace.Paths.State);
        AttachmentHealthCompatibility.Ensure(workspace.Paths.Root, 0);
        AtomicFile.Write(workspace.Paths.State, "{\"schemaVersion\":1,\"attachmentHealthVersion\":2}");
        Assert.Throws<InvalidDataException>(() => AttachmentHealthCompatibility.Ensure(workspace.Paths.Root, 1));
        AtomicFile.Write(workspace.Paths.State, "{\"schemaVersion\":1,\"attachmentHealthVersion\":\"1\"}");
        Assert.Throws<InvalidDataException>(() => AttachmentHealthCompatibility.Ensure(workspace.Paths.Root, 1));
        AtomicFile.Write(workspace.Paths.State, "invalid-json");
        Assert.Throws<System.Text.Json.JsonException>(() => AttachmentHealthCompatibility.Ensure(workspace.Paths.Root, 0));
        AttachmentHealthCompatibility.Ensure(workspace.Paths.Root, 1);
    }

    [Fact]
    public void CommittedHealthRequirementRejectsLegacySelectionWithoutChangingSessionsOrInstallation()
    {
        using var workspace = new TestWorkspace();
        var installation = DistributionTests.CreateInstallation(Path.Combine(workspace.Root, "installed"), "0.1.7");
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Permission));
        new StateStore(workspace.Paths).Drain();
        var beforeState = File.ReadAllBytes(workspace.Paths.State);
        var beforeSelection = File.ReadAllBytes(installation.SelectionPath);
        var offer = new ReleaseOffer(new ReleaseContract("0.1.9"), new string('a', 64));
        var error = Assert.Throws<InvalidDataException>(() => UpdateTransaction.Stage(installation, workspace.Paths, "not-read.zip", offer));
        Assert.Contains("health-v1", error.Message);
        Assert.Equal(beforeSelection, File.ReadAllBytes(installation.SelectionPath));
        Assert.Equal(beforeState, File.ReadAllBytes(workspace.Paths.State));
        Assert.False(Directory.Exists(Path.Combine(installation.Root, "updates")));
        Assert.Throws<InvalidDataException>(() => CaptureCompatibility.EnsureTrayInvocation(["tray", "--data-dir", workspace.Paths.Root], 1, 0));
        CaptureCompatibility.EnsureTrayInvocation(["tray", "--data-dir", workspace.Paths.Root], 1, 1);
        CaptureCompatibility.EnsureTrayInvocation(["hook", "--data-dir", workspace.Paths.Root], 0, 0);
    }

    [Fact]
    public void LegacyStateMigratesAndCapableConsumerPreservesUnknownDataAcrossReplay()
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow.AddHours(-1);
        new AttachmentStore(workspace.Paths).Save(new("attachment-1", "claude", null, new("future-host"), workspace.Root, at));
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Permission, at));
        var state = new StateStore(workspace.Paths).Drain();
        var root = System.Text.Json.Nodes.JsonNode.Parse(Json.Encode(state))!;
        root["futureRoot"] = 123;
        root.AsObject().Remove("attachmentHealthVersion");
        root["sessions"]!["claude:session-1"]!["futureSession"] = true;
        root["sessions"]!["claude:session-1"]!.AsObject().Remove("attachmentHealth");
        root["sessions"]!["claude:session-1"]!.AsObject().Remove("attachmentUnverifiedSince");
        AtomicFile.Write(workspace.Paths.State, root.ToJsonString());
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.ToolStart) with { At = at.AddSeconds(1) });
        var restored = new StateStore(workspace.Paths).Drain();
        Assert.Equal("unavailable", restored.Sessions["claude:session-1"].AttachmentHealth);
        Assert.Contains("futureRoot", Json.Encode(restored));
        Assert.Contains("futureSession", Json.Encode(restored));
        AttachmentHealthCompatibility.Ensure(workspace.Paths.Root, 1);
        Assert.Throws<InvalidDataException>(() => AttachmentHealthCompatibility.Ensure(workspace.Paths.Root, 0));
        new ReleaseContract("0.1.9", AttachmentHealth: 1).Validate();
        Assert.Throws<InvalidDataException>(() => new ReleaseContract("0.1.9", AttachmentHealth: 2).Validate());
    }
}
