using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BellTower.Core;
using BellTower.Diagnostics;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Providers;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Providers;

public sealed class CaptureTests
{
    private static CaptureEnvelope Envelope(string provider = "claude", string hook = "Stop", string? operation = null,
        DateTimeOffset? at = null, string? agent = null, string? agentType = null)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            session_id = "fixture",
            cwd = "D:/unicode-Δ",
            hook_event_name = hook,
            tool_name = "Bash",
            tool_use_id = operation,
            tool_input = new { command = "private-command" },
            agent_id = agent,
            agent_type = agentType,
            last_assistant_message = "private-message",
            unknown_field = "private-unknown"
        }));
        return CaptureFactory.Create(provider, document.RootElement,
            new("fixture-attachment", provider, null, new(), "D:/unicode-Δ", DateTimeOffset.UtcNow), at ?? DateTimeOffset.UtcNow)!;
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void SourceGeneratedProtocolRoundTripsAndPreservesUnknownExtensions(string provider)
    {
        var original = Envelope(provider);
        var node = JsonNode.Parse(CaptureProtocol.Encode(original))!;
        node["future"] = new JsonObject { ["count"] = 42 };
        node["observation"]!["futureObservation"] = true;
        node["attachment"]!["futureAttachment"] = true;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString()));
        var envelope = CaptureProtocol.Decode(stream);
        var encoded = Encoding.UTF8.GetString(CaptureProtocol.Encode(envelope));
        Assert.Contains("futureObservation", encoded);
        Assert.Contains("futureAttachment", encoded);
        Assert.Contains("future", encoded);
        Assert.Equal(original.Observation.OperationFingerprint, envelope.Observation.OperationFingerprint);
        Assert.Equal("D:/unicode-Δ", envelope.Observation.Directory);
        Assert.DoesNotContain("private-", encoded);
        node["protocol"] = 999;
        using var unknown = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString()));
        Assert.Throws<InvalidDataException>(() => CaptureProtocol.Decode(unknown));
        node.AsObject().Remove("protocol");
        using var missing = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString()));
        Assert.Throws<InvalidDataException>(() => CaptureProtocol.Decode(missing));
    }

    [Fact]
    public void InputBudgetCountsBytesStripsBomAndRejectsMalformedUtf8()
    {
        using var stream = new MemoryStream([0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("{\"value\":\"Δ\"}")]);
        using var document = CaptureProtocol.ReadInput(stream);
        Assert.Equal("Δ", document.RootElement.GetProperty("value").GetString());
        using var oversized = new MemoryStream(new byte[CaptureProtocol.MaximumInputBytes + 10]);
        Assert.Throws<InvalidDataException>(() => CaptureProtocol.ReadInput(oversized));
        Assert.Equal(CaptureProtocol.MaximumInputBytes + 1, oversized.Position);
        using var invalid = new MemoryStream([0xff]);
        Assert.Throws<DecoderFallbackException>(() => CaptureProtocol.ReadInput(invalid));
    }

    [Fact]
    public void ParallelCapturesAreAtomicUniqueAndDoNotUseSharedStateLocks()
    {
        using var workspace = new TestWorkspace();
        using var heldState = new DataLock(workspace.Paths, "state");
        using var heldIdentity = new DataLock(workspace.Paths, "identity");
        var paths = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 64, i => paths.Add(CaptureSpool.Write(workspace.Paths.Root, Envelope(i % 2 == 0 ? "claude" : "codex"))));
        Assert.Equal(64, paths.Distinct().Count());
        Assert.Empty(Directory.GetFiles(Path.Combine(workspace.Paths.Root, CaptureProtocol.InboxName), "*.tmp"));
        foreach (var path in paths)
        {
            using var stream = File.OpenRead(path);
            CaptureProtocol.Decode(stream);
        }
    }

    [Fact]
    public void FailedAtomicRenameCleansTemporaryFileAndReportsOnlyCategory()
    {
        using var workspace = new TestWorkspace();
        var envelope = Envelope();
        var path = CaptureSpool.Write(workspace.Paths.Root, envelope);
        File.Delete(path);
        Directory.CreateDirectory(path);
        Assert.ThrowsAny<IOException>(() => CaptureSpool.Write(workspace.Paths.Root, envelope));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        CaptureSpool.RecordRejection(workspace.Paths.Root, new IOException("private-exception-message"));
        var report = Json.Encode(new CaptureHealthReporter(workspace.Paths).Report());
        Assert.Contains("io", report);
        Assert.DoesNotContain("private", report);
        Assert.DoesNotContain("private", string.Join("", Directory.GetFiles(Path.Combine(workspace.Paths.Root, "capture-health")).Select(File.ReadAllText)));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ALegacyCaptureCarryingARoleIsConsumedWithoutAnyRoleInNameOrSpeech(string provider)
    {
        using var workspace = new TestWorkspace();
        var node = JsonNode.Parse(CaptureProtocol.Encode(Envelope(provider, hook: "Stop")))!;
        // An envelope spooled by an older ingress (BELLTOWER_ROLE) still decodes; the role is ignored.
        node["role"] = "PM";
        node["name"] = null;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(node.ToJsonString()));
        var legacy = CaptureProtocol.Decode(stream);
        Assert.Equal("PM", legacy.Role);
        CaptureSpool.Write(workspace.Paths.Root, legacy);
        var state = new StateStore(workspace.Paths).Drain();
        var identity = new IdentityStore(workspace.Paths).Get(provider + ":fixture", "D:/unicode-Δ");
        Assert.Null(identity.Role);
        Assert.DoesNotContain("PM", identity.Name, StringComparison.Ordinal);
        Assert.DoesNotContain("PM", AlertPolicy.Phrase(state.Sessions[provider + ":fixture"], identity), StringComparison.Ordinal);
        Assert.DoesNotContain("\"role\":\"PM\"", Encoding.UTF8.GetString(CaptureProtocol.Encode(Envelope(provider))), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void TrayOffConsumptionCrashReplayAndLegacyInboxCoexist(string provider)
    {
        using var workspace = new TestWorkspace();
        var envelope = Envelope(provider);
        var path = CaptureSpool.Write(workspace.Paths.Root, envelope);
        var retained = File.ReadAllBytes(path);
        new EventStore(workspace.Paths).Publish(TestWorkspace.Event(EventKind.Stop) with { SessionId = "legacy" });
        Assert.False(File.Exists(workspace.Paths.State));
        var store = new StateStore(workspace.Paths);
        var first = store.Drain();
        var episode = first.Sessions[provider + ":fixture"].Episode;
        Assert.Equal(2, first.Sessions.Count);
        Assert.False(File.Exists(path));
        File.WriteAllBytes(path, retained);
        var replay = store.Drain();
        Assert.Equal(episode, replay.Sessions[provider + ":fixture"].Episode);
        Assert.Single(replay.Sessions[provider + ":fixture"].Waits);
        Assert.False(File.Exists(path));
        Assert.NotNull(replay.Sessions[provider + ":fixture"].CausalEvents[0].ProcessedAt);
    }

    [Fact]
    public void ConsumerBatchIsBoundedAndCorruptCapturesCannotStarveFollowingDrain()
    {
        using var workspace = new TestWorkspace();
        for (var i = 0; i < CaptureConsumer.BatchSize + 1; i++)
        {
            CaptureSpool.Write(workspace.Paths.Root, Envelope());
        }
        var inbox = Path.Combine(workspace.Paths.Root, CaptureProtocol.InboxName);
        var state = new AppState();
        Assert.Equal(CaptureConsumer.BatchSize, new CaptureConsumer(workspace.Paths).Prepare(state).Count);
        File.WriteAllText(Path.Combine(inbox, "bad.json"), "private-corrupt-input");
        var store = new StateStore(workspace.Paths);
        store.Drain();
        store.Drain();
        Assert.Empty(Directory.GetFiles(inbox, "*.json"));
        Assert.DoesNotContain("private-corrupt-input", Json.Encode(new CaptureHealthReporter(workspace.Paths).Report()));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void CausalReplayProducesSameEpisodeRegardlessOfDeliveryOrder(string provider)
    {
        var at = DateTimeOffset.UtcNow.AddMinutes(-2);
        var events = new[] { "SessionStart", "UserPromptSubmit", "PreToolUse", "PermissionRequest", "PostToolUse", "Stop" }
            .Select((hook, index) => Envelope(provider, hook, "operation", at.AddSeconds(index)).Observation).ToArray();
        var random = new Random(42);
        string? expected = null;
        for (var i = 0; i < 40; i++)
        {
            var state = new AppState();
            foreach (var observation in events.OrderBy(_ => random.Next()))
            {
                SessionReducer.Apply(state, observation);
                SessionReducer.Apply(state, observation);
            }
            var session = state.Sessions[provider + ":fixture"];
            Assert.Equal(AttentionReason.Done, session.Reason);
            Assert.Empty(session.Operations);
            Assert.Contains("operation", session.CompletedOperations);
            expected ??= session.Episode;
            Assert.Equal(expected, session.Episode);
            Assert.Equal(at.AddSeconds(5), session.Since);
        }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void DelayedStartsAndRegistrationsDoNotReopenCompletedState(string provider)
    {
        var at = DateTimeOffset.UtcNow.AddMinutes(-2);
        var state = new AppState();
        SessionReducer.Apply(state, Envelope(provider, "PostToolUse", "operation", at.AddSeconds(2)).Observation);
        SessionReducer.Apply(state, Envelope(provider, "PreToolUse", "operation", at).Observation);
        var session = state.Sessions[provider + ":fixture"];
        Assert.Empty(session.Operations);
        Assert.Contains("operation", session.CompletedOperations);
        SessionReducer.Apply(state, Envelope(provider, "SessionEnd", at: at.AddSeconds(3)).Observation);
        SessionReducer.Apply(state, Envelope(provider, "SessionStart", at: at.AddSeconds(-1)).Observation);
        Assert.True(state.Sessions[provider + ":fixture"].Ended);
        SessionReducer.Apply(state, Envelope(provider, "SessionStart", at: at.AddSeconds(4)).Observation);
        Assert.True(state.Sessions[provider + ":fixture"].Ended);
        SessionReducer.Apply(state, Envelope(provider, "SessionStart", at: at.AddSeconds(5)).Observation with { RegistrationSource = "resume" });
        Assert.False(state.Sessions[provider + ":fixture"].Ended);
    }

    [Fact]
    public void CustomMainAgentRemainsMainAndSubagentEvidenceIsExcluded()
    {
        Assert.True(Envelope(agentType: "pm").Observation.IsMainThread);
        Assert.False(Envelope(agent: "subagent", agentType: "pm").Observation.IsMainThread);
    }

    [Fact]
    public void FailedStateCommitLeavesCapturesForRetryAndKeepsEffectsIdempotent()
    {
        using var workspace = new TestWorkspace();
        var path = CaptureSpool.Write(workspace.Paths.Root, Envelope());
        Directory.CreateDirectory(workspace.Paths.State);
        var failure = Record.Exception(() => new StateStore(workspace.Paths).Drain());
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Assert.True(File.Exists(path));
        Directory.Delete(workspace.Paths.State);
        var state = new StateStore(workspace.Paths).Drain();
        Assert.Single(state.Sessions);
        Assert.Single(state.Sessions.Values.Single().Waits);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void MixedProvidersAndLegacyEventsShareBoundedPersistentCausalReplay()
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow.AddMinutes(-3);
        foreach (var provider in new[] { "claude", "codex" })
        {
            var envelope = Envelope(provider, "UserPromptSubmit", at: at);
            var state = new AppState();
            for (var i = 0; i < CausalSessionReducer.Window + 5; i++)
            {
                SessionReducer.Apply(state, envelope.Observation with { Id = Guid.NewGuid().ToString("N"), At = at.AddMilliseconds(i) });
            }
            var session = state.Sessions[provider + ":fixture"];
            Assert.Equal(CausalSessionReducer.Window, session.CausalEvents.Count);
            Assert.NotNull(session.CausalCheckpoint);
            var restored = Json.Decode<AppState>(Json.Encode(state));
            SessionReducer.Apply(restored, Envelope(provider, "Stop", at: at.AddSeconds(1)).Observation with { IngressAt = null });
            Assert.Equal(AttentionReason.Done, restored.Sessions[provider + ":fixture"].Reason);
            CaptureSpool.Write(workspace.Paths.Root, Envelope(provider, "Stop", at: at));
        }
        var mixed = new StateStore(workspace.Paths).Drain();
        Assert.Equal(2, mixed.Sessions.Count);
        Assert.Equal(2, mixed.Sessions.Values.Count(s => s.Reason == AttentionReason.Done));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void SuccessfulPostCanSelectWithoutPreAndDelayedResultCannotReplaceNewerSelection(string provider)
    {
        using var workspace = new TestWorkspace();
        var at = DateTimeOffset.UtcNow.AddMinutes(-2);
        CaptureEnvelope Selection(string change, DateTimeOffset when)
        {
            var directory = Path.Combine(workspace.Root, "openspec", "changes", change);
            Directory.CreateDirectory(directory);
            using var input = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                session_id = "fixture",
                cwd = workspace.Root,
                hook_event_name = "PostToolUse",
                tool_name = provider == "codex" ? "apply_patch" : "Write",
                tool_use_id = change,
                tool_input = provider == "codex"
                    ? (object)new { command = "*** Begin Patch\n*** Add File: " + Path.Combine(directory, "proposal.md").Replace('\\', '/') + "\n+x\n*** End Patch" }
                    : new { file_path = Path.Combine(directory, "proposal.md") },
                tool_response = provider == "codex" ? (object)"Success." : new { type = "create" }
            }));
            return CaptureFactory.Create(provider, input.RootElement,
                new("attachment", provider, null, new(), workspace.Root, at), when)!;
        }
        CaptureSpool.Write(workspace.Paths.Root, Selection("new", at.AddSeconds(1)));
        var store = new StateStore(workspace.Paths);
        store.Drain();
        CaptureSpool.Write(workspace.Paths.Root, Selection("old", at));
        store.Drain();
        Assert.Equal("new", new IdentityStore(workspace.Paths).Get(provider + ":fixture", workspace.Root).ActiveWork);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void SelectionReplayPreservesNewerIntentAndManualIdentityInEitherProvider(string provider)
    {
        var random = new Random(7);
        var at = DateTimeOffset.UtcNow.AddMinutes(-2);
        for (var trial = 0; trial < 16; trial++)
        {
            using var workspace = new TestWorkspace();
            var key = provider + ":fixture";
            var original = new IdentityStore(workspace.Paths).Get(key, workspace.Root, initialName: "Manual");
            Directory.CreateDirectory(Path.Combine(workspace.Root, "openspec", "changes", "old"));
            Directory.CreateDirectory(Path.Combine(workspace.Root, "openspec", "changes", "new"));
            var receipts = new[] { ("old", true), ("new", true), ("new", false), ("old", false) }
                .Select((item, index) => Envelope(provider, item.Item2 ? "PreToolUse" : "PostToolUse", item.Item1, at.AddSeconds(index)) with
                {
                    Selection = new(item.Item1, null, ContentHash.Compute(item.Item1), "edit") { WorkRoot = workspace.Root },
                    SelectionPre = item.Item2,
                    SelectionRoot = item.Item2 ? null : workspace.Root
                }).ToArray();
            var observer = new OpenSpecSelectionObserver(workspace.Paths);
            foreach (var receipt in receipts.OrderBy(_ => random.Next()))
            {
                observer.Observe(receipt);
                observer.Observe(receipt);
            }
            var result = new IdentityStore(workspace.Paths).Get(key, workspace.Root);
            Assert.Equal("new", result.ActiveWork);
            Assert.Equal("Manual", result.Name);
            Assert.Equal(original.Palette, result.Palette);
            Assert.False(File.Exists(workspace.Paths.State));
        }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void DeferredCaptureUsesHistoricalLaunchPaletteAfterTheShellHasExited(string provider)
    {
        using var workspace = new TestWorkspace();
        var historicalShell = new ProcessIdentity(int.MaxValue, DateTimeOffset.UtcNow.AddMinutes(-1).Ticks);
        var launch = new LaunchPresentationStore(workspace.Paths).Prepare("fixture-attachment", historicalShell);
        var envelope = Envelope(provider);
        envelope = envelope with { Attachment = envelope.Attachment with { Host = new("unknown", historicalShell) } };
        CaptureSpool.Write(workspace.Paths.Root, envelope);
        new StateStore(workspace.Paths).Drain();
        Assert.Equal(launch.Palette, new IdentityStore(workspace.Paths).Get(provider + ":fixture", workspace.Root).Palette);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task ExecutableCapturesUnicodeAndFailsNeutralWithoutLeakingInput(string provider)
    {
        using var workspace = new TestWorkspace();
        var output = new DirectoryInfo(AppContext.BaseDirectory);
        var executable = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(BuiltApplication.Executable)!, "..", "..", "..", "..",
            "BellTower.Ingress", "bin", output.Parent!.Name, output.Name, "win-x64", "bell-tower-ingress.exe"));
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false)
        };
        foreach (var arg in new[] { "--provider", provider, "--data-dir", workspace.Paths.Root })
        {
            start.ArgumentList.Add(arg);
        }
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("BELLTOWER_", StringComparison.Ordinal)).ToArray())
        {
            start.Environment.Remove(key);
        }
        start.Environment["BELLTOWER_CODEX_TOKEN"] = "private-credential";
        foreach (var input in new[] { "{\"session_id\":\"fixture\",\"cwd\":\"D:/unicode-Δ\",\"hook_event_name\":\"Stop\",\"prompt\":\"private-prompt\"}", "malformed-private-json" })
        {
            using var process = Process.Start(start)!;
            process.StandardInput.Write(input);
            process.StandardInput.Close();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(10000));
            Assert.Equal(0, process.ExitCode);
            Assert.Equal("{}", (await stdout).Trim());
            Assert.Empty(await stderr);
        }
        var capture = Assert.Single(Directory.GetFiles(Path.Combine(workspace.Paths.Root, CaptureProtocol.InboxName), "*.json"));
        Assert.DoesNotContain("private", File.ReadAllText(capture));
        using var stream = File.OpenRead(capture);
        Assert.Equal("D:/unicode-Δ", CaptureProtocol.Decode(stream).Observation.Directory);
        Assert.Contains("json", Json.Encode(new CaptureHealthReporter(workspace.Paths).Report()));
    }
}
