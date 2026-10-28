using System.Text.Json;
using BellTower.Core;
using BellTower.Diagnostics;
using BellTower.IO;
using BellTower.Tests.Testing;
using BellTower.Tray;
using Xunit;

namespace BellTower.Tests.Diagnostics;

public sealed class MicrophoneDiagnosticsTests
{
    private sealed class Probe(MicrophoneActivitySnapshot snapshot) : IMicrophoneActivitySource
    {
        public int Calls { get; private set; }

        public MicrophoneActivitySnapshot GetSnapshot()
        {
            Calls++;
            return snapshot;
        }
    }

    [Fact]
    public void DoctorUsesOneInjectedMicrophoneSnapshotAndReportsPolicyScope()
    {
        using var workspace = new TestWorkspace();
        new ConfigStore(workspace.Paths).Update(config => config.MicrophoneAwareVoice = true);
        var observedAt = DateTimeOffset.UtcNow;
        var probe = new Probe(new(MicrophoneUsageState.Inactive, observedAt, "fixture: no shared capture session"));

        using var report = JsonDocument.Parse(Json.Encode(new DiagnosticsReporter(workspace.Paths, probe).Report()));
        var microphone = report.RootElement.GetProperty("microphoneAwareVoice");

        Assert.True(microphone.GetProperty("enabled").GetBoolean());
        Assert.Equal("inactive", microphone.GetProperty("state").GetString());
        Assert.Equal(observedAt, microphone.GetProperty("observedAt").GetDateTimeOffset());
        Assert.Equal("fixture: no shared capture session", microphone.GetProperty("reason").GetString());
        Assert.Contains("shared-mode", microphone.GetProperty("scope").GetString());
        Assert.Equal(1, probe.Calls);
    }
}
