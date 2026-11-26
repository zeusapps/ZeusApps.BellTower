using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BellTower.Distribution;
using BellTower.FieldAcceptance.Evidence;
using BellTower.FieldAcceptance.Harness;
using BellTower.IO;
using BellTower.Protocol;
using BellTower.Updates;

namespace BellTower.FieldAcceptance.Scenarios;

/// <summary>
/// Task 2.1 and ledger row U1. Installs the published 0.1.9 archive, runs 0.1.9's own setup and seeds identity stores, color pins and
/// palettes through 0.1.9 commands, upgrades to the fixture-version candidate without rerunning setup, and checks both providers
/// and a mixed run: 0.1.9-image captures are still accepted, doctor reports the stale hook ingress image with its advice, the
/// post-0.1.9 hook-side features that need the new image are inactive, and the baseline data is preserved. It then runs
/// <c>setup --host-bridge --apply</c> inside the disposable home and repeats the checks against the current image.
/// </summary>
public sealed class UpgradeWithoutSetupScenario : IScenario
{
    private const string StaleAdvice = "rerun setup --host-bridge --apply; Codex may ask to trust the changed hooks";
    private static readonly string[] Providers = [ProviderNames.Claude, ProviderNames.Codex];
    private static readonly Regex ImageHash = new(@"provider-ingress[/\\]1[/\\]([0-9a-fA-F]{64})[/\\]bell-tower-ingress\.exe", RegexOptions.CultureInvariant);

    public string Id => "upgrade-without-setup";
    public string Title => "Published 0.1.9 to the fixture-version candidate: upgrade without setup, then setup --host-bridge --apply";

    public void Run(RunContext context)
    {
        var scenario = Id;
        void Check(string id, string provider, bool passed, string detail, EvidenceLabel label = EvidenceLabel.InstalledHarness) =>
            context.Check(scenario, id, provider, passed, detail, label);

        var instance = context.CreateInstance("upgrade");
        var work = CreateWorkDirectory(instance);
        instance.ExtractInstallation(context.Options.BaselineArchive);
        var baselineVersion = instance.Tower("--version").Output.Trim();
        var candidateVersion = context.Report.Candidate!.Version;
        Check("baseline-installed", ProviderNames.Harness, baselineVersion == "0.1.9", "Installed the published 0.1.9 archive under the disposable install root; launcher reports " + baselineVersion + ".");
        Check("isolation-environment", ProviderNames.Harness,
            instance.Environment["USERPROFILE"] == instance.Layout.Home && instance.Environment["CLAUDE_CONFIG_DIR"] == instance.Layout.ClaudeDirectory &&
            instance.Environment["CODEX_HOME"] == instance.Layout.CodexDirectory && instance.Environment["BELLTOWER_DATA_DIR"] == instance.Layout.Data &&
            instance.Environment["PATH"].StartsWith(instance.Layout.FakeBin, StringComparison.OrdinalIgnoreCase) && instance.EnvironmentViolations.Count == 0,
            "Child processes get a sandbox USERPROFILE, HOME, APPDATA, LOCALAPPDATA, TEMP, CLAUDE_CONFIG_DIR, CODEX_HOME, BELLTOWER_DATA_DIR and a PATH that starts with the fake provider directory; " +
            instance.RemovedPathEntries.Count + " PATH entries inside production locations were dropped; every Bell Tower command also passes --data-dir. " +
            "Two effects reach outside the sandbox: the on-demand tray-start task setup registers in the user's Task Scheduler, and, by product design, a tray started by a hook, which runs as a child of the desktop shell with the real profile environment " +
            "(USERPROFILE, APPDATA, LOCALAPPDATA, TEMP and any global CODEX_HOME or CLAUDE_CONFIG_DIR). The harness exits that tray right after its probe and restarts it silently under the sandbox environment " +
            "(the hook-started-tray-replaced check verifies both steps), and an instance that has both a hook-started tray and a shared-backend Codex session is refused and its trays ended.");

        // ---- Baseline: 0.1.9's own setup and data -------------------------------------------------------------------
        var baselineSetup = instance.TowerWithHome("setup", "--clients", "both", "--apply");
        var baselineHashes = Providers.ToDictionary(provider => provider, provider => HashOf(Owned(instance, provider, "SessionStart")));
        Check("baseline-setup", ProviderNames.Mixed, baselineSetup.Succeeded && Providers.All(provider => baselineHashes[provider] is not null),
            "0.1.9 setup --clients both --apply wrote owned hooks into the fake provider homes (ingress image " + Short(baselineHashes[ProviderNames.Claude]) + ").");
        // Silent by construction: no sound, no update check (and so no network), for every tray this instance ever starts.
        new ConfigStore(new DataPaths(instance.Layout.Data)).Update(config => { config.Muted = true; config.Updates.Enabled = false; });
        var baselineTray = instance.Tower("start", "--silent");
        TowerReader.WaitFor(() => TowerReader.TrayRunning(instance));
        Check("baseline-tray", ProviderNames.Mixed, baselineTray.Succeeded && TowerReader.TrayRunning(instance), "0.1.9 tray started silently for the disposable data directory.");

        var sessions = new Dictionary<string, AgentSession>();
        foreach (var name in new[] { "pinned", "auto" })
        {
            foreach (var provider in Providers)
            {
                var id = provider + "-" + name;
                sessions[id] = instance.StartSession(provider, id, work, "att-" + id);
            }
        }
        var seedAccepted = new Dictionary<string, bool>();
        foreach (var hookEvent in new[] { "SessionStart", "UserPromptSubmit" })
        {
            foreach (var session in sessions.Values)
            {
                var result = Fire(instance, session, hookEvent, BaselineShell(instance, session.Provider));
                seedAccepted[session.Provider] = seedAccepted.GetValueOrDefault(session.Provider, true) && ProviderModel.For(session.Provider).Accepted(result);
            }
        }
        var seededKeys = sessions.Values.Select(session => session.Key).ToList();
        var seededRows = TowerReader.WaitForSessions(instance, seededKeys);
        foreach (var provider in Providers)
        {
            var color = provider == ProviderNames.Claude ? "#336699" : "#993366";
            var rename = instance.Tower("rename", "--session", provider + ":" + provider + "-pinned", "--name", provider + " pinned name", "--color", color,
                "--change", "change-" + provider, "--checkout", work);
            Check("baseline-seed", provider, seedAccepted[provider] && rename.Succeeded && seededKeys.Where(key => key.StartsWith(provider, StringComparison.Ordinal)).All(key => seededRows.Any(row => row.Key == key)),
                "0.1.9 ingress accepted the hooks of two " + provider + " sessions (through " + ShellLabel(BaselineShell(instance, provider)) + (provider == ProviderNames.Codex ? ", the modeled cmd fallback: the 0.1.9 command form does not parse under PowerShell" : "") + ") and 0.1.9 rename stored name, change association, working checkout and color pin " + color + " for one; the other keeps its automatic name and palette.");
        }
        var baselineRows = TowerReader.List(instance);
        var baselineView = seededKeys.ToDictionary(key => key, key => TowerReader.IdentityView(baselineRows.Single(row => row.Key == key).Identity));
        var baselineStores = TowerReader.IdentityStores(instance);
        Check("baseline-data", ProviderNames.Mixed,
            seededKeys.All(key => baselineStores.ContainsKey(key)) && baselineView["claude:claude-pinned"]["manualName"]!.GetValue<string>() == "claude pinned name" &&
            baselineView["codex:codex-pinned"]["colorPin"]!.GetValue<string>() == "#993366",
            seededKeys.Count + " identity stores written by 0.1.9 (" + string.Join(", ", seededKeys.Select(key => key + " " + TowerReader.Text(baselineView[key], "palette", "accent"))) + ").");

        // ---- Upgrade without rerunning setup ------------------------------------------------------------------------
        var settingsBefore = Providers.ToDictionary(provider => provider, provider => File.ReadAllBytes(ProviderModel.For(provider).SettingsPath(instance.Layout)));
        instance.Tower("exit");
        TowerReader.WaitFor(() => !TowerReader.TrayRunning(instance));
        var offer = DistributionJson.Read<ReleaseOffer>(context.Options.CandidateArchive[..^4] + ".release.json");
        var installation = new Installation(instance.Layout.Install);
        var paths = new DataPaths(instance.Layout.Data);
        instance.WithProcessEnvironment(() =>
        {
            var plan = UpdateTransaction.Stage(installation, paths, context.Options.CandidateArchive, offer);
            UpdateTransaction.Start(plan);
            return plan;
        });
        TowerReader.WaitFor(() => File.Exists(UpdateTransaction.OutcomePath(installation)) &&
            DistributionJson.Read<UpdateOutcome>(UpdateTransaction.OutcomePath(installation)).State is "activated" or "failed" or "monitoring-restored" or "rolled-back", 90000);
        var outcome = DistributionJson.Read<UpdateOutcome>(UpdateTransaction.OutcomePath(installation));
        var installedVersion = instance.Tower("--version").Output.Trim();
        Check("upgrade-activated", ProviderNames.Mixed, outcome.State is "activated" or "monitoring-restored" && installedVersion == candidateVersion && installation.Selection().Version == candidateVersion,
            "The candidate payload was staged and activated by the real update transaction (state " + outcome.State + "); the launcher now reports " + installedVersion + ". The tray was stopped, so the update did not start one: " + (TowerReader.TrayRunning(instance) ? "a tray is running" : "no tray running") + ".");
        Check("setup-not-rerun", ProviderNames.Mixed, Providers.All(provider => File.ReadAllBytes(ProviderModel.For(provider).SettingsPath(instance.Layout)).SequenceEqual(settingsBefore[provider])),
            "setup was not rerun: the Claude Code and Codex hook settings are byte-identical and still name the 0.1.9 ingress image; the 0.1.9 image file is retained: " +
            Providers.All(provider => File.Exists(ImagePath(instance, baselineHashes[provider]!))));
        instance.Tower("start", "--silent");
        TowerReader.WaitFor(() => TowerReader.TrayRunning(instance));
        Check("candidate-tray", ProviderNames.Mixed, TowerReader.TrayRunning(instance), "The candidate tray started silently against the data the 0.1.9 tray wrote.");

        var candidateHash = installation.Contract(candidateVersion).Files!["bell-tower-ingress.exe"];
        Check("candidate-image-differs", ProviderNames.Mixed, !string.Equals(candidateHash, baselineHashes[ProviderNames.Claude], StringComparison.OrdinalIgnoreCase),
            "The candidate payload ships a different ingress image (" + Short(candidateHash) + ") than the 0.1.9 image the hooks name (" + Short(baselineHashes[ProviderNames.Claude]) + ").");

        // ---- State 1: upgraded, setup not rerun -----------------------------------------------------------------------
        VerifyState(context, instance, work, sessions, seededKeys, baselineView, stale: true, candidateHash, baselineHashes, "stale");

        context.Report.Notes.Add("U1, plainly: after the upgrade and before setup is rerun, Codex hooks fail outright wherever Codex runs them through PowerShell (the stale/captures-default-shell check is an expected-failure check that observed the 0.1.9 command's parser error), " +
            "so a setup rerun is required for Codex to deliver hooks at all, not only for the new features. Claude Code hooks keep working. The stale-state Codex captures that the tray sees run through a labeled modeled cmd fallback, which models a Codex whose PowerShell discovery failed and does not claim Codex falls back to cmd.");

        // ---- State 2: setup --host-bridge --apply inside the disposable homes ------------------------------------------
        var rerun = instance.TowerWithHome(300000, "setup", "--host-bridge", "--apply");
        var currentHashes = Providers.ToDictionary(provider => provider, provider => HashOf(Owned(instance, provider, "SessionStart")));
        Check("setup-host-bridge", ProviderNames.Mixed,
            rerun.Succeeded && Providers.All(provider => string.Equals(currentHashes[provider], candidateHash, StringComparison.OrdinalIgnoreCase)) &&
            Providers.All(provider => File.Exists(ImagePath(instance, baselineHashes[provider]!)) && File.Exists(ImagePath(instance, candidateHash))),
            "setup --host-bridge --apply (candidate launcher, --home and --data-dir inside the sandbox) rewrote the owned hooks to the candidate image " + Short(candidateHash) +
            "; the 0.1.9 image stays on disk, so running sessions are not cut off. exit " + rerun.ExitCode + (rerun.Succeeded ? "" : ": " + rerun.Error));
        var bridgeNotes = rerun.Output.Split('\n').Where(line => line.Contains("VS Code", StringComparison.OrdinalIgnoreCase)).Select(line => line.Trim()).Take(3).ToList();
        context.Report.Notes.Add("setup --host-bridge VS Code lines: " + (bridgeNotes.Count == 0 ? "none" : string.Join(" | ", bridgeNotes)) +
            ". The sandbox profile has no VS Code installation (the child USERPROFILE redirect also moves the product's known-folder lookups), so the focus-bridge extension install is not exercised by this scenario; " +
            "its settings are written only into the fake home. VS Code bridge behavior stays with tasks 2.6 and 4.1.");
        VerifyState(context, instance, work, sessions, seededKeys, baselineView, stale: false, candidateHash, baselineHashes, "current");
        context.Report.Notes.Add("Setup registers an on-demand tray-start task for the sandbox data directory in the user's Task Scheduler (task " + (instance.TaskExists() ? "present" : "not registered") +
            " after the setup rerun). It is the first of two effects outside the sandbox directories; the run deletes it at cleanup and fails the isolation check if any other Bell Tower task changed or the sandbox task remains. " +
            "The second is the tray a hook starts (see the hook-started-tray checks): it runs with the real profile environment by product design, and the harness replaces it with a sandbox-environment tray right after its probe.");
        context.Report.Notes.Add("Codex hook shell: discovered the way Codex 0.164.0 does (pwsh.exe on PATH, then PowerShell 7 at its fixed path, then powershell.exe on PATH, then Windows PowerShell at its fixed path, then cmd) and resolved to " + ShellLabel(ProviderModel.Codex.RuntimeShell(instance)) +
            (ProviderModel.Codex.RuntimeShell(instance).Note is { } shellNote ? " (" + shellNote + ")" : "") +
            " for this run; Claude Code hooks run through Git Bash. Payloads are the field sets of the native captures in tests/BellTower.Tests/Fixtures/native with sandbox session, directory and transcript values.");
    }

    // The checks that run in both states. In the stale state the hook ingress image is the 0.1.9 one; afterwards it is the candidate's.
    private static void VerifyState(RunContext context, Instance instance, string work, Dictionary<string, AgentSession> sessions, List<string> seededKeys,
        Dictionary<string, JsonObject> baselineView, bool stale, string candidateHash, Dictionary<string, string?> baselineHashes, string state)
    {
        void Check(string id, string provider, bool passed, string detail, bool expectedFailure = false) =>
            context.Check("upgrade-without-setup", state + "/" + id, provider, passed, detail, expectedFailure: expectedFailure);
        var doctor = TowerReader.Doctor(instance);
        foreach (var provider in Providers)
        {
            var image = doctor["hookStart"]?["ingressImage"]?[provider];
            var status = image?["status"]?.GetValue<string>();
            var advice = image?["advice"]?.GetValue<string>();
            var recorded = image?["recordedImage"]?.GetValue<string>();
            var expectedRecorded = Short(stale ? baselineHashes[provider] : candidateHash);
            var passed = stale
                ? status == "stale" && advice == StaleAdvice && recorded == expectedRecorded
                : status == "current" && advice is null && recorded == expectedRecorded;
            Check("doctor-hook-image", provider, passed,
                "doctor hookStart.ingressImage." + provider + ": status " + status + ", recorded image " + recorded + (advice is null ? ", no advice." : ", advice \"" + advice + "\"."));
        }
        var availability = doctor["hookStart"]?["availability"]?.GetValue<string>() ?? "";
        Check("doctor-availability", ProviderNames.Mixed, stale ? availability.StartsWith("needs a setup refresh", StringComparison.Ordinal) : availability == "available",
            "doctor hookStart.availability: " + availability);

        // Captures through the installed hook commands: per provider, then interleaved (mixed). Claude Code runs through Git Bash; Codex runs
        // through the shell Codex 0.164.0 discovers (PowerShell). The 0.1.9 Codex command form does not parse there, so in the stale state the
        // default-shell Codex captures are an expected-failure check (the known defect, the real Codex outcome of U1). A separate, labeled modeled
        // cmd fallback is what lets the tray see the Codex sessions the stale-state checks below need.
        var accepted = new Dictionary<string, int>();
        var expected = new Dictionary<string, int>();
        var parserFailures = new Dictionary<string, int>();
        var fallbackAccepted = new Dictionary<string, int>();
        var fallbackExpected = new Dictionary<string, int>();
        var failures = new List<string>();
        var newSessions = new List<AgentSession>();
        foreach (var provider in Providers)
        {
            var id = provider + "-" + state;
            newSessions.Add(instance.StartSession(provider, id, work, "att-" + id));
        }
        var all = sessions.Values.Concat(newSessions).ToList();
        foreach (var hookEvent in new[] { "SessionStart", "UserPromptSubmit", "UserPromptSubmit" })
        {
            foreach (var session in all)
            {
                if (hookEvent == "SessionStart" && sessions.ContainsValue(session))
                {
                    continue;
                }
                var model = ProviderModel.For(session.Provider);
                var result = Fire(instance, session, hookEvent, model.RuntimeShell(instance));
                expected[session.Provider] = expected.GetValueOrDefault(session.Provider) + 1;
                if (model.Accepted(result))
                {
                    accepted[session.Provider] = accepted.GetValueOrDefault(session.Provider) + 1;
                }
                else
                {
                    failures.Add(session.Key + " " + hookEvent + " exit " + result.ExitCode + " " + Trim(result.Error));
                    if (IsParserError(result.Error))
                    {
                        parserFailures[session.Provider] = parserFailures.GetValueOrDefault(session.Provider) + 1;
                    }
                }
                if (stale && model.ModeledFallbackShell(instance) is { } fallback)
                {
                    fallbackExpected[session.Provider] = fallbackExpected.GetValueOrDefault(session.Provider) + 1;
                    if (model.Accepted(Fire(instance, session, hookEvent, fallback)))
                    {
                        fallbackAccepted[session.Provider] = fallbackAccepted.GetValueOrDefault(session.Provider) + 1;
                    }
                }
            }
        }
        foreach (var provider in Providers)
        {
            var model = ProviderModel.For(provider);
            var shell = ShellLabel(model.RuntimeShell(instance));
            var image = stale ? "0.1.9" : "candidate";
            var providerFailures = failures.Where(failure => failure.StartsWith(provider, StringComparison.Ordinal)).ToList();
            if (stale && provider == ProviderNames.Codex)
            {
                Check("captures-default-shell", provider, accepted.GetValueOrDefault(provider) == 0 && parserFailures.GetValueOrDefault(provider) == expected[provider],
                    "EXPECTED FAILURE (the known defect, the real U1 outcome for Codex): " + accepted.GetValueOrDefault(provider) + " of " + expected[provider] + " installed " + provider + " hook commands (" + image + " image) were accepted through " + shell +
                    ", the shell Codex 0.164.0 uses by default; " + parserFailures.GetValueOrDefault(provider) + " failed with a PowerShell parser error. Until setup is rerun Codex hooks fail wherever Codex runs them through PowerShell." +
                    (providerFailures.Count > 0 ? " First failure: " + providerFailures[0] : ""), expectedFailure: true);
                var cmd = ShellLabel(model.ModeledFallbackShell(instance)!);
                Check("captures-modeled-cmd-fallback", provider, fallbackAccepted.GetValueOrDefault(provider) == fallbackExpected[provider] && fallbackExpected[provider] > 0,
                    "Modeled cmd fallback (a model of a Codex whose PowerShell discovery failed, not a claim that Codex falls back to cmd): " + fallbackAccepted.GetValueOrDefault(provider) + " of " + fallbackExpected[provider] + " installed " + provider +
                    " hook commands exited 0 with {} through " + cmd + " (" + image + " image).");
            }
            else
            {
                Check("captures-accepted", provider, accepted.GetValueOrDefault(provider) == expected[provider] && expected[provider] > 0,
                    accepted.GetValueOrDefault(provider) + " of " + expected[provider] + " installed " + provider + " hook commands exited 0 with {} through " + shell + " (" + image + " image)" +
                    (providerFailures.Count > 0 ? ": " + string.Join("; ", providerFailures) : "."));
            }
        }
        var expectedKeys = all.Select(session => session.Key).ToList();
        var rows = TowerReader.WaitForSessions(instance, expectedKeys);
        var listed = expectedKeys.Count(key => rows.Any(row => row.Key == key));
        var codexOk = stale ? accepted.GetValueOrDefault(ProviderNames.Codex) == 0 && fallbackAccepted.GetValueOrDefault(ProviderNames.Codex) == fallbackExpected.GetValueOrDefault(ProviderNames.Codex)
            : accepted.GetValueOrDefault(ProviderNames.Codex) == expected[ProviderNames.Codex];
        Check("captures-accepted", ProviderNames.Mixed, listed == expectedKeys.Count && accepted.GetValueOrDefault(ProviderNames.Claude) == expected[ProviderNames.Claude] && codexOk,
            "Interleaved claude and codex hooks from " + all.Count + " simultaneous sessions in one launch directory: through each provider's own shell " + accepted.Values.Sum() + " of " + expected.Values.Sum() + " accepted (claude " +
            accepted.GetValueOrDefault(ProviderNames.Claude) + " of " + expected[ProviderNames.Claude] + ", codex " + accepted.GetValueOrDefault(ProviderNames.Codex) + " of " + expected[ProviderNames.Codex] + ")" +
            (stale ? "; modeled cmd fallback for Codex " + fallbackAccepted.GetValueOrDefault(ProviderNames.Codex) + " of " + fallbackExpected.GetValueOrDefault(ProviderNames.Codex) + " accepted" : "") +
            "; the installed tray lists " + listed + " of " + expectedKeys.Count + " sessions.");

        VerifyInactiveFeatures(context, instance, work, stale, state);

        // Baseline data written by 0.1.9 stays intact.
        foreach (var provider in Providers)
        {
            var keys = seededKeys.Where(key => key.StartsWith(provider, StringComparison.Ordinal)).ToList();
            var differences = keys.Where(key => !JsonNode.DeepEquals(baselineView[key], TowerReader.IdentityView(rows.Single(row => row.Key == key).Identity))).ToList();
            var stores = TowerReader.IdentityStores(instance);
            var storeDifferences = keys.Where(key => !stores.TryGetValue(key, out var store) || !JsonNode.DeepEquals(baselineView[key], TowerReader.IdentityView(store))).ToList();
            Check("baseline-data-preserved", provider, differences.Count == 0 && storeDifferences.Count == 0,
                keys.Count + " identity stores seeded by 0.1.9 (names, change, color pin, checkout pin, palette) read back unchanged through list and from disk" +
                (differences.Count + storeDifferences.Count == 0 ? "." : "; changed: " + string.Join(", ", differences.Concat(storeDifferences).Distinct())));
        }
        var allDifferences = seededKeys.Where(key => !JsonNode.DeepEquals(baselineView[key], TowerReader.IdentityView(rows.Single(row => row.Key == key).Identity))).ToList();
        var distinctAccents = seededKeys.Select(key => TowerReader.Text(rows.Single(row => row.Key == key).Identity, "palette", "accent")).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        Check("baseline-data-preserved", ProviderNames.Mixed, allDifferences.Count == 0,
            "All " + seededKeys.Count + " mixed-run identities unchanged; " + distinctAccents + " distinct palette accents across them.");
    }

    // The post-0.1.9 hook-side features of ledger row U1 and whether each is active with the hook commands currently installed.
    private static void VerifyInactiveFeatures(RunContext context, Instance instance, string work, bool stale, string state)
    {
        var featureResults = new List<bool>();
        void Check(string id, string provider, bool passed, string detail)
        {
            featureResults.Add(passed);
            context.Check("upgrade-without-setup", state + "/" + id, provider, passed, detail);
        }
        var label = stale ? "inactive (0.1.9 image)" : "active (candidate image)";

        // 1. Native adoption: a plain provider start (no Bell Tower launch, so no attachment id) is classified and tied to its process only by the new image.
        var plainKeys = new List<string>();
        foreach (var provider in Providers)
        {
            var plain = instance.StartSession(provider, provider + "-plain-" + state, work, null);
            Fire(instance, plain, "SessionStart", CaptureShell(instance, provider, stale));
            Fire(instance, plain, "UserPromptSubmit", CaptureShell(instance, provider, stale));
            plainKeys.Add(plain.Key);
        }
        TowerReader.WaitForSessions(instance, plainKeys);
        foreach (var provider in Providers)
        {
            var key = plainKeys.Single(k => k.StartsWith(provider, StringComparison.Ordinal));
            var attachment = WaitForAttachment(instance, key);
            var mode = TowerReader.Text(attachment, "evidence", "mode");
            var ownership = TowerReader.Text(attachment, "ownership");
            var health = TowerReader.Text(attachment, "health");
            var hasProcess = attachment?["process"] is not null;
            var detail = provider + (provider == ProviderNames.Codex ? " --no-daemon" : "") + " started without Bell Tower (hooks through " + ShellLabel(CaptureShell(instance, provider, stale)) + (stale && provider == ProviderNames.Codex ? ", modeled cmd fallback" : "") +
                "): attachment mode " + mode + ", ownership " + ownership + ", health " + health + ", process identity " + (hasProcess ? "captured" : "not captured") + ".";
            var passed = stale
                ? mode == "unknown" && (provider == ProviderNames.Claude || (ownership == "shared-runtime" && !hasProcess))
                : provider == ProviderNames.Claude ? mode == "interactive-client" : mode == "embedded-runtime" && ownership == "observed" && hasProcess && health == "verified-live";
            Check("native-adoption", provider, passed, "Native adoption " + label + ": " + detail);
        }
        Check("native-adoption", ProviderNames.Mixed, TowerReader.List(instance).Count(row => plainKeys.Contains(row.Key)) == 2,
            "Both plain sessions are listed together alongside the managed ones; adoption is evaluated per provider by the same shared policy.");

        // 2. The repaired Codex hook command: the 0.1.9 command only parses in the command shell, not in PowerShell, the shell Codex 0.164.0 uses by default on Windows.
        if (stale)
        {
            var codex = instance.Sessions.Last(session => session.Provider == ProviderNames.Codex);
            var shell = ProviderModel.Codex.RuntimeShell(instance);
            var result = Fire(instance, codex, "UserPromptSubmit", shell);
            Check("codex-command-form", ProviderNames.Codex, !result.Succeeded && IsParserError(result.Error),
                "Repaired Codex hook command " + label + ": the installed 0.1.9 command run through " + ShellLabel(shell) + " exits " + result.ExitCode + " with a parser error (it only works in the command shell); the candidate's setup writes the PowerShell-safe form.");
        }
        else
        {
            var command = Owned(instance, ProviderNames.Codex, "UserPromptSubmit")!;
            Check("codex-command-form", ProviderNames.Codex, command.StartsWith("& '", StringComparison.Ordinal),
                "Repaired Codex hook command " + label + ": the installed command is the PowerShell call form (& 'image' --provider codex ...), accepted through " + ShellLabel(ProviderModel.Codex.RuntimeShell(instance)) + " above.");
        }

        // 3. The hook-started tray: with the tray stopped (and not stopped by the user), the first hook starts it, but only through the new image.
        // By product design that tray is a child of the desktop shell and runs with the real profile environment, so the probe requires the
        // shell route (the scheduled-task route has the same exposure). The instance notes every hook that starts a tray (and every session start
        // without --no-daemon) itself, and refuses the combination; here the tray is replaced with a sandbox-environment one and that is checked.
        foreach (var provider in Providers)
        {
            instance.Tower("exit");
            TowerReader.WaitFor(() => !TowerReader.TrayRunning(instance));
            var hookStart = Path.Combine(instance.Layout.Data, "hook-start");
            // The exit command records a user stop that keeps the tray down by design; the probe models a tray that is simply not running (for example after a restart).
            HookTrayStart.ClearUserStop(instance.Layout.Data);
            if (Directory.Exists(hookStart))
            {
                foreach (var file in Directory.EnumerateFiles(hookStart, "attempt-*.lock").Append(Path.Combine(hookStart, "last-attempt.txt")))
                {
                    File.Delete(file);
                }
            }
            var session = instance.Sessions.Last(candidate => candidate.Provider == provider);
            var result = Fire(instance, session, "UserPromptSubmit", CaptureShell(instance, provider, stale));
            TowerReader.WaitFor(() => TowerReader.TrayRunning(instance), stale ? 8000 : 25000);
            var started = TowerReader.TrayRunning(instance);
            var recorded = HookTrayStart.LastAttempt(instance.Layout.Data);
            var attempt = recorded is not null || File.Exists(Path.Combine(hookStart, "last-attempt.txt"));
            var route = recorded?.Detail ?? "none";
            var viaShell = route == HookStartRoute.Shell;
            Check("hook-started-tray", provider, ProviderModel.For(provider).Accepted(result) && (stale ? !started && !attempt : started && attempt && viaShell),
                "Hook-started tray " + label + ": a " + provider + " hook with the tray stopped " + (started ? "started the tray" : "left the tray stopped") + (attempt ? " and recorded a start attempt (route " + route + (stale || viaShell ? "" : "; the shell route '" + HookStartRoute.Shell + "' is required") + ")." : " and recorded no start attempt.") +
                (started ? " That tray ran with the real profile environment by product design (see hook-started-tray-replaced for what the harness did about it)." : ""));
            // Whatever started: no later step may run a tray with the real profile environment, and the replacement is evidence, not an assumption.
            if (started)
            {
                var replacement = TrayReplacement.Run(() => TowerReader.TrayRunning(instance), () => instance.Tower("exit"), () => instance.Tower("start", "--silent"),
                    (condition, timeout) => TowerReader.WaitFor(condition, timeout), () => instance.EndTrays());
                Check("hook-started-tray-replaced", provider, replacement.Passed, replacement.Detail);
            }
            else if (!TowerReader.TrayRunning(instance))
            {
                // Nothing started (the stale state): put the sandbox tray back for the steps that follow.
                instance.Tower("start", "--silent");
                Check("tray-restored", provider, TowerReader.WaitFor(() => TowerReader.TrayRunning(instance)), "No hook started a tray; the sandbox tray was restarted silently for the next step.");
            }
        }
        Check("inactive-features", ProviderNames.Mixed, featureResults.All(passed => passed),
            stale ? "Until setup is rerun the upgraded hooks keep the 0.1.9 behavior: no native adoption of plain claude or codex --no-daemon, no hook-started tray, and the 0.1.9 Codex command form (fails under PowerShell). See the checks above per provider."
                  : "After the setup rerun all three post-0.1.9 hook-side features are active for both providers.");
    }

    private static JsonNode? WaitForAttachment(Instance instance, string key)
    {
        JsonNode? found = null;
        TowerReader.WaitFor(() => (found = Attachment(instance, key)) is not null, 20000);
        return found;
    }

    private static JsonNode? Attachment(Instance instance, string key)
    {
        try
        {
            var state = JsonNode.Parse(File.ReadAllText(Path.Combine(instance.Layout.Data, "state.json")));
            var id = state?["sessions"]?[key]?["attachmentId"]?.GetValue<string>();
            if (id is null)
            {
                return null;
            }
            foreach (var file in Directory.EnumerateFiles(Path.Combine(instance.Layout.Data, "attachments"), "*.json"))
            {
                var node = JsonNode.Parse(File.ReadAllText(file));
                if (node?["id"]?.GetValue<string>() == id)
                {
                    return node;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            // The tray replaces these files atomically; the next poll reads the new copy.
        }
        return null;
    }

    // ---- Helpers -------------------------------------------------------------------------------------------------

    private static string CreateWorkDirectory(Instance instance)
    {
        // A minimal Git metadata directory gives the launch directory its own checkout and branch without starting git.
        var work = Path.Combine(instance.Layout.Root, "work", "main");
        Directory.CreateDirectory(Path.Combine(work, ".git", "refs", "heads"));
        Directory.CreateDirectory(Path.Combine(work, ".git", "objects"));
        File.WriteAllText(Path.Combine(work, ".git", "HEAD"), "ref: refs/heads/main\n");
        return work;
    }

    private static string? Owned(Instance instance, string provider, string hookEvent) => ProviderModel.For(provider).OwnedCommand(instance.Layout, hookEvent);

    private static ProcessResult Fire(Instance instance, AgentSession session, string hookEvent, ResolvedShell shell) =>
        session.Hook(Owned(instance, session.Provider, hookEvent) ?? throw new InvalidDataException("No owned " + hookEvent + " hook for " + session.Provider),
            ProviderModel.For(session.Provider).Payload(hookEvent, session.SessionId, session.Directory, instance.Layout), shell);

    // The 0.1.9 Codex hook command is written for the Windows command shell: seeding the baseline needs the modeled cmd fallback.
    private static ResolvedShell BaselineShell(Instance instance, string provider) =>
        ProviderModel.For(provider).ModeledFallbackShell(instance) ?? ProviderModel.For(provider).RuntimeShell(instance);

    // Where a step needs a capture to succeed: the provider's own shell, except in the stale state where the 0.1.9 Codex command only parses in the modeled cmd fallback.
    private static ResolvedShell CaptureShell(Instance instance, string provider, bool stale) =>
        stale ? BaselineShell(instance, provider) : ProviderModel.For(provider).RuntimeShell(instance);

    private static bool IsParserError(string error) =>
        error.Contains("Unexpected token", StringComparison.Ordinal) || error.Contains("ParserError", StringComparison.Ordinal) || error.Contains("ParseException", StringComparison.Ordinal);

    private static string ShellLabel(ResolvedShell shell) => shell.Kind switch
    {
        HookShell.GitBash => "Git Bash (" + shell.Name + ")",
        HookShell.Cmd => "the command shell (" + shell.Name + ")",
        _ => shell.Name.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase) ? "PowerShell 7 (pwsh.exe)" : "Windows PowerShell (" + shell.Name + ")"
    };

    private static string? HashOf(string? command) => command is not null && ImageHash.Match(command) is { Success: true } match ? match.Groups[1].Value.ToLowerInvariant() : null;

    private static string ImagePath(Instance instance, string hash) => Path.Combine(instance.Layout.Data, "integrations", "provider-ingress", "1", hash, "bell-tower-ingress.exe");

    private static string Short(string? hash) => hash is { Length: >= 12 } ? hash[..12] : hash ?? "none";

    private static string Trim(string text) => text.Length > 160 ? text[..160] : text.Trim();
}
