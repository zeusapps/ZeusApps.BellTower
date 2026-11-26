using System.Text.Json;
using System.Text.Json.Nodes;
using BellTower.Distribution;
using BellTower.FieldAcceptance.Evidence;
using BellTower.FieldAcceptance.Harness;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Updates;

namespace BellTower.FieldAcceptance.Scenarios;

/// <summary>
/// One group (Claude Code alone, Codex alone, or both together) of <see cref="FaultsAndRollbackScenario"/> on its own installation. The phases
/// run in order: the published 0.1.9 with live agents, the upgrade under load, the candidate image and the fault injections, the rollback to
/// 0.1.9 with the candidate-image hooks and the faults again under 0.1.9, and the second upgrade. Provider differences come from
/// <see cref="ProviderModel"/>; nothing here branches on a provider name.
/// </summary>
public sealed class FaultGroupRun
{
    private readonly RunContext context;
    private readonly string scenario;
    private readonly string group;
    private readonly ProviderModel[] models;
    private readonly int firstNumber;
    private readonly string label;
    private readonly List<Conv> conversations = [];
    private Instance instance = null!;
    private string main = "";
    private string baselineVersion = "";
    private string candidateVersion = "";
    private readonly Dictionary<string, DateTime> lastHookAt = [];
    private Dictionary<string, string?> baselineImages = [];
    private int switches;
    private bool installed;

    public FaultGroupRun(RunContext context, string scenario, string group, ProviderModel[] models, int firstNumber)
    {
        this.context = context;
        this.scenario = scenario;
        this.group = group;
        this.models = models;
        this.firstNumber = firstNumber;
        label = models.Length == 1 ? models[0].Name : ProviderNames.Mixed;
    }

    private sealed class Conv(ProviderModel model, string role, int number, LaunchKind kind)
    {
        public ProviderModel Model { get; } = model;
        /// <summary>How the user reached the provider (the environment and arguments the hook sees); every non-wait conversation is a Bell Tower managed launch.</summary>
        public LaunchKind Kind { get; } = kind;
        public string Role { get; } = role;
        public string Id => Model.Name + "-" + Role;
        /// <summary>Started by the candidate after the setup rerun, not registered by 0.1.9 (ANALYSIS-264).</summary>
        public bool Fresh => Role.StartsWith("wait-fresh-", StringComparison.Ordinal);
        public string Key => Model.Name + ":" + Id;
        public string Ticket => "DEP-" + number;
        public string Change => Ticket.ToLowerInvariant() + "-login";
        public string Branch => Ticket + "-feat";
        public string Worktree { get; set; } = "";
        public string? Attachment => Kind.ManagedAttachment ? "att-" + Id : null;
        public AgentSession Session { get; set; } = null!;
        /// <summary>The second, identifiable process of a conversation whose startup captured none (the repair).</summary>
        public AgentSession? Identified { get; set; }
        public IEnumerable<AgentSession> Sessions => (Session is null ? [] : new[] { Session }).Concat(Identified is null ? [] : new[] { Identified });
    }

    /// <summary>Hooks fired in one phase: what was sent and what the installed ingress accepted.</summary>
    private sealed class Segment
    {
        public int Fired { get; private set; }
        public int Accepted { get; private set; }
        public List<string> Failures { get; } = [];
        public bool AllAccepted => Fired > 0 && Fired == Accepted;
        public string Summary => Accepted + " of " + Fired + " hooks accepted" + (Failures.Count == 0 ? "" : " (" + string.Join("; ", Failures.Take(2)) + ")");

        public void Note(string what, bool accepted, ProcessResult result)
        {
            Fired++;
            if (accepted)
            {
                Accepted++;
            }
            else
            {
                Failures.Add(what + " exit " + result.ExitCode + " " + result.Error.Trim());
            }
        }
    }

    private sealed record View(string Wait, string Identity, string Source, string Checkout, string Work, string Name, string Health, string Activity);

    private readonly Dictionary<string, Dictionary<string, View>> reference = [];

    // User decision 2026-11-26 (task 2.4 findings 1 and 2): documented in 0.1.11, not fixed.
    private const string LimitationText = "EXPECTED FAILURE (documented limitation, user decision 2026-11-26): a plain codex --no-daemon conversation that 0.1.9 registered keeps its attachment codex-session-<hash>; " +
        "once its hooks run the candidate ingress (which emits the process id) every non-registration event is ignored, by the candidate tray and by a rolled-back 0.1.9 alike, until the conversation registers again. " +
        "Recovery: rerun setup, then resume or restart that conversation.";

    // ANALYSIS-240 (orchestrator fix cycle 1): the default Bell Tower Codex launch. Pending a user decision; no product wire-id change in this cycle.
    private const string OwnedRuntimeLimitationText = "EXPECTED FAILURE (ANALYSIS-240, pending user decision): a Bell Tower owned-runtime Codex conversation is followed by the candidate, but a rolled-back 0.1.9 receives the candidate ingress's " +
        "provisional wire id (codex-session-<hash>, the managed id only in the evidence) and ignores every non-registration event of a session registered under the managed id. Recovery: rerun the older version's setup, then resume or restart the conversation.";

    private void Check(string id, bool passed, string detail, string? provider = null) =>
        context.Check(scenario, group + "/" + id, provider ?? label, passed, detail);

    private string Data => instance.Layout.Data;
    private string Inbox => Path.Combine(Data, CaptureProtocol.InboxName);
    private bool Drained() => SpoolInjection.Pending(Inbox).Count == 0;
    private bool Until(Func<bool> condition, int milliseconds = 60000) => TowerReader.WaitFor(condition, milliseconds);
    private StateView State(Conv conv) => StateView.Read(instance, conv.Key);
    private bool AllAlive() => conversations.SelectMany(conv => conv.Sessions).All(session => session.IsRunning);
    private int AliveCount() => conversations.SelectMany(conv => conv.Sessions).Count(session => session.IsRunning);
    private IEnumerable<Conv> Role(string role) => conversations.Where(conv => conv.Role == role);
    private IEnumerable<Conv> Waiters() => conversations.Where(conv => conv.Role.StartsWith("wait-", StringComparison.Ordinal) && conv.Session is not null);
    private IEnumerable<Conv> BaselineWaiters() => conversations.Where(conv => conv.Role.StartsWith("wait-", StringComparison.Ordinal) && !conv.Fresh);

    // One post-only hook per conversation, then whether each conversation's session recorded it (per conversation, so a launch kind that loses hooks is named).
    private Dictionary<string, bool> Probe(Segment segment, string tag, IEnumerable<Conv> targets, int milliseconds = 45000)
    {
        var list = targets.ToList();
        foreach (var conv in list)
        {
            Tool(segment, conv, "PostToolUse", tag, "toolu_" + tag);
        }
        // Wait for the event to reach the tray's journal (applied or not), then read whether it was applied: a dropped event costs no timeout.
        Until(() => list.All(conv => State(conv).JournalOperationIds.Contains("toolu_" + tag)), milliseconds);
        return list.ToDictionary(conv => conv.Key, conv => State(conv).Completed.Contains("toolu_" + tag));
    }

    private string SharedId(Conv conv) => BellTower.Core.NativeAdoption.SharedId(conv.Model.Name, conv.Id);

    private string OwnId(Conv conv) => BellTower.Core.NativeAdoption.OwnId(conv.Model.Name, BellTower.IO.ProcessProbe.Identify(conv.Session.ProcessId) ?? throw new IOException("The agent process identity could not be read."));

    /// <summary>The attachment ids a conversation's documented loss involves (ANALYSIS-263): the session's and the probe event's, each exact.</summary>
    private (string Session, string Event) ExpectedLoss(Conv conv, bool rollback) =>
        conv.Kind.HooksLostAcrossUpgrade ? (SharedId(conv), OwnId(conv)) : rollback && conv.Kind.HooksLostAcrossRollback ? (conv.Attachment!, SharedId(conv)) : throw new InvalidOperationException("No documented loss for " + conv.Key);

    private string Diagnosis(Conv conv) => conv.Kind.Meaning + ": session on attachment " + State(conv).AttachmentId + ", latest hook event carries " + State(conv).LastEventAttachmentId;
    private SessionRow? Row(Conv conv) => TowerReader.List(instance).SingleOrDefault(row => row.Key == conv.Key);
    private JsonObject Identity(Conv conv) => Row(conv)?.Identity ?? [];
    private static string Ids(IEnumerable<string> ids) => string.Join(", ", ids);

    // ---- The run -----------------------------------------------------------------------------------------------------------------------

    public void Run()
    {
        instance = context.CreateInstance("faults-" + group);
        main = Path.Combine(instance.Layout.Root, "work", "D360");
        SandboxGit.InitRepository(instance, main);
        var number = firstNumber;
        foreach (var model in models)
        {
            foreach (var role in new[] { "work", "fault", "ghost", "heal" })
            {
                conversations.Add(new Conv(model, role, number++, model.LaunchKinds[0]));
            }
            // A plain launch whose hooks see no identifiable agent process (ANALYSIS-242).
            conversations.Add(new Conv(model, "ghostplain", number++, model.LaunchKinds.First(kind => kind.Name == "plain")));
            // One wait conversation per real way of reaching the provider: the hook ids differ by launch kind between 0.1.9 and the candidate.
            foreach (var kind in model.LaunchKinds)
            {
                conversations.Add(new Conv(model, "wait-" + kind.Name, number++, kind));
            }
            // The default Bell Tower Codex launch first started by the candidate, after the setup rerun, with its runtime record (ANALYSIS-264).
            foreach (var kind in model.LaunchKinds.Where(kind => kind.OwnedRuntime))
            {
                conversations.Add(new Conv(model, "wait-fresh-" + kind.Name, number++, kind));
            }
            number = firstNumber + 50;
        }
        candidateVersion = context.Report.Candidate!.Version;
        try
        {
            RunBaselinePhase();
            RunUpgradePhase();
            RunCandidatePhase();
            RunRollbackPhase();
            RerunBaselineSetupAfterRollback();
            RunReupgradePhase();
        }
        finally
        {
            try
            {
                if (installed)
                {
                    RunFinalChecks();
                }
            }
            finally
            {
                // The group is over: its agents and tray end now so the next group (or scenario) runs alone. The files go with the run.
                instance.Dispose();
            }
        }
    }

    // ---- Hooks ---------------------------------------------------------------------------------------------------------------------------

    private bool Send(Segment segment, Conv conv, string hookEvent, string payload, string what, AgentSession? via = null)
    {
        var command = CandidateInstall.Command(instance, conv.Model, hookEvent);
        // The 0.1.9 Codex command only parses in the command shell: until setup is rerun its hooks run through the labeled modeled fallback.
        // The shell follows the command that is installed (ANALYSIS-246): the 0.1.9 image runs through the modeled fallback, the candidate image through the provider's own shell.
        var legacy = string.Equals(IngressImage.HashOf(command), baselineImages.GetValueOrDefault(conv.Model.Name), StringComparison.OrdinalIgnoreCase);
        lastHookAt[conv.Key] = DateTime.UtcNow;
        var shell = legacy ? conv.Model.ModeledFallbackShell(instance) ?? conv.Model.RuntimeShell(instance) : conv.Model.RuntimeShell(instance);
        var result = (via ?? conv.Session).Hook(command, payload, shell);
        var accepted = conv.Model.Accepted(result);
        segment.Note(conv.Key + " " + what, accepted, result);
        return accepted;
    }

    private bool Launch(Segment segment, Conv conv, string hookEvent, AgentSession? via = null) =>
        Send(segment, conv, hookEvent, conv.Model.Payload(hookEvent, conv.Id, main, instance.Layout), hookEvent, via);

    private bool Tool(Segment segment, Conv conv, string hookEvent, string command, string? toolUseId = null, AgentSession? via = null) =>
        Send(segment, conv, hookEvent, conv.Model.ToolPayload(hookEvent, conv.Id, main, instance.Layout, command, toolUseId), hookEvent + " " + command, via);

    private void StartConversation(Segment segment, Conv conv, bool unidentified = false)
    {
        // An owned runtime starts under a dedicated manager process whose record names it (written by the session start, before the first hook).
        conv.Session = instance.StartSession(conv.Model.Name, conv.Id, main, conv.Attachment, conv.Kind.Arguments, unidentified, conv.Kind.Dedicated, conv.Kind.OwnedRuntime);
        Launch(segment, conv, "SessionStart");
        Launch(segment, conv, "UserPromptSubmit");
    }

    // One resolved operation and one pending wait, which must survive every later phase.
    private void SeedWaiter(Segment segment, Conv conv)
    {
        Tool(segment, conv, "PreToolUse", "resolved-a", "toolu_resolved_a");
        Tool(segment, conv, "PermissionRequest", "resolved-a");
        Tool(segment, conv, "PostToolUse", "resolved-a", "toolu_resolved_a");
        Tool(segment, conv, "PreToolUse", "pending-a", "toolu_pending_a");
        Tool(segment, conv, "PermissionRequest", "pending-a");
    }

    // ---- Phase 1: the published 0.1.9 ---------------------------------------------------------------------------------------------------

    private void RunBaselinePhase()
    {
        instance.ExtractInstallation(context.Options.BaselineArchive);
        installed = true;
        baselineVersion = instance.Tower("--version").Output.Trim();
        var setup = instance.TowerWithHome("setup", "--clients", "both", "--apply");
        // Silent by construction (no sound, no update check), and the product's minimum attachment grace (one minute) so liveness verdicts fit the run.
        new ConfigStore(new DataPaths(Data)).Update(config => { config.Muted = true; config.Updates.Enabled = false; config.AttachmentEvidenceGraceMinutes = 1; });
        instance.Tower("start", "--silent");
        var running = Until(() => TowerReader.TrayRunning(instance), 40000);
        Check("baseline/installed", baselineVersion == "0.1.9" && setup.Succeeded && running,
            "Installed the published 0.1.9 archive (launcher reports " + baselineVersion + "), ran its own setup --clients both --apply (exit " + setup.ExitCode + ") and started its tray silently with a one-minute attachment grace (running: " + running + ").", ProviderNames.Harness);

        baselineImages = models.ToDictionary(model => model.Name, model => IngressImage.HashOf(CandidateInstall.Command(instance, model, "SessionStart")));
        // `work` has historical work in its own ticket worktree and is launched in the main checkout; `wait` ends this phase with one resolved operation
        // and one pending wait, which must survive every later phase.
        var launchedAt = DateTimeOffset.UtcNow;
        foreach (var conv in Role("work"))
        {
            conv.Worktree = Path.Combine(instance.Layout.Root, "work", "D360-worktrees", conv.Ticket);
            ConversationFixture.BuildWorktree(instance, main, conv.Worktree, conv.Branch, conv.Change);
            ConversationFixture.WriteHistory(instance, conv.Model, conv.Id, conv.Role, conv.Worktree, conv.Change, launchedAt.AddMinutes(-10));
        }
        var segment = new Segment();
        foreach (var conv in Role("work").Concat(BaselineWaiters()))
        {
            StartConversation(segment, conv);
        }
        foreach (var conv in BaselineWaiters())
        {
            SeedWaiter(segment, conv);
        }
        var pending = Until(() => Waiters().All(conv => State(conv).Waits == 1 && State(conv).Completed.Contains("toolu_resolved_a")));
        var recovered = Until(() => Role("work").All(conv => TowerReader.Text(Identity(conv), "activeWork") == conv.Change && Row(conv)?.Checkout is { Length: > 0 }), 150000);
        Check("baseline/sessions", segment.AllAccepted && pending && recovered && AllAlive(),
            "Under 0.1.9 (hooks through the 0.1.9 image" + (models.Any(model => model.ModeledFallbackShell(instance) is not null) ? ", Codex through the modeled cmd fallback" : "") + "): " + segment.Summary + "; each wait conversation shows its one pending wait and its resolved operation; " +
            "each work conversation's historical work was recovered from its own transcript (" + Ids(Role("work").Select(conv => conv.Key + "=" + TowerReader.Text(Identity(conv), "activeWork"))) + "); " + AliveCount() + " agent sessions are running.");
    }

    // ---- Phase 2: the upgrade under load ------------------------------------------------------------------------------------------------

    private void RunUpgradePhase()
    {
        var installation = new Installation(instance.Layout.Install);
        var paths = new DataPaths(Data);
        var offer = DistributionJson.Read<ReleaseOffer>(context.Options.CandidateArchive[..^4] + ".release.json");
        var trayAtStart = TowerReader.TrayRunning(instance);
        var plan = instance.WithProcessEnvironment(() =>
        {
            var staged = UpdateTransaction.Stage(installation, paths, context.Options.CandidateArchive, offer);
            UpdateTransaction.Start(staged);
            return staged;
        });
        // The coordinator now stops the tray, selects the candidate and starts its tray; hooks keep arriving all the while (post-only calls, so each
        // is unique and its delivery is countable). The swap above has returned, so firing hooks here is allowed.
        var window = new Segment();
        var ids = new List<string>();
        var rounds = 0;
        var firedWhileDown = 0;
        var outcomePath = UpdateTransaction.OutcomePath(installation);
        var deadline = DateTime.UtcNow.AddSeconds(150);
        UpdateOutcome? outcome = null;
        while (DateTime.UtcNow < deadline)
        {
            outcome = File.Exists(outcomePath) ? TryReadOutcome(outcomePath) : null;
            if (outcome is { State: "monitoring-restored" or "failed" or "rolled-back" } || outcome is { State: "activated" } && !plan.HadTray)
            {
                break;
            }
            ids.Add("toolu_upd_" + rounds);
            foreach (var conv in Waiters())
            {
                // ANALYSIS-247: a hook counts as "under load" only if the coordinator had the tray stopped when it was fired (the tray's instance mutex, read in process).
                firedWhileDown += new BellTower.Tray.TrayController(paths).IsRunning() ? 0 : 1;
                Tool(window, conv, "PostToolUse", "upd-" + rounds, "toolu_upd_" + rounds);
            }
            rounds++;
        }
        var activated = outcome is { State: "monitoring-restored" };
        var reported = instance.Tower("--version").Output.Trim();
        var trayBack = Until(() => TowerReader.TrayRunning(instance), 60000);
        var drained = Until(Drained, 60000);
        var delivered = Until(() => Waiters().All(conv => ids.All(id => State(conv).Completed.Contains(id))), 90000);
        var stable = Waiters().All(conv => State(conv).Waits == 1 && State(conv).Operations.SetEquals(["toolu_pending_a"]) && State(conv).Completed.Contains("toolu_resolved_a"));
        Check("upgrade/under-load", trayAtStart && plan.HadTray && firedWhileDown > 0 && activated && reported == candidateVersion && trayBack && window.AllAccepted && drained && delivered && stable && AllAlive(),
            "The real update transaction upgraded 0.1.9 to " + candidateVersion + " with the tray running and agents live (outcome " + (outcome?.State ?? "none") + ", launcher " + reported + ", tray restarted by the coordinator: " + trayBack + "). Throughout, " + ids.Count +
            " post-only hooks per wait conversation (0.1.9 image) kept arriving, " + firedWhileDown + " of the " + window.Fired + " hooks fired while the coordinator had the tray stopped: " + window.Summary + "; the spool drained and every one is in the completed-operation memory of its session (" + delivered + "); the pending wait is still exactly one per wait conversation and the resolved operation did not return (" + stable + "); " + AliveCount() + " agent sessions still running.");

        // The candidate's own setup: the owned hooks now name the candidate's ingress image.
        var rerun = instance.TowerWithHome(300000, "setup", "--host-bridge", "--apply");
        var candidateHash = installation.Contract(candidateVersion).Files!["bell-tower-ingress.exe"];
        var named = models.Select(model => IngressImage.HashOf(CandidateInstall.Command(instance, model, "SessionStart"))).ToList();
        Check("upgrade/hooks-reference-candidate-image", rerun.Succeeded && named.All(hash => string.Equals(hash, candidateHash, StringComparison.OrdinalIgnoreCase)) && TowerReader.TrayRunning(instance),
            "setup --host-bridge --apply (exit " + rerun.ExitCode + ") rewrote the owned hooks to the candidate's ingress image " + IngressImage.Short(candidateHash) + "; from here every hook runs the candidate's ingress through the provider's own shell (" +
            Ids(models.Select(model => model.Name + " through " + model.RuntimeShell(instance).Name)) + ").");
        StartFreshOwnedRuntime();
        ProbeCandidateAfterSetup("upgrade", "ref_1");
    }

    // ANALYSIS-264: a default Bell Tower Codex launch whose first hook the candidate sees, with the runtime record: the record (not a launch record) verifies it.
    private void StartFreshOwnedRuntime()
    {
        var fresh = conversations.Where(conv => conv.Fresh).ToList();
        var segment = new Segment();
        foreach (var conv in fresh)
        {
            StartConversation(segment, conv);
            SeedWaiter(segment, conv);
        }
        if (fresh.Count == 0)
        {
            return;
        }
        var seeded = Until(() => fresh.All(conv => State(conv).Waits == 1 && State(conv).Completed.Contains("toolu_resolved_a")), 60000);
        Check("upgrade/fresh-owned-runtime-registered", segment.AllAccepted && seeded && fresh.All(conv => State(conv).AttachmentId == conv.Attachment),
            "A default Bell Tower Codex launch first seen by the candidate (the backend under its own manager helper, the runtime record naming that helper) registered under its managed id and made its one wait: " +
            Ids(fresh.Select(conv => conv.Key + " on " + State(conv).AttachmentId)) + "; " + segment.Summary + ".", ProviderNames.Codex);
    }

    // After a setup rerun under the candidate: one probe per launch kind. The documented loss is an expected failure tied to its mechanism (QA-241, ANALYSIS-243):
    // it passes only while the event reached the journal, was not applied, and its attachment id differs from the session's in the shared-form way.
    private void ProbeCandidateAfterSetup(string phase, string tag)
    {
        var segment = new Segment();
        var applied = Probe(segment, tag, Waiters());
        foreach (var conv in Waiters())
        {
            var expectLoss = conv.Kind.HooksLostAcrossUpgrade;
            var ok = applied[conv.Key];
            var mechanism = expectLoss && LossPredicates.IdLoss(State(conv), "toolu_" + tag, ExpectedLoss(conv, false).Session, ExpectedLoss(conv, false).Event);
            context.Check(scenario, group + "/" + phase + "/candidate-hooks-applied-after-setup-refresh/" + conv.Role, conv.Model.Name, segment.AllAccepted && (expectLoss ? mechanism : ok),
                (expectLoss ? LimitationText + " " : "") + "After the setup rerun under the candidate, a hook through the candidate-image command of a " + Diagnosis(conv) + " was " + (ok ? "applied by the candidate tray" : "NOT applied by the candidate tray") +
                (expectLoss ? (mechanism ? ": the event reached the journal and was ignored for the documented reason (session on its shared id, the event carrying the agent's own process id)." : ": UNEXPECTED, the documented loss did not occur by its mechanism; revisit the limitation.") : "."), expectedFailure: expectLoss);
        }
    }

    private static UpdateOutcome? TryReadOutcome(string path)
    {
        try
        {
            return DistributionJson.Read<UpdateOutcome>(path);
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return null;
        }
    }

    // ---- Phase 3: the candidate, with faults --------------------------------------------------------------------------------------------

    private void RunCandidatePhase()
    {
        var unique = Until(() => Role("work").All(conv => TowerReader.Text(Identity(conv), "activeWork") == conv.Change && Row(conv)?.CheckoutSource == "unique-work"), 150000);
        Check("candidate/work-identity-kept", unique,
            "After the upgrade the candidate kept each work conversation's association and verified checkout (" + Ids(Role("work").Select(conv => conv.Key + " " + (Row(conv)?.CheckoutSource ?? "unread") + " in " + Path.GetFileName(Row(conv)?.Checkout ?? ""))) + ").");

        InjectNullProcessStartup();
        InjectLiveFaults();
        InjectDowntimeDelivery();
        EstablishRetainedCheckout();

        // The ghost conversations were registered with no process: past the one-minute grace they must read unavailable, never working.
        var unavailable = Until(() => Role("ghost").All(conv => Row(conv) is { AttachmentHealth: "unavailable", Activity: "unavailable" }), 240000);
        Check("candidate/null-process-unavailable", unavailable && AllAlive(),
            "A conversation whose startup captured no agent process stays unverified within the grace and reads unavailable after it (activity unavailable, not working), never verified live: " +
            Ids(Role("ghost").Select(conv => conv.Key + " health " + (Row(conv)?.AttachmentHealth ?? "unread") + ", activity " + (Row(conv)?.Activity ?? "unread"))) + "; its agent process was left running (" + AliveCount() + " sessions alive).");
        reference["candidate"] = Snapshot();
    }

    // A startup whose hooks captured no agent process: the fake agent's image is not the provider's, so the product's ancestry walk finds none.
    private void InjectNullProcessStartup()
    {
        var segment = new Segment();
        foreach (var conv in Role("ghost").Concat(Role("heal")))
        {
            StartConversation(segment, conv, unidentified: true);
        }
        var keys = Role("ghost").Concat(Role("heal")).Select(conv => conv.Key).ToList();
        TowerReader.WaitForSessions(instance, keys);
        var facts = Role("ghost").Concat(Role("heal")).Select(conv => (conv, attachment: StateView.Attachment(instance, conv.Key), row: Row(conv))).ToList();
        var processless = facts.All(fact => fact.attachment is not null && fact.attachment["process"] is null);
        var notLive = facts.All(fact => fact.row is not null && fact.row.AttachmentHealth != "verified-live");
        Check("candidate/null-process-startup", segment.AllAccepted && processless && notLive,
            "Hooks of " + keys.Count + " conversations started with no agent process in their ancestry: " + segment.Summary + "; each attachment record carries no process identity and none reads verified live yet (" +
            Ids(facts.Select(fact => fact.conv.Key + " " + (fact.row?.AttachmentHealth ?? "unread"))) + "); the hook was accepted and the capture durable, only the liveness evidence is missing.");

        // The repair: a later hook of the same conversation (same attachment) from the identifiable agent process. Claude Code treats the unclassified
        // startup as direct, so the hook fills the missing identity in; Codex fails an unclassified startup closed to the shared runtime (a Codex may share
        // a backend), so the same hook cannot repair it and the conversation must read unavailable, never working, once the grace has passed.
        var repair = new Segment();
        foreach (var conv in Role("heal"))
        {
            conv.Identified = instance.StartSession(conv.Model.Name, conv.Id, main, conv.Attachment, conv.Kind.Arguments, false, conv.Kind.Dedicated);
            Launch(repair, conv, "UserPromptSubmit", conv.Identified);
        }
        bool Repaired(Conv conv) => StateView.Attachment(instance, conv.Key)?["process"] is not null && Row(conv)?.AttachmentHealth == "verified-live";
        bool HonestlyUnavailable(Conv conv) => StateView.Attachment(instance, conv.Key)?["process"] is null && Row(conv) is { AttachmentHealth: "unavailable", Activity: "unavailable" };
        bool AsExpected(Conv conv) => conv.Model.UnclassifiedStartupFailsClosed ? HonestlyUnavailable(conv) : Repaired(conv);
        var settled = Until(() => Role("heal").All(AsExpected), 180000);
        Check("candidate/null-process-repaired-or-unavailable", repair.AllAccepted && settled && AllAlive(),
            "A later hook from the identifiable agent process of the same conversation (" + repair.Summary + ") " + (settled ? "produced the adapter's documented outcome: " : "did NOT produce the adapter's documented outcome: ") +
            Ids(Role("heal").Select(conv => conv.Key + " " + (Repaired(conv) ? "repaired (process captured, verified live)" : HonestlyUnavailable(conv) ? "honestly unavailable (no process, activity unavailable, not working)" : "health " + (Row(conv)?.AttachmentHealth ?? "unread")) +
                " [expected " + (conv.Model.UnclassifiedStartupFailsClosed ? "unavailable: an unclassified startup fails closed to the shared runtime" : "repaired") + "]")) + ".");

        // ANALYSIS-241: what the repaired or fail-closed conversation does with its later hooks. A permission request from the identifiable process must make
        // a wait where the conversation kept one attachment id (Claude Code); where it did not (Codex: the first hook registered the shared id, the later ones
        // carry the managed id) the events reach the journal and are ignored. The outcome is reported as it is, not hidden behind "unavailable".
        var later = new Segment();
        var healBase = Role("heal").ToDictionary(conv => conv.Key, conv => State(conv).Journal.Count);
        foreach (var conv in Role("heal"))
        {
            Tool(later, conv, "PreToolUse", "heal-1", "toolu_heal_1", conv.Identified);
            Tool(later, conv, "PermissionRequest", "heal-1", null, conv.Identified);
        }
        Until(() => Role("heal").All(conv => State(conv).Journal.Count >= healBase[conv.Key] + 2), 45000);
        foreach (var conv in Role("heal"))
        {
            var expectLoss = conv.Model.UnclassifiedStartupFailsClosed;
            var waits = State(conv).Waits;
            var ok = expectLoss ? waits == 0 && LossPredicates.IdLoss(State(conv), "toolu_heal_1", SharedId(conv), conv.Attachment!) : waits == 1;
            context.Check(scenario, group + "/candidate/null-process-later-hooks/" + conv.Role, conv.Model.Name, later.AllAccepted && ok,
                (expectLoss ? "EXPECTED FAILURE (documented by this check, pending the 5.2 parity decision): " : "") + "A pre hook and a permission request from the identifiable agent process of the repaired-or-unavailable conversation " +
                (waits == 1 ? "made its one wait" : "made no wait (waits " + waits + "); the events reached the tray's journal and were ignored: the session is on attachment " + State(conv).AttachmentId + " while the later hooks carry " + State(conv).LastEventAttachmentId) + ".",
                expectedFailure: expectLoss);
        }

        // ANALYSIS-242: a plain launch whose hooks find no agent process gets a fresh random attachment id per hook from the ingress (0.1.9 and candidate alike,
        // Hosts/AttachmentCapture.cs:110-111; v0.1.9 :30-31), so Claude Code's later hooks never match the session; Codex's shared form is stable.
        var plain = new Segment();
        foreach (var conv in Role("ghostplain"))
        {
            StartConversation(plain, conv, unidentified: true);
            Tool(plain, conv, "PreToolUse", "plain-1", "toolu_plain_1");
            Tool(plain, conv, "PermissionRequest", "plain-1");
        }
        Until(() => Role("ghostplain").All(conv => State(conv).Journal.Count >= 4), 45000);
        foreach (var conv in Role("ghostplain"))
        {
            var state = State(conv);
            var stable = !conv.Model.NullProcessPlainStartupGetsFreshIds;
            var ok = stable ? state.Waits == 1 && state.JournalAttachmentIds.Count == 1 : LossPredicates.FreshIdLoss(state, "toolu_plain_1");
            context.Check(scenario, group + "/candidate/null-process-plain-startup/" + conv.Role, conv.Model.Name, plain.AllAccepted && ok,
                (stable ? "" : "EXPECTED FAILURE (pre-existing in 0.1.9, ANALYSIS-242, pending decision): ") + "A plain " + conv.Model.Name + " whose hooks find no agent process sent a start, a prompt, a pre hook and a permission request: the journal holds events under " + state.JournalAttachmentIds.Count +
                " attachment id(s)" + (stable ? " (the shared form is stable)" : " (a fresh random id per hook)") + " and the conversation has " + state.Waits + " wait(s)" + (stable ? "." : "; the probe event (toolu_plain_1) is in the journal under " + state.AttachmentOfOperation("toolu_plain_1") + " while the session is on " + state.AttachmentId + ", so it was ignored."), expectedFailure: !stable);
        }
        CheckNullProcessAncestry();
    }

    // QA-260: the null-process verdicts rest on an unidentified agent whose ancestry holds no provider process. The harness reports the ancestry it relies on
    // and the host it runs under, so a host-dependent result fails loudly instead of silently changing a verdict.
    private void CheckNullProcessAncestry()
    {
        var unidentified = Role("ghost").Concat(Role("heal")).Concat(Role("ghostplain")).ToList();
        var offenders = unidentified.Where(conv => conv.Session.Ancestry.Any(name => name.Equals(conv.Model.Name + ".exe", StringComparison.OrdinalIgnoreCase))).Select(conv => conv.Key).ToList();
        var host = AgentSession.ProductAncestry(Environment.ProcessId);
        var hostProvider = host.Where(name => name.Equals("claude.exe", StringComparison.OrdinalIgnoreCase) || name.Equals("codex.exe", StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
        Check("candidate/null-process-ancestry", unidentified.Count > 0 && offenders.Count == 0,
            "The unidentified fake agents (" + unidentified.Count + ") were started through a detached spawner, so the product's ancestry walk above each of them is " + Ids(unidentified.Select(conv => conv.Key + ": " + (conv.Session.Ancestry.Count == 0 ? "empty (dead parent)" : string.Join(" <- ", conv.Session.Ancestry)))) +
            "; none holds a provider image (" + (offenders.Count == 0 ? "asserted" : "VIOLATED by " + Ids(offenders)) + "). The harness itself runs under " + (hostProvider.Count == 0 ? "no provider process" : "a provider host (" + Ids(hostProvider) + ")") +
            " (its own ancestry: " + string.Join(" <- ", host.Take(8)) + (host.Count > 8 ? " <- ..." : "") + "); the verdicts do not depend on it.");
    }

    // Lost, late and duplicate pre hooks against the running candidate tray.
    private void InjectLiveFaults()
    {
        var segment = new Segment();
        var faults = Role("fault").ToList();
        foreach (var conv in faults)
        {
            StartConversation(segment, conv);
        }
        TowerReader.WaitForSessions(instance, faults.Select(conv => conv.Key).ToList());
        Until(() => faults.All(conv => State(conv).Journal.Count >= 2));
        var baseline = faults.ToDictionary(conv => conv.Key, conv => State(conv).Journal.Count);
        bool Processed(int events) => faults.All(conv => State(conv).Journal.Count >= baseline[conv.Key] + events);

        // Lost pre: only the post arrives. The operation completes, nothing waits and nothing is left in flight.
        foreach (var conv in faults)
        {
            Tool(segment, conv, "PostToolUse", "lost-1", "toolu_lost_1");
        }
        var lost = Until(() => Processed(1)) && faults.All(conv => State(conv).Waits == 0 && State(conv).Completed.Contains("toolu_lost_1") && !State(conv).Operations.Contains("toolu_lost_1"));
        Check("candidate/lost-pre-hook", lost, "A post-only call (its pre hook lost) completed without a wait or an operation left in flight for " + Ids(faults.Select(conv => conv.Key)) + ".");

        // Late pre: the permission request and the pre hook of an operation arrive after its post.
        foreach (var conv in faults)
        {
            Tool(segment, conv, "PostToolUse", "late-2", "toolu_late_2");
            Tool(segment, conv, "PreToolUse", "late-2", "toolu_late_2");
            Tool(segment, conv, "PermissionRequest", "late-2");
        }
        var late = Until(() => Processed(4)) && faults.All(conv => State(conv).Waits == 0 && !State(conv).Operations.Contains("toolu_late_2") && State(conv).Completed.Contains("toolu_late_2"));
        Check("candidate/late-pre-hook", late, "A pre hook and a permission request that arrived after the operation's post created no wait and no in-flight operation (the completed operation is not resurrected) for " + Ids(faults.Select(conv => conv.Key)) + ".");

        // The ordinary flow, then the same flow with duplicated hooks.
        foreach (var conv in faults)
        {
            Tool(segment, conv, "PreToolUse", "norm-3", "toolu_norm_3");
            Tool(segment, conv, "PermissionRequest", "norm-3");
        }
        var waiting = Until(() => faults.All(conv => State(conv).Waits == 1));
        foreach (var conv in faults)
        {
            Tool(segment, conv, "PostToolUse", "norm-3", "toolu_norm_3");
        }
        var resolved = Until(() => faults.All(conv => State(conv).Waits == 0 && State(conv).Completed.Contains("toolu_norm_3")));
        Check("candidate/pre-permission-post-flow", waiting && resolved, "An ordinary pre, permission request, post sequence made exactly one wait and the post resolved it for " + Ids(faults.Select(conv => conv.Key)) + ".");

        foreach (var conv in faults)
        {
            Tool(segment, conv, "PreToolUse", "dup-4", "toolu_dup_4");
            Tool(segment, conv, "PreToolUse", "dup-4", "toolu_dup_4");
            Tool(segment, conv, "PermissionRequest", "dup-4");
        }
        var single = Until(() => faults.All(conv => State(conv).Waits == 1));
        foreach (var conv in faults)
        {
            Tool(segment, conv, "PostToolUse", "dup-4", "toolu_dup_4");
            Tool(segment, conv, "PostToolUse", "dup-4", "toolu_dup_4");
        }
        var cleared = Until(() => faults.All(conv => State(conv).Waits == 0 && State(conv).Completed.Contains("toolu_dup_4") && !State(conv).Operations.Contains("toolu_dup_4")));
        Check("candidate/duplicate-hooks", single && cleared, "A duplicated pre hook still made exactly one wait and a duplicated post resolved it with nothing left over for " + Ids(faults.Select(conv => conv.Key)) + ".");
        Check("candidate/live-fault-hooks-accepted", segment.AllAccepted, "Every hook of the live fault injection exited 0 with {} through the installed candidate command: " + segment.Summary + ".");
    }

    // The tray is stopped by the user's Exit; hooks keep arriving; the spool is then shuffled and partly duplicated before the tray drains it.
    private void InjectDowntimeDelivery()
    {
        var faults = Role("fault").ToList();
        // Nothing may still be queued from the live phase: the downtime counts exactly the captures of its own hooks.
        var quiet = Until(Drained, 60000);
        instance.Tower("exit");
        var stopped = Until(() => !TowerReader.TrayRunning(instance), 40000);
        var userStop = File.Exists(HookTrayStart.UserStopRecord(Data));
        var journalBefore = faults.ToDictionary(conv => conv.Key, conv => State(conv).Journal.Count);
        var segment = new Segment();
        foreach (var conv in faults)
        {
            Tool(segment, conv, "PreToolUse", "s-1", "toolu_s_1");
            Tool(segment, conv, "PermissionRequest", "s-1");
            Tool(segment, conv, "PostToolUse", "s-1", "toolu_s_1");
            Tool(segment, conv, "PreToolUse", "s-2", "toolu_s_2");
            Tool(segment, conv, "PermissionRequest", "s-2");
            Tool(segment, conv, "PostToolUse", "s-3", "toolu_s_3");
            Tool(segment, conv, "PermissionRequest", "s-4");
            Tool(segment, conv, "PostToolUse", "s-4", "toolu_s_4");
            Tool(segment, conv, "PostToolUse", "s-5", "toolu_s_5");
            Tool(segment, conv, "PermissionRequest", "s-5");
        }
        var spooled = SpoolInjection.Pending(Inbox).Count;
        var stayedDown = TrayStayedDown();
        var attemptAfter = HookTrayStart.LastAttempt(Data);
        var noStart = attemptAfter is not { } started || started.Outcome != nameof(HookStartOutcome.Started);
        Check("candidate/tray-downtime-durable", quiet && stopped && userStop && segment.AllAccepted && spooled == segment.Fired && stayedDown && noStart,
            "With the tray stopped by the user's Exit (user stop recorded: " + userStop + "), " + segment.Fired + " approval-flow hooks through the candidate command were accepted (" + segment.Summary + "), exactly " + spooled +
            " captures waited durably in the spool, and no hook started a tray (tray still stopped: " + stayedDown + "; last start attempt " + (attemptAfter is { } attempt ? attempt.Outcome : "none") + ").");

        // Shuffle the drain order and deliver three captures twice.
        var shuffled = SpoolInjection.Reorder(Inbox, SpoolInjection.ShuffledOrder(spooled));
        var copies = SpoolInjection.Duplicate(Inbox, [0, spooled / 2, spooled - 1]);
        var queued = SpoolInjection.Pending(Inbox).Count;
        instance.Tower("start", "--silent");
        var back = Until(() => TowerReader.TrayRunning(instance), 40000);
        var drained = Until(Drained, 90000);
        var converged = Until(() => faults.All(conv => State(conv) is var state && state.Waits == 1 && state.Operations.SetEquals(["toolu_s_2"]) &&
            new[] { "toolu_s_1", "toolu_s_3", "toolu_s_4", "toolu_s_5" }.All(state.Completed.Contains)), 90000);
        var exactly = faults.All(conv => State(conv).Journal.Count - journalBefore[conv.Key] == 10 && State(conv).Journal.Distinct().Count() == State(conv).Journal.Count);
        Check("candidate/shuffled-and-duplicate-delivery", back && drained && converged && exactly && queued == spooled + copies.Count,
            "The spool was reordered (" + shuffled.Count + " captures drained in a reversed and rotated order) and " + copies.Count + " captures were delivered twice (" + queued + " files queued); the tray drained it (" + drained + ") and every conversation converged on the causal result: " +
            "the resolved operations stay resolved, the post-only and late-permission operations left no wait, and exactly the one genuinely pending wait (the permission request with no post) remains (" + converged + "); each journal gained exactly 10 events with no duplicate identity (" + exactly + ").");
    }

    // After the branch change in the worktree the candidate keeps the verified checkout as `retained` and records the additive checkoutWork field.
    private void EstablishRetainedCheckout()
    {
        foreach (var conv in Role("work"))
        {
            SandboxGit.SwitchNewBranch(instance, conv.Worktree, conv.Branch + "-hotfix");
        }
        var retained = Until(() => Role("work").All(conv => Row(conv) is { CheckoutSource: "retained" } && TowerReader.Text(Identity(conv), "activeWork") == ""), 150000);
        var stores = TowerReader.IdentityStores(instance);
        Check("candidate/retained-checkout-established", retained && Role("work").All(conv => stores.TryGetValue(conv.Key, out var store) && TowerReader.Text(store, "checkoutWork") == conv.Change),
            "After git switch -c in each work conversation's worktree the candidate listed checkoutSource retained, kept the verified worktree as the working checkout and stored the additive checkoutWork field (" +
            Ids(Role("work").Select(conv => conv.Key + " source " + (Row(conv)?.CheckoutSource ?? "unread") + ", checkoutWork '" + (stores.TryGetValue(conv.Key, out var store) ? TowerReader.Text(store, "checkoutWork") : "unread") + "'")) + ").");
    }

    // A tray a hook started runs with the real profile environment by product design. After a phase that must leave the tray down, a running tray is
    // replaced at once by a stopped one (the exit and end are observed, never assumed), so no later phase runs under that environment; the caller fails
    // its check, because the user's Exit must have kept the hooks from starting one.
    private bool TrayStayedDown()
    {
        if (!TowerReader.TrayRunning(instance))
        {
            return true;
        }
        instance.Tower("exit");
        if (!Until(() => !TowerReader.TrayRunning(instance), 20000))
        {
            instance.EndTrays();
        }
        return false;
    }

    // ---- Phase 4: the rollback ----------------------------------------------------------------------------------------------------------

    private void RunRollbackPhase()
    {
        var candidate = reference["candidate"];
        var installation = new Installation(instance.Layout.Install);
        var candidateHash = installation.Contract(candidateVersion).Files!["bell-tower-ingress.exe"];
        Until(Drained, 60000);
        // Stop the candidate tray and select 0.1.9, but start no tray yet: the first reads must be 0.1.9 reading what the candidate wrote, before any 0.1.9 tray pass.
        var back = InstallSwitch.Select(instance, baselineVersion, candidateVersion, "rollback-" + ++switches, startTray: false);
        var stillCandidate = models.Select(model => IngressImage.HashOf(CandidateInstall.Command(instance, model, "SessionStart"))).All(hash => string.Equals(hash, candidateHash, StringComparison.OrdinalIgnoreCase));
        Check("rollback/selected", back.Passed && back.Reported == baselineVersion && stillCandidate && AllAlive(),
            "Rolled back to the real " + baselineVersion + " by the manual selection of its retained payload (the product's capture-compatibility check passed; tray stopped: " + back.Stopped + ", pointer rewritten: " + back.Selected + ", launcher reports " + back.Reported + "); " +
            "the provider hooks still name the candidate's ingress image " + IngressImage.Short(candidateHash) + " (" + stillCandidate + "); " + AliveCount() + " agent sessions untouched; user data was not rolled back.");

        // 0.1.9 reads what the candidate wrote (its own commands, its own policy; no candidate type judges it).
        var rows = TowerReader.List(instance).ToDictionary(row => row.Key);
        var stores = TowerReader.IdentityStores(instance);
        var readBack = new List<string>();
        foreach (var conv in conversations)
        {
            if (!rows.TryGetValue(conv.Key, out var row))
            {
                readBack.Add(conv.Key + " missing from 0.1.9 list");
                continue;
            }
            var was = candidate[conv.Key];
            if (TowerReader.IdentityView(row.Identity).ToJsonString() != was.Identity)
            {
                readBack.Add(conv.Key + " identity fields differ");
            }
            if (row.Name != was.Name)
            {
                readBack.Add(conv.Key + " name '" + row.Name + "' (candidate '" + was.Name + "')");
            }
            if (row.CheckoutSource != was.Source || row.Checkout != was.Checkout)
            {
                readBack.Add(conv.Key + " checkout " + row.CheckoutSource + " " + row.Checkout + " (candidate " + was.Source + " " + was.Checkout + ")");
            }
        }
        var doctor = instance.Tower("doctor");
        var badges = new List<string>();
        foreach (var conv in conversations.Where(conv => rows.ContainsKey(conv.Key)))
        {
            var input = JsonSerializer.Serialize(new { session_id = conv.Id, cwd = main });
            var badge = instance.Run(instance.Layout.Launcher, ["statusline", "--provider", conv.Model.Name, "--no-color", "--data-dir", Data], stdin: input);
            if (!badge.Succeeded || !badge.Output.Contains(rows[conv.Key].Name, StringComparison.Ordinal))
            {
                badges.Add(conv.Key);
            }
        }
        var work = Role("work").ToList();
        var checkoutWork = work.All(conv => stores.TryGetValue(conv.Key, out var store) && TowerReader.Text(store, "checkoutWork") == conv.Change);
        Check("rollback/reads-candidate-identity", readBack.Count == 0 && doctor.Succeeded && badges.Count == 0 && work.All(conv => rows.TryGetValue(conv.Key, out var row) && row.CheckoutSource == "retained") && checkoutWork,
            "0.1.9's own commands read the candidate's data before any 0.1.9 tray pass: `list` shows all " + rows.Count + " conversations with identical stored identity fields, names, colors, pins, checkouts and sources; the retained conversations list checkoutSource retained and keep the checkoutWork field on disk (" + checkoutWork +
            "); `doctor` exit " + doctor.ExitCode + "; the installed 0.1.9 `statusline` carries each conversation's name" + (readBack.Count + badges.Count == 0 ? "." : "; differences: " + Ids(readBack.Concat(badges.Select(key => key + " badge"))) + "."));

        // Hooks while the tray is down under 0.1.9 (the user's Exit is still recorded, so no hook may start a tray).
        var userStop = File.Exists(HookTrayStart.UserStopRecord(Data));
        var segment = new Segment();
        var downIds = new List<string>();
        for (var round = 0; round < 3; round++)
        {
            foreach (var conv in Waiters())
            {
                var id = "toolu_rbdown_" + round;
                if (!downIds.Contains(id))
                {
                    downIds.Add(id);
                }
                Tool(segment, conv, "PostToolUse", "rbdown-" + round, id);
            }
        }
        var spooled = SpoolInjection.Pending(Inbox).Count;
        var stayedDown = TrayStayedDown();
        var noStart = HookTrayStart.LastAttempt(Data) is not { } attempt || attempt.Outcome != nameof(HookStartOutcome.Started);
        instance.Tower("start", "--silent");
        var running = Until(() => TowerReader.TrayRunning(instance), 40000);
        var versions = InstallSwitch.RunningVersions(instance);
        var drained = Until(Drained, 90000);
        Until(() => Waiters().Where(conv => !conv.Kind.HooksLostAcrossUpgrade && !conv.Kind.HooksLostAcrossRollback).All(conv => downIds.All(id => State(conv).Completed.Contains(id))), 60000);
        Check("rollback/tray-and-delivery", userStop && segment.AllAccepted && spooled == segment.Fired && stayedDown && noStart && running && versions.Count > 0 && versions.All(version => version == baselineVersion) && drained && AllAlive(),
            "With the tray down under the selected 0.1.9 (user stop recorded: " + userStop + "), " + segment.Fired + " hooks through the candidate-image commands were accepted (" + segment.Summary + "), all " + spooled + " captures waited in the spool and no hook started a tray (" + (stayedDown && noStart) + "). " +
            "The restarted tray is " + Ids(versions.Distinct()) + " (only " + baselineVersion + " processes) and drained the candidate-written spool (" + drained + "); " + AliveCount() + " agent sessions still running.");
        // Wait for the events to reach the journal (applied or not): a dropped event costs no timeout.
        Until(() => Waiters().All(conv => downIds.All(id => State(conv).JournalOperationIds.Contains(id))), 60000);
        foreach (var conv in Waiters())
        {
            var expectLoss = conv.Kind.HooksLostAcrossUpgrade || conv.Kind.HooksLostAcrossRollback;
            var ok = downIds.All(id => State(conv).Completed.Contains(id));
            var mechanism = expectLoss && downIds.All(id => LossPredicates.IdLoss(State(conv), id, ExpectedLoss(conv, true).Session, ExpectedLoss(conv, true).Event));
            context.Check(scenario, group + "/rollback/candidate-hooks-applied-by-0.1.9/" + conv.Role, conv.Model.Name, expectLoss ? mechanism : ok,
                (expectLoss ? (conv.Kind.HooksLostAcrossUpgrade ? LimitationText : OwnedRuntimeLimitationText) + " " : "") + "Post-only hooks written by the candidate's ingress while 0.1.9 was selected, for a " + Diagnosis(conv) + ": " + (ok ? "applied by the 0.1.9 tray" : "NOT applied by the 0.1.9 tray (health " +
                TowerReader.Text(StateView.Attachment(instance, conv.Key), "health") + ", ownership " + TowerReader.Text(StateView.Attachment(instance, conv.Key), "ownership") + ")") +
                (expectLoss ? (mechanism ? ": every event reached the journal and was ignored for the documented reason (the session and the probe events carry exactly the documented, differing attachment ids)." : ": UNEXPECTED, the documented loss did not occur by its mechanism; revisit the limitation.") : "."), expectedFailure: expectLoss);
        }

        // The faults again, now consumed by 0.1.9.
        var faults = Role("fault").ToList();
        var base0 = faults.ToDictionary(conv => conv.Key, conv => State(conv).Journal.Count);
        var faultSegment = new Segment();
        foreach (var conv in faults)
        {
            Tool(faultSegment, conv, "PostToolUse", "c-lost-1", "toolu_c_1");
            Tool(faultSegment, conv, "PostToolUse", "c-late-2", "toolu_c_2");
            Tool(faultSegment, conv, "PreToolUse", "c-late-2", "toolu_c_2");
            Tool(faultSegment, conv, "PermissionRequest", "c-late-2");
            Tool(faultSegment, conv, "PostToolUse", "c-lost-1", "toolu_c_1");
        }
        var consumed = Until(() => faults.All(conv => State(conv).Journal.Count >= base0[conv.Key] + 5), 90000);
        var faultsHold = faults.All(conv => State(conv) is var state && state.Waits == 1 && state.Operations.SetEquals(["toolu_s_2"]) && state.Completed.Contains("toolu_c_1") && state.Completed.Contains("toolu_c_2") && state.Completed.Contains("toolu_s_1"));
        Check("rollback/faults-under-0.1.9", faultSegment.AllAccepted && consumed && faultsHold,
            "0.1.9 consumed the candidate's lost-pre, late-pre and duplicate hooks: " + faultSegment.Summary + "; each fault conversation still has exactly its one pending wait, no wait or in-flight operation appeared for the late or lost operations and the earlier resolved operations did not return.");
        var waitsStable = Waiters().All(conv => State(conv).Waits == 1 && State(conv).Operations.SetEquals(["toolu_pending_a"]) && State(conv).Completed.Contains("toolu_resolved_a"));
        Check("rollback/no-resurrected-waits", waitsStable && faults.All(conv => State(conv).Waits == 1), "Across the rollback each wait conversation keeps its one pending wait and no resolved wait came back (" + Ids(Waiters().Concat(faults).Select(conv => conv.Key + " waits " + State(conv).Waits)) + ").");

        // Liveness as 0.1.9 judges it: the same verdicts the candidate gave.
        bool Verdict(Conv conv) => conv.Role == "ghost" || conv.Model.UnclassifiedStartupFailsClosed
            ? Row(conv) is { AttachmentHealth: "unavailable", Activity: "unavailable" }
            : Row(conv)?.AttachmentHealth == "verified-live";
        var live = Until(() => Role("heal").Concat(Role("ghost")).All(Verdict), 90000);
        Check("rollback/liveness", live && AllAlive(),
            "0.1.9 reads liveness as the candidate did (repaired conversations verified live, process-less and fail-closed ones unavailable, never working): " + Ids(Role("heal").Concat(Role("ghost")).Select(conv => conv.Key + " " + (Row(conv)?.AttachmentHealth ?? "unread") + "/" + (Row(conv)?.Activity ?? "unread"))) + ".");

        // The expected one-way effect on retained checkouts (ANALYSIS-186): recorded, not a defect.
        var released = Until(() => work.All(conv => Row(conv) is { CheckoutSource: not "retained" }), 120000);
        var after = TowerReader.IdentityStores(instance);
        var preserved = work.All(conv => after.TryGetValue(conv.Key, out var store) && TowerReader.Text(store, "checkoutWork") == conv.Change);
        var sources = work.Select(conv => conv.Key + " " + (Row(conv)?.CheckoutSource ?? "unread") + " " + (Row(conv)?.Checkout == main ? "launch checkout" : Row(conv)?.Checkout == conv.Worktree ? "own worktree" : "other"));
        Check("rollback/retained-checkout-effects", released && preserved && work.All(conv => Row(conv)?.CheckoutSource == "launch" && Row(conv)?.Checkout == main),
            "EXPECTED RESULT (ANALYSIS-186, no product change): read straight after the rollback the retained source was intact; after 0.1.9's own passes it records the launch checkout (0.1.9 has no retained input): " + Ids(sources) +
            "; the additive checkoutWork field survived 0.1.9's rewrite of the identity store (" + preserved + "). Upgrading again does not restore the retained checkout (checked after the second upgrade).");
    }

    // Rerunning 0.1.9's own setup after the rollback points the hooks back at the 0.1.9 ingress image: delivery must be whole again for every launch kind.
    private void RerunBaselineSetupAfterRollback()
    {
        var before = Waiters().ToDictionary(conv => conv.Key, conv => State(conv).Completed.Contains("toolu_rbdown_0"));
        var rerun = instance.TowerWithHome(300000, "setup", "--clients", "both", "--apply");
        var named = models.ToDictionary(model => model.Name, model => IngressImage.HashOf(CandidateInstall.Command(instance, model, "SessionStart")));
        var restored = models.All(model => named[model.Name] is { } hash && string.Equals(hash, baselineImages[model.Name], StringComparison.OrdinalIgnoreCase));
        var segment = new Segment();
        var applied = Probe(segment, "rr_1", Waiters());
        Check("rollback/setup-rerun-points-hooks-at-0.1.9-image", rerun.Succeeded && restored && TowerReader.TrayRunning(instance),
            "0.1.9's own `setup --clients both --apply` (0.1.9 has no --host-bridge; exit " + rerun.ExitCode + (rerun.Succeeded ? "" : ": " + rerun.Error.Trim()) + ") rewrote the owned hooks to the 0.1.9 ingress image " + Ids(models.Select(model => model.Name + " " + IngressImage.Short(named[model.Name]))) + "; hooks run again as in the baseline phase (Codex through the modeled cmd fallback).");
        foreach (var conv in Waiters())
        {
            Check("rollback/setup-rerun-restores-delivery/" + conv.Role, segment.AllAccepted && applied[conv.Key],
                "After the 0.1.9 setup rerun a hook of a " + Diagnosis(conv) + " was " + (applied[conv.Key] ? "applied by the 0.1.9 tray" : "NOT applied by the 0.1.9 tray") + (before[conv.Key] ? " (it was also applied before the rerun)." : " (it had been dropped before the rerun)."), conv.Model.Name);
        }
    }

    // ---- Phase 5: the second upgrade ----------------------------------------------------------------------------------------------------

    private void RunReupgradePhase()
    {
        var candidate = reference["candidate"];
        var installation = new Installation(instance.Layout.Install);
        Until(Drained, 60000);
        var again = InstallSwitch.Select(instance, candidateVersion, baselineVersion, "reupgrade-" + ++switches);
        var versions = InstallSwitch.RunningVersions(instance);
        Check("reupgrade/selected", again.Passed && again.Reported == candidateVersion && versions.Count > 0 && versions.All(version => version == candidateVersion) && AllAlive(),
            "Selected the retained candidate payload again (the same manual selection, tray restarted: " + again.TrayStarted + "; launcher reports " + again.Reported + "; running tray " + Ids(versions.Distinct()) + "); " + AliveCount() + " agent sessions untouched.");

        var refresh = instance.TowerWithHome(300000, "setup", "--host-bridge", "--apply");
        var imageHash = installation.Contract(candidateVersion).Files!["bell-tower-ingress.exe"];
        Check("reupgrade/setup-rerun-points-hooks-at-candidate-image", refresh.Succeeded && models.All(model => string.Equals(IngressImage.HashOf(CandidateInstall.Command(instance, model, "SessionStart")), imageHash, StringComparison.OrdinalIgnoreCase)),
            "The candidate's `setup --host-bridge --apply` after the second upgrade (exit " + refresh.ExitCode + ") pointed the owned hooks at the candidate ingress image " + IngressImage.Short(imageHash) + " again.");
        ProbeCandidateAfterSetup("reupgrade", "ref_2");
        var settled = Until(() => Waiters().Concat(Role("fault")).All(conv => State(conv).Waits == 1), 60000);
        var segment = new Segment();
        var faults = Role("fault").ToList();
        var base0 = faults.ToDictionary(conv => conv.Key, conv => State(conv).Journal.Count);
        foreach (var conv in faults)
        {
            Tool(segment, conv, "PostToolUse", "f-lost-1", "toolu_f_1");
            Tool(segment, conv, "PostToolUse", "f-late-2", "toolu_f_2");
            Tool(segment, conv, "PermissionRequest", "f-late-2");
        }
        var consumed = Until(() => faults.All(conv => State(conv).Journal.Count >= base0[conv.Key] + 3), 90000);
        var holds = faults.All(conv => State(conv) is var state && state.Waits == 1 && state.Operations.SetEquals(["toolu_s_2"]) && state.Completed.Contains("toolu_f_1") && state.Completed.Contains("toolu_f_2"));
        Check("reupgrade/faults-and-waits", settled && segment.AllAccepted && consumed && holds && Waiters().All(conv => State(conv).Waits == 1 && State(conv).Operations.SetEquals(["toolu_pending_a"])) && Drained(),
            "After the second upgrade the candidate consumed the lost and late hooks (" + segment.Summary + ") and every conversation still has exactly its one pending wait and no resurrected resolved wait; the spool is drained.");

        var now = TowerReader.List(instance).ToDictionary(row => row.Key);
        var differences = conversations.Where(conv => now.ContainsKey(conv.Key) && !conv.Role.Equals("work") &&
            TowerReader.IdentityView(now[conv.Key].Identity).ToJsonString() != candidate[conv.Key].Identity).Select(conv => conv.Key).ToList();
        var work = Role("work").ToList();
        var launchKept = Until(() => work.All(conv => Row(conv) is { CheckoutSource: "launch" }), 60000) && work.All(conv => Row(conv)?.Checkout == main);
        Check("reupgrade/identity-and-retained-effects", differences.Count == 0 && launchKept && now.Count >= conversations.Count,
            "The names, colors, pins and checkouts every conversation had before the rollback read back unchanged" + (differences.Count == 0 ? "" : " (differences: " + Ids(differences) + ")") + ". EXPECTED RESULT (ANALYSIS-186, no product change): the retained checkout was not restored by upgrading again; the work conversations stay on the launch checkout that 0.1.9 recorded (" +
            Ids(work.Select(conv => conv.Key + " " + (Row(conv)?.CheckoutSource ?? "unread"))) + ") until new work or a pin; sessions 0.1.9 already moved to the launch checkout before an upgrade have lost that evidence the same way.");
    }

    // ---- The end: stable state, no termination, no exposure --------------------------------------------------------------------------------

    private void RunFinalChecks()
    {
        // Option (a) of ANALYSIS-261: wait deterministically until every conversation's last hook is older than the attachment grace plus a margin (no clock-driven
        // verdict can still change), then compare once. Nothing is retried, so a periodic instability cannot be masked.
        var quiet = StableState.RemainingQuiet(lastHookAt.Values, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(15), DateTime.UtcNow);
        if (quiet > TimeSpan.Zero)
        {
            Thread.Sleep(quiet);
        }
        var first = conversations.ToDictionary(conv => conv.Key, conv => State(conv).Fingerprint + "|" + (Row(conv)?.AttachmentHealth ?? ""));
        Thread.Sleep(3000);
        var second = conversations.ToDictionary(conv => conv.Key, conv => State(conv).Fingerprint + "|" + (Row(conv)?.AttachmentHealth ?? ""));
        var changed = conversations.Where(conv => first[conv.Key] != second[conv.Key]).Select(conv => conv.Key).ToList();
        var running = TowerReader.TrayRunning(instance);
        Check("final/stable-state", changed.Count == 0 && running && Drained(),
            "After waiting " + quiet.TotalSeconds.ToString("F0") + " s so that every conversation's last hook is past the attachment grace, two reads three seconds apart agree (one comparison, no retry) for all " + conversations.Count + " conversations (waits, operations, completed operations, journal size, liveness verdict) with the candidate tray running and the spool drained" + (changed.Count == 0 ? "." : "; changed: " + Ids(changed) + "."));
        Check("final/no-process-termination", AllAlive(),
            "Every fake agent session started for this group (" + conversations.SelectMany(conv => conv.Sessions).Count() + " processes, including the process-less and repaired ones) was still running after the upgrade, the tray downtime, the rollback and the second upgrade; the harness ended only trays, and only through the user's Exit.");
        var visible = instance.WindowGuard.VisibleNow();
        Check("final/no-visible-sandbox-ui", visible.Count == 0,
            "No window of a sandbox Bell Tower process was visible on the desktop at the end (" + (visible.Count == 0 ? "none" : string.Join("; ", visible)) + "). The product has no setting that turns the notification strip off (`--silent` and `muted` silence audio only), so the harness hid " +
            instance.WindowGuard.HiddenCount + " window show(s) of its own sandbox trays within 100 ms each (a window owned by a process under this instance's install directory, never the user's own trays).");
        var lastAttempt = HookTrayStart.LastAttempt(Data);
        Check("final/real-environment-exposure", !instance.Exposure.HookStartedTrayUsed && instance.Exposure.SharedBackendCodexUsed == conversations.Any(conv => conv.Model.UsesSharedBackend(conv.Kind.Arguments)) && (lastAttempt is not { } attempt || attempt.Outcome != nameof(HookStartOutcome.Started)),
            "No hook started a tray (every tray-down phase followed the user's Exit)" + (instance.Exposure.SharedBackendCodexUsed ? "; a modeled shared-backend Codex (a fake app-server agent, no real backend) ran, which is allowed only while no hook starts a tray in the same instance, and none did" : "; no shared-backend Codex ran") +
            ", so no tray reached the real profile environment (last start attempt " + (lastAttempt is { } last ? last.Outcome : "none") + ").");
    }

    // ---- Reads for comparison -------------------------------------------------------------------------------------------------------------

    private Dictionary<string, View> Snapshot()
    {
        var rows = TowerReader.List(instance).ToDictionary(row => row.Key);
        var stores = TowerReader.IdentityStores(instance);
        var view = new Dictionary<string, View>();
        foreach (var conv in conversations)
        {
            if (rows.TryGetValue(conv.Key, out var row))
            {
                view[conv.Key] = new(State(conv).Fingerprint, TowerReader.IdentityView(row.Identity).ToJsonString(), row.CheckoutSource, row.Checkout,
                    stores.TryGetValue(conv.Key, out var store) ? TowerReader.Text(store, "checkoutWork") : "", row.Name, row.AttachmentHealth, row.Activity);
            }
        }
        return view;
    }
}
