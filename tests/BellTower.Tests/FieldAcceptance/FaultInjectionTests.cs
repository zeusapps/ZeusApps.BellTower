using System.Text.Json.Nodes;
using BellTower.FieldAcceptance.Harness;
using BellTower.FieldAcceptance.Isolation;
using Xunit;

namespace BellTower.Tests.FieldAcceptance;

/// <summary>
/// The swap tests clear the whole process environment for the duration of a call, so they run alone: no other test class runs in parallel with a collection that
/// disables parallelization (ANALYSIS-245).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection
{
    public const string Name = "ProcessEnvironment";
}

/// <summary>The pieces of the fault-injection and rollback scenario that need no installation: the environment swap, the spool injections and the modeled events.</summary>
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class FaultInjectionTests
{
    private static readonly InstanceLayout Layout = new("faults", @"D:\repo\artifacts\field-acceptance\runs\1\sandbox\faults");

    // ---- ProcessEnvironmentSwap (ANALYSIS-171) -------------------------------------------------------------------------------------

    [Fact]
    public void TheSwapReplacesTheEnvironmentAndRestoresItExactlyIncludingAVariableTheCallCreated()
    {
        const string Kept = "FIELD_ACCEPTANCE_TEST_KEPT";
        const string Created = "FIELD_ACCEPTANCE_TEST_CREATED";
        const string Injected = "FIELD_ACCEPTANCE_TEST_INJECTED";
        Environment.SetEnvironmentVariable(Kept, "original");
        Environment.SetEnvironmentVariable(Created, null);
        try
        {
            var seen = ProcessEnvironmentSwap.Run(new Dictionary<string, string> { [Injected] = "sandbox" }, () =>
            {
                Environment.SetEnvironmentVariable(Created, "by-the-call");
                return (Kept: Environment.GetEnvironmentVariable(Kept), Injected: Environment.GetEnvironmentVariable(Injected), Created: Environment.GetEnvironmentVariable(Created));
            });

            Assert.Null(seen.Kept); // the harness user's variables are gone while the call runs
            Assert.Equal("sandbox", seen.Injected);
            Assert.Equal("by-the-call", seen.Created);
            Assert.Equal("original", Environment.GetEnvironmentVariable(Kept));
            Assert.Null(Environment.GetEnvironmentVariable(Created)); // a variable the call created does not survive the restore
            Assert.Null(Environment.GetEnvironmentVariable(Injected));
            Assert.False(ProcessEnvironmentSwap.Active);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Kept, null);
        }
    }

    [Fact]
    public void AnotherThreadIsRefusedWhileASwapRunsButTheSwappingThreadIsNot()
    {
        ProcessEnvironmentSwap.Run(new Dictionary<string, string>(), () =>
        {
            ProcessEnvironmentSwap.ThrowIfForeign(); // the swapping thread itself (the update transaction starts the tray from it)
            Exception? foreign = null;
            var thread = new Thread(() =>
            {
                try
                {
                    ProcessEnvironmentSwap.ThrowIfForeign();
                }
                catch (InvalidOperationException exception)
                {
                    foreign = exception;
                }
            });
            thread.Start();
            thread.Join();
            Assert.NotNull(foreign);
            Assert.Throws<InvalidOperationException>(() => ProcessEnvironmentSwap.Run(new Dictionary<string, string>(), () => 0)); // swaps do not nest
            return 0;
        });
        ProcessEnvironmentSwap.ThrowIfForeign(); // nothing is refused once the swap ended, and a failed call also releases it
        Assert.Throws<ArgumentException>(() => ProcessEnvironmentSwap.Run<int>(new Dictionary<string, string>(), () => throw new ArgumentException("boom")));
        Assert.False(ProcessEnvironmentSwap.Active);
    }

    // ---- SpoolInjection --------------------------------------------------------------------------------------------------------------

    private static string Spool(params string[] ids)
    {
        var inbox = Path.Combine(Path.GetTempPath(), "belltower-spool-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(inbox);
        for (var index = 0; index < ids.Length; index++)
        {
            File.WriteAllText(Path.Combine(inbox, (639000000000000000L + index).ToString("D19") + "-" + ids[index] + ".json"), ids[index]);
        }
        return inbox;
    }

    [Fact]
    public void AReorderChangesTheDrainOrderAndKeepsEveryEnvelopeIdAndBody()
    {
        var inbox = Spool("a", "b", "c", "d", "e");
        try
        {
            var order = SpoolInjection.ShuffledOrder(5);
            Assert.Equal(order.Order().ToList(), Enumerable.Range(0, 5).ToList());
            Assert.NotEqual(Enumerable.Range(0, 5).ToList(), order);

            SpoolInjection.Reorder(inbox, order);
            var drained = SpoolInjection.Pending(inbox).Select(SpoolInjection.EnvelopeId).ToList();

            Assert.Equal(order.Select(index => "abcde"[index].ToString()).ToList(), drained);
            Assert.All(SpoolInjection.Pending(inbox), path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                Assert.Equal(19, name.IndexOf('-')); // the consumer requires <ticks>-<id>; the id suffix is what it checks
                Assert.Equal(SpoolInjection.EnvelopeId(path), File.ReadAllText(path));
            });
        }
        finally
        {
            Directory.Delete(inbox, true);
        }
    }

    [Fact]
    public void ADuplicateIsAnotherFileWithTheSameEnvelopeIdThatDrainsAfterTheOriginals()
    {
        var inbox = Spool("a", "b", "c");
        try
        {
            var copies = SpoolInjection.Duplicate(inbox, [0, 2]);
            var drained = SpoolInjection.Pending(inbox).Select(SpoolInjection.EnvelopeId).ToList();

            Assert.Equal(2, copies.Count);
            Assert.Equal(["a", "b", "c", "a", "c"], drained);
            Assert.Equal(5, SpoolInjection.Pending(inbox).Count);
        }
        finally
        {
            Directory.Delete(inbox, true);
        }
    }

    [Fact]
    public void AReorderThatIsNotAPermutationIsRefusedAndMovesNothing()
    {
        var inbox = Spool("a", "b");
        try
        {
            Assert.Throws<ArgumentException>(() => SpoolInjection.Reorder(inbox, [0, 0]));
            Assert.Throws<ArgumentException>(() => SpoolInjection.Reorder(inbox, [0]));
            Assert.Equal(["a", "b"], SpoolInjection.Pending(inbox).Select(SpoolInjection.EnvelopeId).ToList());
        }
        finally
        {
            Directory.Delete(inbox, true);
        }
    }

    // ---- The modeled approval-flow events and histories -----------------------------------------------------------------------------

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ThePermissionRequestCarriesNoToolUseIdAndThePreAndPostDoWithTheSameCommand(string provider)
    {
        var model = ProviderModel.For(provider);

        var request = JsonNode.Parse(model.ToolPayload("PermissionRequest", "s", @"D:\repo", Layout, "cmd-1", null))!.AsObject();
        var pre = JsonNode.Parse(model.ToolPayload("PreToolUse", "s", @"D:\repo", Layout, "cmd-1", "toolu_1"))!.AsObject();
        var post = JsonNode.Parse(model.ToolPayload("PostToolUse", "s", @"D:\repo", Layout, "cmd-1", "toolu_1"))!.AsObject();

        Assert.Null(request["tool_use_id"]);
        Assert.Equal("toolu_1", pre["tool_use_id"]!.GetValue<string>());
        Assert.Equal("toolu_1", post["tool_use_id"]!.GetValue<string>());
        Assert.All(new[] { request, pre, post }, payload =>
        {
            Assert.Equal("Bash", payload["tool_name"]!.GetValue<string>());
            Assert.Equal("cmd-1", payload["tool_input"]!["command"]!.GetValue<string>());
            Assert.Null(payload["turn_id"]); // never filtered by turn
        });
        Assert.NotNull(post["tool_response"]);
        Assert.Null(pre["tool_response"]);
        Assert.Throws<ArgumentException>(() => model.ToolPayload("PostToolUse", "s", @"D:\repo", Layout, "cmd-1", null));
    }

    [Fact]
    public void TheHistoryRecordsAreTheSharedTranscriptBuildersPerProviderAndOnlyClaudeCarriesATitle()
    {
        var at = DateTimeOffset.Parse("2026-11-26T10:00:00Z");
        var claude = ProviderModel.Claude.HistoryRecords("s", "work", @"D:\wt", "dep-1-login", at, "A title").ToList();
        var codex = ProviderModel.Codex.HistoryRecords("s", "work", @"D:\wt", "dep-1-login", at, "A title").ToList();

        Assert.Equal(TranscriptModel.ClaudeWriteUse("s", "toolu_work", @"D:\wt", "dep-1-login", TranscriptModel.Timestamp(at)), claude[0]);
        Assert.Equal(TranscriptModel.ClaudeCustomTitle("s", "A title"), claude[2]);
        Assert.Equal(TranscriptModel.CodexPatchUse("call_work", @"D:\wt", "dep-1-login", TranscriptModel.Timestamp(at)), codex[0]);
        Assert.Equal(2, codex.Count);
        Assert.Equal(2, ProviderModel.Claude.HistoryRecords("s", "work", @"D:\wt", "dep-1-login", at, null).Count());
    }

    [Theory]
    [InlineData(@"C:\x\claude.exe", true)]
    [InlineData(@"C:\x\codex.exe", true)]
    [InlineData(@"C:\x\unidentified-claude.exe", true)]
    [InlineData(@"C:\x\unidentified-codex.exe", true)]
    [InlineData(@"C:\x\BellTower.FieldAcceptance.exe", false)]
    [InlineData(@"C:\x\unidentified-other.exe", false)]
    public void TheFakeAgentImagesIncludeTheUnidentifiedOnesThatTheProductCannotRecognizeAsTheProvider(string image, bool fake)
    {
        Assert.Equal(fake, FakeAgent.IsFakeImage(image));
        // The product looks for an ancestor named <provider>.exe; the unidentified image never matches it.
        // The product walks the ancestry for an image named exactly <provider>.exe (Hosts/AttachmentCapture.cs); the unidentified image must never match that name.
        foreach (var provider in new[] { "claude", "codex" })
        {
            Assert.False(Path.GetFileName(FakeAgent.UnidentifiedPrefix + provider + ".exe").Equals(provider + ".exe", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void OnlyTheProviderThatMayShareABackendFailsAnUnclassifiedStartupClosed()
    {
        // Mirrors AgentModeClassifier.MayShareBackend: Codex may share a backend, Claude Code has none (the scenario expects repair for one, unavailable for the other).
        Assert.False(ProviderModel.Claude.UnclassifiedStartupFailsClosed);
        Assert.True(ProviderModel.Codex.UnclassifiedStartupFailsClosed);
        Assert.Equal(BellTower.Providers.AgentModeClassifier.MayShareBackend("claude"), ProviderModel.Claude.UnclassifiedStartupFailsClosed);
        Assert.Equal(BellTower.Providers.AgentModeClassifier.MayShareBackend("codex"), ProviderModel.Codex.UnclassifiedStartupFailsClosed);
    }

    [Fact]
    public void TheIngressImageHashIsReadFromAnOwnedCommandInEitherShellForm()
    {
        var hash = new string('a', 64);
        Assert.Equal(hash, IngressImage.HashOf(@"'C:\data\integrations\provider-ingress\1\" + hash.ToUpperInvariant() + @"\bell-tower-ingress.exe' --provider claude"));
        Assert.Equal(hash, IngressImage.HashOf("/c/data/integrations/provider-ingress/1/" + hash + "/bell-tower-ingress.exe --provider codex"));
        Assert.Null(IngressImage.HashOf("echo 1"));
        Assert.Equal("aaaaaaaaaaaa", IngressImage.Short(hash));
    }

    [Fact]
    public void AStateViewReadsWaitsOperationsCompletionsAndTheJournalFromRawJsonAndFingerprintsThem()
    {
        var session = JsonNode.Parse("""
            {"attachmentId":"att","ended":false,"waits":[{"reason":2}],"operations":{"toolu_2":{}},"completedOperations":["toolu_1","fp"],"causalEvents":[{"id":"e1"},{"id":"e2"}]}
            """);
        var view = new StateView(session);

        Assert.True(view.Exists);
        Assert.Equal(1, view.Waits);
        Assert.Equal(["toolu_2"], view.Operations);
        Assert.Contains("toolu_1", view.Completed);
        Assert.Equal(["e1", "e2"], view.Journal);
        Assert.Empty(view.JournalOperationIds);
        Assert.Equal("att", view.AttachmentId);
        Assert.Equal(new StateView(session).Fingerprint, view.Fingerprint);
        Assert.False(new StateView(null).Exists);
        Assert.Equal(0, new StateView(null).Waits);
    }

    [Fact]
    public void TheEnforcementIsWiredIntoInstanceCreationNotOnlyIntoThePrimitive()
    {
        // Removing the guard call at the start of Instance.Create (before any file or process work) fails this test: the nulls are never reached.
        Exception? refused = null;
        ProcessEnvironmentSwap.Run<int>(new Dictionary<string, string>(), () =>
        {
            var thread = new Thread(() =>
            {
                try
                {
                    Instance.Create("x", "x", "x", null!, null!);
                }
                catch (InvalidOperationException exception)
                {
                    refused = exception;
                }
            });
            thread.Start();
            thread.Join();
            return 0;
        });
        Assert.NotNull(refused);
        Assert.Contains("swapped", refused!.Message);
    }

    private static string Repository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ZeusApps.BellTower.slnx")))
            {
                return directory.FullName;
            }
        }
        throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void TheLaunchKindsMatchTheProductLaunchScripts()
    {
        var psm1 = File.ReadAllText(Path.Combine(Repository(), "src", "BellTower", "Integration", "BellTower.psm1"));
        var launcher = File.ReadAllText(Path.Combine(Repository(), "src", "BellTower", "Sessions", "AgentLauncher.cs"));
        // BellTower.psm1:40 exports the attachment id; :52-58 a Codex launch prepends --no-daemon and sets the marker unless --remote; :97-106 the default
        // (non-standalone) launch prepares the owned runtime and replaces the arguments with --remote while the marker and the id stay set.
        Assert.Contains("$env:BELLTOWER_ATTACHMENT = $attachmentId", psm1);
        Assert.Contains("$standalone = $nativeArguments -contains '--no-daemon'", psm1);
        Assert.Contains("$nativeArguments = @('--no-daemon') + $nativeArguments", psm1);
        Assert.Contains("$env:BELLTOWER_DEDICATED = '1'", psm1);
        Assert.Contains("$nativeArguments = @('--remote', $runtime.endpoint", psm1);
        Assert.Contains("Where-Object { $_ -ne '--no-daemon' }", psm1);
        Assert.Contains("start.Environment[\"BELLTOWER_DEDICATED\"] = options.Provider == \"codex\" && options.Arguments.Contains(\"--no-daemon\") ? \"1\" : null;", launcher);

        var codex = ProviderModel.Codex.LaunchKinds.ToDictionary(kind => kind.Name);
        Assert.Equal(["--no-daemon"], codex["managed"].Arguments);
        Assert.True(codex["managed"].ManagedAttachment && codex["managed"].Dedicated);
        // The owned runtime: the backend (hooks run under it) is an app-server started with --listen, carrying the id and the marker, with a runtime record.
        Assert.Equal("app-server", codex["owned-runtime"].Arguments[0]);
        Assert.Contains("--listen", codex["owned-runtime"].Arguments);
        Assert.DoesNotContain("--no-daemon", codex["owned-runtime"].Arguments);
        Assert.True(codex["owned-runtime"].ManagedAttachment && codex["owned-runtime"].Dedicated && codex["owned-runtime"].OwnedRuntime);
        Assert.False(codex["plain"].ManagedAttachment || codex["plain"].Dedicated);
        Assert.False(codex["shared"].ManagedAttachment || codex["shared"].Dedicated);
        Assert.DoesNotContain(ProviderModel.Claude.LaunchKinds, kind => kind.Dedicated);
        Assert.True(ProviderModel.Claude.LaunchKinds.Single(kind => kind.Name == "managed").ManagedAttachment);
    }

    [Fact]
    public void ACaptureTheTrayConsumedBeforeItWasReadIsResolvedFromTheTraysJournalAndAnUnappliedOneStaysUnresolved()
    {
        var inbox = Path.Combine(Path.GetTempPath(), "belltower-observer-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(inbox);
        try
        {
            using var observer = new CaptureObserver(inbox);
            var applied = Guid.NewGuid().ToString("N");
            var lost = Guid.NewGuid().ToString("N");
            foreach (var id in new[] { applied, lost })
            {
                File.WriteAllText(Path.Combine(inbox, "6390000000000000000-" + id + ".json"), "not an envelope"); // unreadable: the writer stays unknown
            }
            SpinWait.SpinUntil(() => observer.Captures.Count == 2, 5000);

            var resolved = observer.ResolveFrom(new Dictionary<string, (string Provider, string Session)> { [applied] = ("codex", "bench-codex-1") });

            Assert.Equal(1, resolved);
            Assert.Equal(("codex", "bench-codex-1"), observer.Captures.Where(capture => capture.Id == applied).Select(capture => (capture.Provider!, capture.SessionId!)).Single());
            Assert.Null(observer.Captures.Single(capture => capture.Id == lost).Provider);
        }
        finally
        {
            Directory.Delete(inbox, true);
        }
    }

    private static StateView Session(string attachment, string events, string extra = "") =>
        new(JsonNode.Parse("{\"attachmentId\":\"" + attachment + "\",\"waits\":[],\"completedOperations\":[" + extra + "],\"causalEvents\":[" + events + "]}"));

    private static string Event(string attachment, string? operation = null) =>
        "{\"id\":\"" + Guid.NewGuid().ToString("N") + "\",\"attachmentId\":\"" + attachment + "\"" + (operation is null ? "" : ",\"operationId\":\"" + operation + "\"") + "}";

    [Fact]
    public void AnIdLossNeedsTheProbeInTheJournalNotAppliedAndExactlyTheDocumentedIds()
    {
        var shared = "codex-session-aaa";
        var own = "codex-1-2";
        // The documented direction: session on the shared id, the probe event under the process id, not applied.
        Assert.True(LossPredicates.IdLoss(Session(shared, Event(shared) + "," + Event(own, "toolu_p")), "toolu_p", shared, own));
        // Reversed direction, a different event id, an applied probe and a probe missing from the journal are other behaviors.
        Assert.False(LossPredicates.IdLoss(Session(own, Event(shared) + "," + Event(own, "toolu_p")), "toolu_p", shared, own));
        Assert.False(LossPredicates.IdLoss(Session(shared, Event(shared) + "," + Event("att-x", "toolu_p")), "toolu_p", shared, own));
        Assert.False(LossPredicates.IdLoss(Session(shared, Event(shared) + "," + Event(own, "toolu_p"), "\"toolu_p\""), "toolu_p", shared, own));
        Assert.False(LossPredicates.IdLoss(Session(shared, Event(shared)), "toolu_p", shared, own));
    }

    [Fact]
    public void AFreshIdLossNeedsTheProbeEventInTheJournalUnderAForeignId()
    {
        string Ids(params string[] ids) => string.Join(",", ids.Select(id => Event(id)));
        // Two events under two ids and no probe event (the pre and permission captures were lost or rejected) must not count as the documented loss.
        Assert.False(LossPredicates.FreshIdLoss(Session("a", Ids("a", "b")), "toolu_plain_1"));
        // Four events with the probe under a foreign id: the documented loss.
        Assert.True(LossPredicates.FreshIdLoss(Session("a", Event("a") + "," + Event("b") + "," + Event("c", "toolu_plain_1") + "," + Event("d")), "toolu_plain_1"));
        // The probe under the session's own id would have been applied: not a loss.
        Assert.False(LossPredicates.FreshIdLoss(Session("a", Event("a") + "," + Event("b") + "," + Event("a", "toolu_plain_1") + "," + Event("d")), "toolu_plain_1"));
    }

    [Fact]
    public void TheStableStateWaitsOnlyForTheGraceAfterTheLastHookAndNeverRetries()
    {
        var now = new DateTime(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);
        var grace = TimeSpan.FromMinutes(1);
        var margin = TimeSpan.FromSeconds(15);
        // A hook ten seconds ago: 65 more seconds until every grace window has passed.
        Assert.Equal(TimeSpan.FromSeconds(65), StableState.RemainingQuiet([now.AddSeconds(-10), now.AddMinutes(-20)], grace, margin, now));
        // The latest hook decides; an old one and no hooks need no wait.
        Assert.Equal(TimeSpan.Zero, StableState.RemainingQuiet([now.AddMinutes(-5)], grace, margin, now));
        Assert.Equal(TimeSpan.Zero, StableState.RemainingQuiet([], grace, margin, now));
    }

    [Fact]
    public void AnOperationsAttachmentIsReadFromItsOwnJournalEventNotFromTheLatestOne()
    {
        var state = Session("s", Event("s") + "," + Event("x", "toolu_a") + "," + Event("y", "toolu_b") + "," + Event("z"));
        Assert.Equal("x", state.AttachmentOfOperation("toolu_a"));
        Assert.Equal("y", state.AttachmentOfOperation("toolu_b"));
        Assert.Equal("", state.AttachmentOfOperation("toolu_none"));
        Assert.Equal("z", state.LastEventAttachmentId);
    }

    [Fact]
    public void TheWindowGuardOwnsOnlyALiveImageUnderTheSandboxInstallAndNeverRemembersAnEarlierOwner()
    {
        var install = @"D:\repo\artifacts\field-acceptance\runs\1\sandbox\x\install";
        var sandboxImage = install + @"\versions\0.1.11\bell-tower.exe";
        // A sandbox tray's image counts; a user process (the user's own tray, a reused process id) and an unreadable or exited process do not.
        Assert.True(SandboxWindowGuard.IsSandboxImage(sandboxImage, install));
        Assert.False(SandboxWindowGuard.IsSandboxImage(@"C:\Users\someone\AppData\Local\Programs\ZeusApps.BellTower\bell-tower.exe", install));
        Assert.False(SandboxWindowGuard.IsSandboxImage(install + @"-other\bell-tower.exe", install));
        Assert.False(SandboxWindowGuard.IsSandboxImage(null, install));

        // The lookup is injected and read live on every call; the seam names a sandbox image only for an impossible process id, so the guard thread never
        // sees a real window as owned (an always-sandbox lookup would hide the user's windows).
        var image = sandboxImage;
        using var guard = new SandboxWindowGuard(install, pid => pid == int.MaxValue ? image : null);
        Assert.Empty(guard.VisibleNow());
        image = @"C:\Windows\explorer.exe";
        Assert.False(SandboxWindowGuard.IsSandboxImage(image, install));
    }
}
