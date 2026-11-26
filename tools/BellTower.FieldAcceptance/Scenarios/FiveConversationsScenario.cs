using System.Text.RegularExpressions;
using BellTower.Core;
using BellTower.FieldAcceptance.Evidence;
using BellTower.FieldAcceptance.Harness;

namespace BellTower.FieldAcceptance.Scenarios;

/// <summary>
/// Task 2.2 and ledger rows F7, F8, F9, B and G3. Per provider, and once with both providers together, five conversations launch in one
/// main checkout (a real Git repository in the sandbox) while their work lives in ticket worktrees with branches and OpenSpec change
/// folders. They differ by history work, pinned change, manual name, role argument and native title, and by palette control (named, hex,
/// random and reset). The scenario drives the installed candidate through its hooks and commands and reads the badge (the installed
/// <c>statusline</c>), the tab title, the chip inputs and the spoken phrase for every conversation, then repeats the reads after a resume
/// of the same conversations, after read-only inspection of another change, and after a branch change in four of the five worktrees.
/// The expected values are literal consequences of the shared naming policy in the release handoff; nothing here computes a name.
/// </summary>
public sealed class FiveConversationsScenario : IScenario
{
    public string Id => "five-conversations";
    public string Title => "Five conversations per provider and mixed in one main launch checkout: identity, palette and surfaces through resume, inspection and branch changes";

    private static readonly string[] Roles = ["work", "pinned", "manual", "role", "reset"];
    private static readonly Regex TicketNumber = new(@"DEP-(\d+)", RegexOptions.CultureInvariant);

    public void Run(RunContext context)
    {
        RunGroup(context, "claude", [ProviderModel.Claude], 7100);
        RunGroup(context, "codex", [ProviderModel.Codex], 7200);
        RunGroup(context, "mixed", [ProviderModel.Claude, ProviderModel.Codex], 7300);
        AddNotes(context);
    }

    private sealed class Conversation(ProviderModel model, string role, int number)
    {
        public ProviderModel Model { get; } = model;
        public string Role { get; } = role;
        public int Number { get; } = number;
        public string Ticket => "DEP-" + Number;
        public string SessionId => Model.Name + "-" + Role;
        public string Key => Model.Name + ":" + SessionId;
        // OpenSpec change names are lowercase kebab-case; the ticket in them still matches the worktree and branch ticket.
        public string Change => Ticket.ToLowerInvariant() + "-login";
        public string Pinned => Ticket.ToLowerInvariant() + "-pinned";
        public string Branch => Ticket + "-feat";
        public string Hotfix => Ticket + "-hotfix";
        public string ManualName => Role == "manual" ? "Release notes " + Ticket : "Reset target " + Ticket;
        public string NativeTitle => "Login flow review " + Ticket;
        public string Worktree { get; set; } = "";
        public AgentSession Session { get; set; } = null!;
        // The conversation whose change this one inspects read-only.
        public Conversation? Inspected { get; set; }

        /// <summary>Whether the provider's explicit native title can be modeled for this conversation (see the scenario notes for Codex).</summary>
        public bool HasNativeTitle => Role == "reset" && Model.Name == ProviderNames.Claude;

        /// <summary>The name the shared naming policy gives after the palette controls ran, by the fixture design.</summary>
        public string ExpectedName => Role switch
        {
            "pinned" => Pinned,
            "manual" => ManualName,
            "reset" => HasNativeTitle ? NativeTitle : Change,
            _ => Change
        };

        public bool BranchChanged => Role != "role";

        /// <summary>The persisted checkout source token after the branch change: the work was cleared so the checkout is retained, except for the pinned change, which still verifies the worktree (no label flip).</summary>
        public string ExpectedSource => Role == "pinned" ? "unique-work" : "retained";
    }

    private void RunGroup(RunContext context, string group, ProviderModel[] models, int firstNumber)
    {
        var label = models.Length == 1 ? models[0].Name : ProviderNames.Mixed;
        void Check(string id, bool passed, string detail, string? provider = null) =>
            context.Check(Id, group + "/" + id, provider ?? label, passed, detail);

        var instance = CandidateInstall.Prepare(context, Id, "conv-" + group);
        var work = Path.Combine(instance.Layout.Root, "work");
        var main = Path.Combine(work, "D360");
        SandboxGit.InitRepository(instance, main);

        // ---- Fixtures: ticket worktrees with branches and change folders, and modeled transcripts with historical work -----------------
        var conversations = new List<Conversation>();
        var number = firstNumber;
        foreach (var model in models)
        {
            foreach (var role in Roles)
            {
                conversations.Add(new Conversation(model, role, number++));
            }
            number = firstNumber + 50;
        }
        var launchedAt = DateTimeOffset.UtcNow;
        foreach (var conversation in conversations)
        {
            conversation.Worktree = Path.Combine(work, "D360-worktrees", conversation.Ticket);
            ConversationFixture.BuildWorktree(instance, main, conversation.Worktree, conversation.Branch, conversation.Change, conversation.Pinned);
            WriteTranscript(instance, conversation, launchedAt.AddMinutes(-10));
        }
        Check("fixture", conversations.All(c => Directory.Exists(c.Worktree)) && conversations.Select(c => c.Worktree).Distinct().Count() == conversations.Count,
            conversations.Count + " conversations (" + string.Join(", ", models.Select(model => model.Name)) + ") share one main launch checkout (a real Git repository with one linked worktree and branch per ticket, and OpenSpec change folders inside each worktree); each has a modeled " +
            "transcript with one successful historical write into its own worktree's change folder" + (conversations.Any(c => c.HasNativeTitle) ? " (Claude's reset conversation also has a custom-title record)" : "") + ".");

        // ---- Start every conversation through the installed hooks, all launched in the main checkout -------------------------------
        foreach (var conversation in conversations)
        {
            conversation.Session = instance.StartSession(conversation.Model.Name, conversation.SessionId, main, "att-" + conversation.Model.Name + "-" + conversation.Role);
        }
        var accepted = 0;
        foreach (var hookEvent in new[] { "SessionStart", "UserPromptSubmit" })
        {
            foreach (var conversation in conversations)
            {
                accepted += Fire(instance, conversation, hookEvent, main) ? 1 : 0;
            }
        }
        var keys = conversations.Select(c => c.Key).ToList();
        TowerReader.WaitForSessions(instance, keys);
        var recovered = TowerReader.WaitFor(() => conversations.All(c => TowerReader.Text(ConversationSurfaces.Identity(instance, c.Key), "activeWork") == c.Change), 120000);
        var recoveredRows = conversations.Select(c => (Conversation: c, Work: TowerReader.Text(ConversationSurfaces.Identity(instance, c.Key), "activeWork"), Checkout: TowerReader.Text(ConversationSurfaces.Identity(instance, c.Key), "workingCheckout"),
            Launch: TowerReader.Text(ConversationSurfaces.Identity(instance, c.Key), "launchDirectory"))).ToList();
        Check("hooks-accepted", accepted == conversations.Count * 2,
            accepted + " of " + conversations.Count * 2 + " SessionStart and UserPromptSubmit hooks of the " + conversations.Count + " conversations were accepted through the installed commands (" + string.Join(", ", models.Select(m => ShellName(m, instance))) + ").");
        Check("history-work-and-checkout", recovered && recoveredRows.All(row => PathsEqual(row.Checkout, row.Conversation.Worktree) && PathsEqual(row.Launch, main)),
            "Each conversation's historical work was recovered from its own transcript (active work: " + string.Join(", ", recoveredRows.Select(row => row.Conversation.Key + "=" + row.Work)) +
            "), the launch directory of all is the one main checkout, and the working checkout of each is its own ticket worktree (" + recoveredRows.Count(row => PathsEqual(row.Checkout, row.Conversation.Worktree)) + " of " + recoveredRows.Count + ").");

        // ---- Controls: pinned change, manual names, role argument, palettes (named, hex, random, reset) ----------------------------
        var before = conversations.ToDictionary(c => c.Key, c => TowerReader.Text(ConversationSurfaces.Identity(instance, c.Key), "palette", "accent"));
        var controls = new List<string>();
        foreach (var conversation in conversations)
        {
            switch (conversation.Role)
            {
                case "pinned":
                    controls.Add(Tower(instance, "rename", "--session", conversation.Key, "--change", conversation.Pinned));
                    controls.Add(Tower(instance, "rename", "--session", conversation.Key, "--color", "blue"));
                    break;
                case "manual":
                    controls.Add(Tower(instance, "rename", "--session", conversation.Key, "--name", conversation.ManualName));
                    controls.Add(Tower(instance, "rename", "--session", conversation.Key, "--color", "#8a3ffc"));
                    break;
                case "role":
                    controls.Add(Tower(instance, "rename", "--session", conversation.Key, "--role", "Reviewer"));
                    controls.Add(Tower(instance, "rename", "--session", conversation.Key, "--color", "random"));
                    break;
                case "reset":
                    controls.Add(Tower(instance, "rename", "--session", conversation.Key, "--name", conversation.ManualName));
                    controls.Add(Tower(instance, "rename", "--session", conversation.Key, "--color", "#336699"));
                    break;
            }
        }
        var resetting = conversations.Where(c => c.Role == "reset").ToList();
        var preReset = resetting.ToDictionary(c => c.Key, c => (Name: ConversationSurfaces.Identity(instance, c.Key)["name"]!.GetValue<string>(), Pin: TowerReader.Text(ConversationSurfaces.Identity(instance, c.Key), "colorPin")));
        foreach (var conversation in resetting)
        {
            controls.Add(Tower(instance, "rename", "--session", conversation.Key, "--reset"));
        }
        var snapshot1 = ConversationSurfaces.ReadSettled(instance, keys, main, context.Report.Candidate!.Version);
        var complete = snapshot1.Count == keys.Count;
        var paletteDetail = "Not every conversation had a current presentation snapshot.";
        var paletteOk = complete && PaletteControlsHold(conversations, snapshot1, before, preReset, out paletteDetail);
        Check("palette-controls", paletteOk, paletteDetail);
        Check("names", complete && conversations.All(c => snapshot1[c.Key].Name == c.ExpectedName),
            "Names by the shared precedence (explicit title, then native title, then pinned change, then work association): " + string.Join("; ", conversations.Select(c => c.Key + " " + Describe(c, snapshot1))) + ".");
        Check("native-and-reset", complete && resetting.All(c => preReset[c.Key].Name == c.ManualName && preReset[c.Key].Pin == "#336699" && snapshot1[c.Key].ManualName == "" && snapshot1[c.Key].ColorPin == "" &&
            (!c.HasNativeTitle || snapshot1[c.Key].NativeTitle == c.NativeTitle)),
            "Reset clears the Bell Tower pins (manual name, color pin) and re-resolves: " + string.Join("; ", resetting.Select(c => c.Key + " was '" + preReset[c.Key].Name + "' with pin " + preReset[c.Key].Pin + ", now '" + snapshot1[c.Key].Name +
            "', manual name '" + snapshot1[c.Key].ManualName + "', native title '" + snapshot1[c.Key].NativeTitle + "', color pin '" + snapshot1[c.Key].ColorPin + "'")) +
            (resetting.Any(c => c.HasNativeTitle) ? ". The Claude custom title survives the reset (the transcript is not rewritten)." : "."));
        // ANALYSIS-164 (user decision 2026-11-25): Codex explicit native names stay an open parity gap for 0.1.11. It is reported as its own
        // outcome, never as a passing assertion that pins the gap as expected behavior.
        foreach (var codex in resetting.Where(c => c.Model.Name == ProviderNames.Codex && complete))
        {
            context.Unfinished(Id, group + "/native-title-tier", ProviderNames.Codex,
                "UNFINISHED PARITY, not performed: the Codex explicit native-title tier (tier 2 of the shared precedence) has no read side, so " + codex.Key + " cannot show a native-title name; the reset conversation resolved to '" + snapshot1[codex.Key].Name +
                "' (native title '" + snapshot1[codex.Key].NativeTitle + "'). Claude's custom-title tier is verified above. Closing evidence: a supported Codex read of a name set outside Bell Tower with name provenance, or a version-scoped probe proving none exists (ledger row F7).");
        }
        VerifySurfaces(context, group, label, conversations, snapshot1, "initial");

        // ---- Resume the same conversations ---------------------------------------------------------------------------------------------
        var resumeAccepted = conversations.Count(c => Fire(instance, c, "SessionStart", main, source: "resume"));
        var resumed = ConversationSurfaces.ReadSettled(instance, keys, main, context.Report.Candidate!.Version);
        Check("resume-same-conversation", resumeAccepted == conversations.Count && resumed.Count == keys.Count && keys.All(key => resumed[key].Fingerprint == snapshot1[key].Fingerprint) &&
            TowerReader.List(instance).Count(row => keys.Contains(row.Key)) == keys.Count,
            "A SessionStart with source resume for each of the same " + conversations.Count + " conversations (same session ids and attachments) was accepted (" + resumeAccepted + " of " + conversations.Count + "); the list still holds exactly these conversations and " +
            keys.Count(key => resumed.TryGetValue(key, out var after) && after.Fingerprint == snapshot1[key].Fingerprint) + " of " + keys.Count + " read back with identical badge, tab, chip inputs, speech, palette and association.");

        // ---- Inspect another change read-only (status, then instructions) --------------------------------------------------------------
        for (var index = 0; index < conversations.Count; index++)
        {
            conversations[index].Inspected = conversations[(index + 1) % conversations.Count];
        }
        var inspectionAccepted = 0;
        foreach (var conversation in conversations)
        {
            foreach (var command in new[] { "openspec status --change " + conversation.Inspected!.Change + " --json", "openspec instructions apply --change " + conversation.Inspected.Change + " --json" })
            {
                inspectionAccepted += FireShell(instance, conversation, main, command) ? 1 : 0;
            }
        }
        var afterInspection = ConversationSurfaces.ReadSettled(instance, keys, main, context.Report.Candidate!.Version);
        Check("inspection-keeps-association", inspectionAccepted == conversations.Count * 2 && afterInspection.Count == keys.Count && keys.All(key => afterInspection[key].Fingerprint == resumed[key].Fingerprint),
            "Each conversation ran read-only openspec status and instructions for another conversation's change through the installed PostToolUse hook (" + inspectionAccepted + " of " + conversations.Count * 2 +
            " accepted; modeled payloads, no native capture exists); " + keys.Count(key => afterInspection.TryGetValue(key, out var after) && after.Fingerprint == resumed[key].Fingerprint) + " of " + keys.Count +
            " conversations kept the same name, work association, checkout and surfaces, so inspecting change B never renamed a conversation working on A.");

        // ---- Branch change in four of the five worktrees -------------------------------------------------------------------------------
        var switched = conversations.Where(c => c.BranchChanged).ToList();
        foreach (var conversation in switched)
        {
            SandboxGit.SwitchNewBranch(instance, conversation.Worktree, conversation.Hotfix);
        }
        var invalidated = TowerReader.WaitFor(() => switched.All(c => TowerReader.Text(ConversationSurfaces.Identity(instance, c.Key), "activeWork") == ""), 120000);
        var afterBranch = ConversationSurfaces.ReadSettled(instance, keys, main, context.Report.Candidate!.Version);
        var untouched = conversations.Where(c => !c.BranchChanged).ToList();
        Check("branch-change-invalidates-automatic-evidence", invalidated && afterBranch.Count == keys.Count && afterInspection.Count == keys.Count && switched.All(c => afterBranch[c.Key].ActiveWork == "" && afterBranch[c.Key].WorkFloorAt != ""),
            "After git switch -c in " + switched.Count + " worktrees the automatic work association of each was invalidated (active work cleared, the branch-change floor recorded): " +
            string.Join("; ", switched.Select(c => c.Key + " name " + (afterBranch.TryGetValue(c.Key, out var after) && afterInspection.ContainsKey(c.Key) ? "'" + after.Name + "' (was '" + NameOf(afterInspection, c) + "')" : "unread"))) + ".");
        var listedSources = TowerReader.List(instance).ToDictionary(row => row.Key, row => row.CheckoutSource);
        Check("branch-change-keeps-working-checkout", afterBranch.Count == keys.Count && switched.All(c => PathsEqual(afterBranch[c.Key].Checkout, c.Worktree) && afterBranch[c.Key].Branch == c.Hotfix && afterBranch[c.Key].Worktree == c.Ticket &&
                listedSources.GetValueOrDefault(c.Key) == c.ExpectedSource),
            "After the branch change each of the " + switched.Count + " conversations kept its own verified ticket worktree as the working checkout and shows the refreshed context (branch, worktree): " +
            string.Join("; ", switched.Select(c => c.Key + " " + (afterBranch.TryGetValue(c.Key, out var after) ? (PathsEqual(after.Checkout, c.Worktree) ? "own worktree" : "NOT its own worktree") + ", branch '" + after.Branch + "' (expected '" + c.Hotfix + "'), worktree '" + after.Worktree + "', list checkoutSource '" + listedSources.GetValueOrDefault(c.Key) + "' (expected '" + c.ExpectedSource + "', a data token the tray maps to display text)" : "unread"))) +
            "; the launch directory of all is still the one main checkout.");
        Check("branch-change-keeps-explicit-state", afterBranch.Count == keys.Count && afterInspection.Count == keys.Count && switched.All(c => KeptAfterBranchChange(c, afterInspection[c.Key], afterBranch[c.Key])),
            "Explicit state survived the branch change: " + string.Join("; ", switched.Where(c => afterBranch.ContainsKey(c.Key) && afterInspection.ContainsKey(c.Key)).Select(c => c.Key + " " + KeptDescription(c, afterInspection[c.Key], afterBranch[c.Key]))) +
            ". Session colors and pins are unchanged for all; the pinned change, manual name and Claude custom title keep their names.");
        Check("branch-change-independent", afterBranch.Count == keys.Count && untouched.All(c => afterBranch[c.Key].Fingerprint == afterInspection[c.Key].Fingerprint),
            "The " + untouched.Count + " conversation(s) whose worktree stayed on its branch (" + string.Join(", ", untouched.Select(c => c.Key)) + ") read back unchanged: another conversation's branch change did not touch their name, color, association or surfaces.");
        VerifySurfaces(context, group, label, conversations, afterBranch, "after-branch-change");

        // Invalidated evidence must not come back when the conversation is resumed and its history is read again.
        var resumeAfter = conversations.Count(c => Fire(instance, c, "SessionStart", main, source: "resume"));
        Thread.Sleep(8000);
        var final = ConversationSurfaces.ReadSettled(instance, keys, main, context.Report.Candidate!.Version);
        Check("branch-change-no-resurrection", resumeAfter == conversations.Count && final.Count == keys.Count && switched.All(c => final[c.Key].ActiveWork == "" && final[c.Key].WorkFloorAt == afterBranch[c.Key].WorkFloorAt) &&
            keys.All(key => final[key].Fingerprint == afterBranch[key].Fingerprint),
            "After a resume of every conversation and another history pass the invalidated work did not return (floor unchanged) and all " + keys.Count + " conversations read back as before.");
    }

    private static string NameOf(IReadOnlyDictionary<string, SurfaceSnapshot> snapshots, Conversation conversation) => snapshots[conversation.Key].Name;

    // What the branch change must leave alone, by role: every conversation keeps its color; the pinned change, manual name and native title keep the name.
    private static bool KeptAfterBranchChange(Conversation conversation, SurfaceSnapshot before, SurfaceSnapshot after)
    {
        var same = before.Accent == after.Accent && before.ColorPin == after.ColorPin && before.Change == after.Change && before.ManualName == after.ManualName && before.NativeTitle == after.NativeTitle;
        return conversation.Role switch
        {
            "pinned" or "manual" => same && before.Name == after.Name,
            "reset" => same && (!conversation.HasNativeTitle || before.Name == after.Name),
            _ => same
        };
    }

    private static string KeptDescription(Conversation conversation, SurfaceSnapshot before, SurfaceSnapshot after) =>
        conversation.Role switch
        {
            "pinned" => "pinned change '" + after.Change + "' and name '" + after.Name + "'",
            "manual" => "manual name '" + after.ManualName + "'",
            "reset" => conversation.HasNativeTitle ? "native title '" + after.NativeTitle + "'" : "no explicit state (the reset cleared it); color unchanged",
            _ => "color unchanged"
        } + " (accent " + before.Accent + " to " + after.Accent + ")";

    private static string Describe(Conversation conversation, IReadOnlyDictionary<string, SurfaceSnapshot> snapshots) =>
        snapshots.TryGetValue(conversation.Key, out var snapshot) ? "'" + snapshot.Name + "'" + (snapshot.Name == conversation.ExpectedName ? "" : " (expected '" + conversation.ExpectedName + "')") : "unread";

    // The palette controls, from the product's own palette outputs: a named or hex pin gives exactly that color; random and reset clear the
    // pin and allocate another color of the series; every automatic color is different.
    private static bool PaletteControlsHold(List<Conversation> conversations, IReadOnlyDictionary<string, SurfaceSnapshot> snapshots, Dictionary<string, string> before,
        Dictionary<string, (string Name, string Pin)> preReset, out string detail)
    {
        var results = new List<string>();
        var ok = true;
        var series = Palette.Series.Select(entry => entry.Accent).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var conversation in conversations)
        {
            var current = snapshots[conversation.Key];
            switch (conversation.Role)
            {
                case "pinned":
                    var named = current.ColorPin == "blue" && current.Accent == Palette.FromColor("blue").Accent;
                    ok &= named;
                    results.Add(conversation.Key + " named blue " + (named ? "applied (" + current.Accent + ")" : "NOT applied (pin '" + current.ColorPin + "', accent " + current.Accent + ")"));
                    break;
                case "manual":
                    var hex = current.ColorPin == "#8a3ffc" && current.Accent == "#8a3ffc";
                    ok &= hex;
                    results.Add(conversation.Key + " hex #8a3ffc " + (hex ? "applied" : "NOT applied (pin '" + current.ColorPin + "', accent " + current.Accent + ")"));
                    break;
                case "role":
                    var random = current.ColorPin == "" && series.Contains(current.Accent) && !string.Equals(current.Accent, before[conversation.Key], StringComparison.OrdinalIgnoreCase);
                    ok &= random;
                    results.Add(conversation.Key + " random " + (random ? "moved " + before[conversation.Key] + " to series color " + current.Accent : "NOT applied (pin '" + current.ColorPin + "', accent " + current.Accent + ")"));
                    break;
                case "reset":
                    var reset = current.ColorPin == "" && series.Contains(current.Accent) && !string.Equals(current.Accent, "#336699", StringComparison.OrdinalIgnoreCase) && preReset[conversation.Key].Pin == "#336699";
                    ok &= reset;
                    results.Add(conversation.Key + " color #336699 then reset " + (reset ? "cleared the pin and chose series color " + current.Accent : "NOT cleared (pin '" + current.ColorPin + "', accent " + current.Accent + ")"));
                    break;
                default:
                    results.Add(conversation.Key + " automatic " + current.Accent);
                    break;
            }
        }
        var automatic = conversations.Where(c => snapshots[c.Key].ColorPin == "").Select(c => snapshots[c.Key].Accent).ToList();
        var distinct = automatic.Distinct(StringComparer.OrdinalIgnoreCase).Count() == automatic.Count;
        ok &= distinct;
        detail = "Palette controls through the installed rename command: " + string.Join("; ", results) + ". The " + automatic.Count + " conversations without a pin have " + (distinct ? "pairwise different" : "REPEATED") + " accents.";
        return ok;
    }

    // The badge (the installed statusline output) carries each conversation's own name and context; the other surfaces carry no other
    // conversation's ticket and differ across conversations. Tab, chip details and speech are product functions over the installed state
    // evaluated in-process with the harness build (not installed outputs), and say so in their detail.
    private void VerifySurfaces(RunContext context, string group, string label, List<Conversation> conversations, IReadOnlyDictionary<string, SurfaceSnapshot> snapshots, string phase)
    {
        void Check(string id, bool passed, string detail) => context.Check(Id, group + "/" + phase + "/" + id, label, passed, detail);
        if (snapshots.Count != conversations.Count)
        {
            Check("surfaces-read", false, "Only " + snapshots.Count + " of " + conversations.Count + " conversations had a current presentation snapshot.");
            return;
        }
        var problems = new List<string>();
        foreach (var conversation in conversations)
        {
            var s = snapshots[conversation.Key];
            if (!s.Badge.Contains(s.Name, StringComparison.Ordinal) || s.Branch != "" && !s.Badge.Contains("branch: " + s.Branch, StringComparison.Ordinal) ||
                s.Worktree != "" && !s.Badge.Contains("worktree: " + s.Worktree, StringComparison.Ordinal))
            {
                problems.Add(conversation.Key + " badge '" + s.Badge + "' does not match its name and context");
            }
            var own = TicketNumber.Matches(conversation.Ticket).Select(match => match.Groups[1].Value).ToHashSet();
            foreach (var text in new[] { s.Badge, s.Tab, s.ChipDetails, s.Speech.Replace(' ', '-'), s.Speech })
            {
                foreach (var other in conversations.Where(c => c != conversation))
                {
                    if (!own.Contains(other.Number.ToString()) && text.Contains(other.Number.ToString(), StringComparison.Ordinal))
                    {
                        problems.Add(conversation.Key + " surface mentions " + other.Ticket + ": '" + text + "'");
                    }
                }
            }
        }
        Check("badges-own-and-no-foreign-ticket", problems.Count == 0,
            "Installed output: for all " + conversations.Count + " conversations the badge (installed statusline command) carries the conversation's own name, branch and worktree, and no surface (badge, tab, chip details, speech) mentions another conversation's ticket: " +
            (problems.Count == 0 ? string.Join("; ", conversations.Take(5).Select(c => c.Key + " badge '" + snapshots[c.Key].Badge + "'")) + (conversations.Count > 5 ? "; and the others" : "") : string.Join("; ", problems)) + ".");
        const string Evaluated = "Product function over installed state, evaluated in-process with the harness build (not an installed output): ";
        var all = snapshots.Values.ToList();
        foreach (var outcome in new[] { SurfaceDistinctness.Tabs(all), SurfaceDistinctness.Chips(all), SurfaceDistinctness.Speech(all) })
        {
            Check(outcome.Surface + "-distinct", outcome.Passed,
                Evaluated + "the " + outcome.Surface + " differs across all " + all.Count + " conversations of this launch directory, none exempt" + (outcome.Passed ? ": " + string.Join("; ", conversations.Select(c => c.Key + " '" + SurfaceText(outcome.Surface, snapshots[c.Key]) + "'")) : "; collisions: " + string.Join("; ", outcome.Collisions)) + ".");
        }
        var names = all.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count();
        Check("distinct-identities", names == all.Count,
            names + " distinct names across " + all.Count + " conversations in the same launch directory, none exempt (a conversation whose branch changed keeps its verified checkout and shows its own ticket, not the launch checkout's name)" +
            "; provenance by conversation: " + string.Join(", ", conversations.Select(c => c.Key + "=" + snapshots[c.Key].Provenance)) + ".");
    }

    private static string SurfaceText(string surface, SurfaceSnapshot snapshot) => surface switch
    {
        "tab" => snapshot.Tab,
        "chip" => snapshot.ChipName + " | " + snapshot.ChipDetails,
        _ => snapshot.Speech
    };

    // ---- Fixtures and hooks ------------------------------------------------------------------------------------------------------------

    private static void WriteTranscript(Instance instance, Conversation conversation, DateTimeOffset at) =>
        ConversationFixture.WriteHistory(instance, conversation.Model, conversation.SessionId, conversation.Role, conversation.Worktree, conversation.Change, at, conversation.HasNativeTitle ? conversation.NativeTitle : null);

    private static bool Fire(Instance instance, Conversation conversation, string hookEvent, string directory, string? source = null)
    {
        var payload = conversation.Model.Payload(hookEvent, conversation.SessionId, directory, instance.Layout, source: source);
        return conversation.Model.Accepted(conversation.Session.Hook(CandidateInstall.Command(instance, conversation.Model, hookEvent), payload, conversation.Model.RuntimeShell(instance)));
    }

    private static bool FireShell(Instance instance, Conversation conversation, string directory, string command)
    {
        var payload = conversation.Model.ShellToolPayload(conversation.SessionId, directory, instance.Layout, "inspect-" + Guid.NewGuid().ToString("N")[..8], command);
        return conversation.Model.Accepted(conversation.Session.Hook(CandidateInstall.Command(instance, conversation.Model, "PostToolUse"), payload, conversation.Model.RuntimeShell(instance)));
    }

    private static string Tower(Instance instance, params string[] arguments)
    {
        var result = instance.Tower(arguments);
        if (!result.Succeeded)
        {
            throw new IOException("bell-tower " + arguments[0] + " failed: " + result.Error.Trim());
        }
        return result.Output.Trim();
    }

    private static bool PathsEqual(string left, string right) => left != "" && CheckoutPolicy.SamePath(left, right);

    private static string ShellName(ProviderModel model, Instance instance) => model.Name + " through " + model.RuntimeShell(instance).Name;

    private static void AddNotes(RunContext context)
    {
        context.Report.Notes.Add("five-conversations, surfaces: only the badge is an installed output (the installed `statusline` command). The tab title (the product's title builder), the chip details (the git details string with the identity name) and the spoken phrase (the product's phrase builder) are product functions over the installed state, evaluated in-process with the harness build; " +
            "they are not outputs of the installed candidate, and each is checked for distinctness across the conversations of one launch directory, with no conversation exempt. The actual terminal tab write needs a real terminal host and is not performed here; " +
            "the chip is only drawn while a session waits for attention, so no chip is rendered (approval waits are the subject of tasks 2.4 and 2.7); the tray runs muted with a silent sink, so no spoken phrase is captured from the tray itself.");
        context.Report.Notes.Add("five-conversations, native names: Claude custom-title records are modeled in the conversation transcript (labeled model, the record shape from the isolated SDK probe) and read back by the installed tray. Codex explicit native names are an UNFINISHED parity item, reported as its own counted outcome (`native-title-tier`), not as a pass or a restriction: a native Codex thread name lives in the Codex runtime (`session_index.jsonl` and the runtime's name service), " +
            "which this sandbox does not run, and the shipped policy does not promote a Codex name set outside Bell Tower to the explicit-title tier (it pauses name writes instead; see `NativeNamePolicy.Pause`). The Codex row verifies the shared tiers (manual, pinned change, work, reset). Closing evidence: a supported Codex read with name provenance, or a version-scoped probe proving none exists (user decision 2026-11-25: open for 0.1.11).");
        context.Report.Notes.Add("five-conversations, models: transcripts (Claude tool_use/tool_result and custom-title records, Codex apply_patch records) and the PostToolUse inspection events are labeled models built from the documented shapes (`tests/BellTower.Tests/Fixtures/identity/provenance.json`); SessionStart and UserPromptSubmit payloads are the native captures' field sets. " +
            "None of this is an authenticated provider run. Roles: Bell Tower has no session role (`--role` is accepted and ignored), so the role conversation proves role-free naming.");
    }
}
