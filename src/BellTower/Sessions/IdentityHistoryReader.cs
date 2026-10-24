using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BellTower.Core;
using BellTower.IO;
using BellTower.Providers;

namespace BellTower.Sessions;

// Reads provider-owned history without executing or copying transcript bodies. Two cursors keep recovery
// complete while the file grows: HistoryOffset walks older chunks back to the start across ticks, and
// HistoryLength follows tail growth separately, so no region is skipped. Only derived metadata is saved.
public sealed partial class IdentityHistoryReader(DataPaths paths)
{
    public const int ByteBudget = 256 * 1024;
    public const int LineLimit = ByteBudget;
    public const int PendingLimit = 64;
    public const int ResultLimit = 64;
    public const int CheckpointBytes = 64 * 1024;

    // Honest adapter status for diagnostics. Nothing here is a provider restriction.
    public const string Capabilities =
        "claude: verified custom-title record and tool_use/tool_result pairs (Claude 2.1.306 envelope fixture, Messages API content blocks); " +
        "codex: verified response_item function_call(exec_command)/function_call_output with 'Process exited with code N' header (openai/codex primary sources; no live capture yet); " +
        "codex: verified custom_tool_call(apply_patch)/custom_tool_call_output with the 'Exit code: 0 ... Success. Updated the following files:' result (openai/codex rust-v0.164.0 sources and tests; no live capture yet); " +
        "codex native title in history: not persisted in rollouts at 0.164.0 (kept in session_index.jsonl, an internal file), so it is read only through the thread name service for managed runtimes, unverified for unmanaged sessions; " +
        "codex shell tools other than exec_command: not registered at 0.164.0, unverified for other versions; " +
        "codex hooks: PostToolUse fires only for a successful tool result at 0.164.0 (core/src/tools/registry.rs success gate), so a failed apply_patch produces no live hook evidence; " +
        "claude fork: SDK forkSession rewrites sessionId (SDK 0.3.309 probe), interactive --fork-session and /branch unverified; " +
        "codex fork: `codex fork` threads record session_meta.forked_from_id (rust-v0.164.0 protocol, rollout recorder and thread manager sources; no live capture); " +
        "codex fork boundary: copied parent records end at the first thread_settings_applied event owned by the fork thread (rust-v0.164.0 sources, synthetic fixture); a rollout without it uses the branch-invalidation floor only, unverified";

    // The suffix the Agent SDK adds to the title of a forked conversation. It is generated text, not a user rename.
    public const string ForkSuffix = " (fork)";

    private sealed record Harvest(List<(long Offset, string Title)> Titles, List<HistoryWork> Calls, List<HistoryResult> Results)
    {
        // Parent conversation proven by this transcript's own forkedFrom records (Claude).
        public string? ForkParent { get; set; }
        // Codex: offset of this thread's first own thread-settings record, the end of the copied parent prefix.
        public long ForkBoundary { get; set; } = -1;
    }

    private sealed record Window(List<(long Offset, byte[] Bytes)> Lines, long NextBack, long NextForward);

    public bool Refresh(SessionState session)
    {
        if (session.Provider is not ("claude" or "codex"))
        { return false; }
        var store = new IdentityStore(paths);
        var saved = store.Get(session.Key, session.Directory);
        if (saved.TranscriptPath is not { } path)
        { return false; }
        Harvest forward = new([], [], []);
        Harvest backward = new([], [], []);
        string fingerprint;
        long newForward;
        long newBack;
        bool fresh;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = stream.Length;
            fingerprint = Fingerprint(stream, path);
            fresh = saved.HistoryFingerprint is null || saved.HistoryFingerprint != fingerprint || saved.HistoryLength > length;
            var front = fresh ? 0 : saved.HistoryLength;
            var back = fresh ? length : saved.HistoryOffset;
            newForward = front;
            newBack = back;
            if (!fresh && length == front && (back == 0 || Satisfied(saved, session.Provider)))
            { return false; }
            var budget = ByteBudget;
            if (fresh)
            {
                // First look at the newest chunk; it seeds both cursors.
                var window = Read(stream, Math.Max(0, length - budget), length, fromBoundary: false);
                (newForward, newBack) = (window.NextForward, window.NextBack);
                Collect(session, window, forward);
                // The Codex fork proof is the first rollout line, which a long transcript's newest window does not reach.
                if (session.Provider == "codex" && length > ByteBudget && newBack > 0)
                {
                    var head = new Harvest([], [], []);
                    Collect(session, Read(stream, 0, ByteBudget, fromBoundary: true), head);
                    forward.ForkParent ??= head.ForkParent;
                }
            }
            else
            {
                if (length > front)
                {
                    var end = Math.Min(length, front + budget);
                    var window = Read(stream, front, end, fromBoundary: true);
                    newForward = window.NextForward;
                    // A full window without any newline is an oversized record: skip it so growth cannot stall recovery.
                    if (newForward == front && end - front >= budget)
                    { newForward = end; }
                    budget -= (int)(end - front);
                    Collect(session, window, forward);
                }
                if (back > 0 && budget > 0 && !Satisfied(saved, session.Provider))
                {
                    var window = Read(stream, Math.Max(0, back - budget), back, fromBoundary: false);
                    newBack = window.NextBack;
                    Collect(session, window, backward);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        var changed = false;
        var forkParent = forward.ForkParent ?? backward.ForkParent ?? (fresh ? null : saved.ForkParent);
        var parent = forkParent is not null && saved.ForkInheritedFrom != forkParent ? store.Find(session.Provider + ":" + forkParent) : null;
        store.Update(session.Key, session.Directory, value =>
        {
            if (value.TranscriptPath != path)
            { return false; }
            var before = Json.Encode(value);
            var oldForward = fresh ? 0 : value.HistoryLength;
            if (fresh)
            { HistoryState.Reset(value); }
            value.ForkParent = forkParent ?? value.ForkParent;
            foreach (var boundary in new[] { forward.ForkBoundary, backward.ForkBoundary }.Where(offset => offset >= 0))
            { value.ForkBoundaryOffset = value.ForkBoundaryOffset < 0 ? boundary : Math.Min(value.ForkBoundaryOffset, boundary); }
            if (parent is not null && value.ForkInheritedFrom != forkParent)
            { IdentityInheritance.FromParent(value, parent, forkParent!); }
            Merge(value, forward, backward, storeForwardResults: fresh);
            value.HistoryFingerprint = fingerprint;
            value.HistoryOffset = newBack;
            value.HistoryLength = newForward;
            if (Json.Encode(value) == before)
            { return false; }
            // Tail growth alone is not worth a write; the next pass re-reads the same bounded region idempotently.
            var cursorOnly = value.HistoryLength;
            value.HistoryLength = oldForward;
            if (!fresh && Json.Encode(value) == before && cursorOnly - oldForward < CheckpointBytes)
            { return false; }
            value.HistoryLength = cursorOnly;
            changed = true;
            return true;
        });
        return changed;
    }

    // Recovery is complete for work once strong history work is known (weak evidence never ends the walk); Claude
    // also needs its title record. Work before the last branch invalidation is filtered by WorkFloorAt, not by the walk.
    private static bool Satisfied(SessionIdentity identity, string provider) =>
        identity.HistoryWorkOffset >= 0 && !identity.WorkWeak &&
        (provider != "claude" || identity.NativeTitleOffset >= 0);

    private static string Fingerprint(FileStream stream, string path)
    {
        var head = new byte[(int)Math.Min(256, stream.Length)];
        stream.Position = 0;
        stream.ReadExactly(head);
        return File.GetCreationTimeUtc(path).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + ContentHash.Compute(Convert.ToBase64String(head))[..16];
    }

    // Reads [start, end). fromBoundary means start is a known record boundary; otherwise a leading partial
    // record belongs to an older chunk and is skipped. A trailing partial record is deferred to a later pass.
    private static Window Read(FileStream stream, long start, long end, bool fromBoundary)
    {
        var bytes = new byte[(int)(end - start)];
        stream.Position = start;
        stream.ReadExactly(bytes);
        var lines = new List<(long, byte[])>();
        var lineStart = 0;
        if (start > 0 && !fromBoundary)
        {
            var first = Array.IndexOf(bytes, (byte)'\n');
            lineStart = first < 0 ? bytes.Length : first + 1;
        }
        var firstLine = lineStart;
        var consumed = lineStart;
        for (var i = lineStart; i < bytes.Length; i++)
        {
            if (bytes[i] != '\n')
            { continue; }
            var length = i - lineStart;
            if (length is > 0 and <= LineLimit)
            { lines.Add((start + lineStart, bytes.AsSpan(lineStart, length).ToArray())); }
            lineStart = i + 1;
            consumed = lineStart;
        }
        var nextBack = start == 0 ? 0 : start + (firstLine >= bytes.Length ? 0 : firstLine);
        return new(lines, nextBack, start + consumed);
    }

    private void Collect(SessionState session, Window window, Harvest into)
    {
        foreach (var (offset, bytes) in window.Lines)
        {
            if (!Relevant(session.Provider, bytes))
            { continue; }
            try
            {
                using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 32 });
                if (session.Provider == "claude")
                { ReadClaude(session, document.RootElement, offset, into); }
                else
                { ReadCodex(session, document.RootElement, offset, into); }
            }
            catch (JsonException) { }
        }
    }

    private static bool Relevant(string provider, byte[] line) => provider == "claude"
        ? line.AsSpan().IndexOf("\"custom-title\""u8) >= 0 || line.AsSpan().IndexOf("\"forkedFrom\""u8) >= 0 || line.AsSpan().IndexOf("tool_use"u8) >= 0 || line.AsSpan().IndexOf("tool_result"u8) >= 0
        : line.AsSpan().IndexOf("\"function_call"u8) >= 0 || line.AsSpan().IndexOf("\"custom_tool_call"u8) >= 0 || line.AsSpan().IndexOf("\"forked_from_id\""u8) >= 0 || line.AsSpan().IndexOf("thread_settings_applied"u8) >= 0;

    private static void Merge(SessionIdentity value, Harvest forward, Harvest backward, bool storeForwardResults)
    {
        foreach (var (offset, title) in forward.Titles.Concat(backward.Titles))
        {
            if (offset <= value.NativeTitleOffset)
            { continue; }
            // The SDK's generated fork title is not a user title and has no effect at all: it neither clears an earlier
            // valid title nor moves the title cursor.
            if (title.EndsWith(ForkSuffix, StringComparison.Ordinal))
            { continue; }
            // Bell Tower's own titles and a title the user released with "use Bell Tower name" are not user overrides;
            // as the newest title they do replace an earlier user title in the conversation.
            // NativeNamePolicy.Ours is reserved for the deferred native-title write (sync-native-session-titles).
            var ignored = NativeNamePolicy.Ours(value, title) || value.NativeTitleReleased is not null && IdentityText.Optional(title) == value.NativeTitleReleased;
            value.NativeTitle = ignored ? null : IdentityText.Optional(title);
            value.NativeTitleSource = ignored ? null : HistoryState.ClaudeTitleSource;
            if (!ignored)
            { value.NativeTitleReleased = null; }
            value.NativeTitleOffset = offset;
        }

        // A proven Codex fork excludes the copied parent prefix from its own work evidence (its own first turn follows the
        // boundary), but only when a branch invalidation is known: copied records are restamped with the fork time, so the
        // floor cannot filter them. Without a known invalidation the copied history is ordered against inherited work by the
        // shared WorkPolicy rule exactly as a Claude fork's is (ANALYSIS-200, ANALYSIS-250).
        if (value.ForkParent is not null && value.ForkBoundaryOffset >= 0 && value.WorkFloorAt is not null)
        {
            value.HistoryCalls = value.HistoryCalls.Where(call => call.Offset >= value.ForkBoundaryOffset).ToList();
            forward.Calls.RemoveAll(call => call.Offset < value.ForkBoundaryOffset);
            backward.Calls.RemoveAll(call => call.Offset < value.ForkBoundaryOffset);
            // The boundary can be found after copied work was already applied (split read windows, late parent floor):
            // retract history-derived work that lies before it. Live work and the floor are untouched (QA-250).
            // Claude has no boundary; its copied records keep their original stamps, so the floor alone filters them.
            if (value.WorkSource?.StartsWith("history:", StringComparison.Ordinal) == true && value.HistoryWorkOffset >= 0 && value.HistoryWorkOffset < value.ForkBoundaryOffset)
            { WorkPolicy.Clear(value, WorkClear.History); }
        }

        var results = value.HistoryResults.Concat(forward.Results).Concat(backward.Results)
            .GroupBy(result => result.Operation).Select(group => group.First()).ToList();
        foreach (var call in forward.Calls)
        {
            if (value.HistoryCalls.All(existing => existing.Operation != call.Operation))
            { value.HistoryCalls.Add(call); }
        }
        var resolved = new List<HistoryWork>();
        var finished = new List<HistoryWork>();
        // Forward calls wait for later results; backward results always precede their call's reader pass,
        // so an unmatched backward call can never be resolved and is dropped.
        foreach (var call in value.HistoryCalls.Concat(backward.Calls).GroupBy(call => call.Operation).Select(group => group.First()))
        {
            var result = results.FirstOrDefault(item => item.Operation == call.Operation && item.Offset > call.Offset);
            if (result is null)
            { continue; }
            finished.Add(call);
            if (result.Ok && (call.Proof != "created" || result.Created))
            { resolved.Add(call); }
        }
        foreach (var call in resolved.OrderBy(call => call.Offset))
        {
            WorkPolicy.Apply(value, new(call.Change, call.Root, "history:" + call.Source, call.Weak, call.At, call.Offset));
        }
        var done = finished.Select(call => call.Operation).ToHashSet(StringComparer.Ordinal);
        value.HistoryCalls = value.HistoryCalls.Where(call => !done.Contains(call.Operation)).OrderBy(call => call.Offset).TakeLast(PendingLimit).ToList();
        // Only results near the unread boundary can still match older calls; keep the lowest offsets.
        var kept = (storeForwardResults ? forward.Results : []).Concat(backward.Results).Concat(value.HistoryResults).GroupBy(result => result.Operation).Select(group => group.First())
            .Where(result => !done.Contains(result.Operation)).OrderBy(result => result.Offset).Take(ResultLimit).ToList();
        value.HistoryResults = kept;
    }

    private static string? Text(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
        { return content.GetString(); }
        if (content.ValueKind != JsonValueKind.Array)
        { return null; }
        var builder = new StringBuilder();
        foreach (var part in content.EnumerateArray().Take(32))
        {
            if (HookInput.Text(part, "text") is { } text)
            { builder.Append(text).Append('\n'); }
            else if (part.ValueKind == JsonValueKind.String)
            { builder.Append(part.GetString()).Append('\n'); }
        }
        return builder.Length == 0 ? null : builder.ToString();
    }

    private static bool Skip(SessionState session, JsonElement record)
    {
        if (record.TryGetProperty("isSidechain", out var sidechain) && sidechain.ValueKind == JsonValueKind.True ||
            HookInput.Text(record, "agentId") is not null || HookInput.Text(record, "parent_tool_use_id") is not null ||
            HookInput.Text(record, "parent_agent_id") is not null)
        { return true; }
        // Forked or concatenated files may carry records of another conversation; those are not this session's evidence.
        var owner = HookInput.Text(record, "sessionId") ?? HookInput.Text(record, "session_id");
        return owner is not null && owner != session.SessionId;
    }

    private static DateTimeOffset At(JsonElement record) =>
        DateTimeOffset.TryParse(HookInput.Text(record, "timestamp"), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var timestamp) ? timestamp : DateTimeOffset.MinValue;

    private static void ReadClaude(SessionState session, JsonElement record, long offset, Harvest into)
    {
        if (Skip(session, record))
        { return; }
        var type = HookInput.Text(record, "type");
        if (record.TryGetProperty("forkedFrom", out var fork) && fork.ValueKind == JsonValueKind.Object &&
            HookInput.Text(fork, "sessionId") is { Length: > 0 } parent && parent != session.SessionId &&
            HookInput.Text(record, "sessionId") == session.SessionId)
        { into.ForkParent ??= parent; }
        if (type == "custom-title" && HookInput.Text(record, "customTitle") is { } title)
        {
            into.Titles.Add((offset, title));
            return;
        }
        if (!record.TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        { return; }
        var cwd = HookInput.Text(record, "cwd") ?? session.Directory;
        foreach (var block in content.EnumerateArray())
        {
            var kind = HookInput.Text(block, "type");
            if (kind == "tool_result" && HookInput.Text(block, "tool_use_id") is { } id)
            {
                var failed = block.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True;
                var text = block.TryGetProperty("content", out var body) ? Text(body) : null;
                into.Results.Add(new(ContentHash.Compute(id), offset, text?.Contains("Created change '", StringComparison.Ordinal) == true, !failed));
            }
            else if (kind == "tool_use" && HookInput.Text(block, "id") is { } operation && HookInput.Text(block, "name") is { } tool &&
                block.TryGetProperty("input", out var input))
            {
                AddCall(into, operation, tool, input, cwd, At(record), offset);
            }
        }
    }

    private static void ReadCodex(SessionState session, JsonElement record, long offset, Harvest into)
    {
        if (Skip(session, record) || !record.TryGetProperty("payload", out var payload))
        { return; }
        var recordType = HookInput.Text(record, "type");
        // The fork copies the parent's records, restamped with the fork time, and then appends one thread-settings snapshot owned by
        // the new thread; copied snapshots keep their original owner (rust-v0.164.0 protocol ThreadSettingsAppliedEvent, session init).
        // That first own snapshot ends the copied prefix.
        if (recordType == "event_msg" && HookInput.Text(payload, "type") == "thread_settings_applied")
        {
            if (HookInput.Text(payload, "thread_id") == session.SessionId && (into.ForkBoundary < 0 || offset < into.ForkBoundary))
            { into.ForkBoundary = offset; }
            return;
        }
        // A forked Codex thread records its parent in its own first rollout line (session_meta.forked_from_id,
        // openai/codex rust-v0.164.0 SessionMeta); parent_thread_id is a spawned sub-agent link, not a fork.
        if (recordType == "session_meta")
        {
            if (HookInput.Text(payload, "id") == session.SessionId && HookInput.Text(payload, "forked_from_id") is { Length: > 0 } parent && parent != session.SessionId)
            { into.ForkParent ??= parent; }
            return;
        }
        if (recordType != "response_item")
        { return; }
        var item = HookInput.Text(payload, "type");
        var operation = HookInput.Text(payload, "call_id");
        if (operation is null)
        { return; }
        if (item == "function_call_output")
        {
            // Codex reports completion as a text header; the hook tool_response carries no exit code.
            var text = payload.TryGetProperty("output", out var output) ? Text(output) : null;
            if (text is not null && ExitCode().Match(text) is { Success: true } exit)
            {
                into.Results.Add(new(ContentHash.Compute(operation), offset, text.Contains("Created change '", StringComparison.Ordinal), exit.Groups[1].Value == "0"));
            }
        }
        else if (item == "custom_tool_call_output")
        {
            // Only the exact success header Codex prints for a freeform apply_patch counts. Anything else (including a
            // verification failure, a non-zero exit or an unrecognized shape) discards the call; unknown is never success.
            var text = payload.TryGetProperty("output", out var output) ? Text(output) : null;
            into.Results.Add(new(ContentHash.Compute(operation), offset, false, text is not null && PatchApplied().IsMatch(text)));
        }
        else if (item == "custom_tool_call" && HookInput.Text(payload, "name") == "apply_patch" && HookInput.Text(payload, "input") is { } patch)
        {
            using var arguments = JsonDocument.Parse(JsonSerializer.Serialize(new { command = patch }));
            AddCall(into, operation, "apply_patch", arguments.RootElement, session.Directory, At(record), offset);
        }
        else if (item == "function_call" && HookInput.Text(payload, "name") == "exec_command" && HookInput.Text(payload, "arguments") is { } arguments)
        {
            try
            {
                using var parsed = JsonDocument.Parse(arguments);
                if (HookInput.Text(parsed.RootElement, "cmd") is not { } command)
                { return; }
                var workdir = HookInput.Text(parsed.RootElement, "workdir");
                var cwd = string.IsNullOrEmpty(workdir) ? session.Directory : Path.IsPathFullyQualified(workdir) ? workdir : Path.GetFullPath(workdir, session.Directory);
                using var shell = JsonDocument.Parse(JsonSerializer.Serialize(new { command }));
                AddCall(into, operation, "Bash", shell.RootElement, cwd, At(record), offset);
            }
            catch (Exception e) when (e is JsonException or ArgumentException or NotSupportedException) { }
        }
    }

    // Normalizes a native call through the same classifier as live hooks (one policy, two adapters).
    private static void AddCall(Harvest into, string operation, string tool, JsonElement args, string cwd, DateTimeOffset at, long offset)
    {
        using var input = JsonDocument.Parse(JsonSerializer.Serialize(new
        { hook_event_name = "PostToolUse", tool_name = tool, tool_use_id = operation, cwd, tool_input = args, tool_response = new { } }));
        if (OpenSpecSelectionReader.Read(input.RootElement) is not { } candidate)
        { return; }
        var root = candidate.WorkRoot ?? ScaffoldLocator.Root(candidate.Directory ?? cwd, candidate.Change, candidate.Redirected ? at : null);
        if (root is null || !Directory.Exists(Path.Combine(root, "openspec", "changes", candidate.Change)))
        { return; }
        var proof = candidate.Verb == "new" && !candidate.Redirected ? "created" : "result";
        into.Calls.Add(new(ContentHash.Compute(operation), candidate.Change, root, candidate.Verb, at, offset, candidate.Weak, proof));
    }

    [GeneratedRegex(@"\AExit code: 0\r?\nWall time: [0-9.]+ seconds\r?\n(?:Total output lines: [0-9]+\r?\n)?Output:\r?\nSuccess\. Updated the following files:\r?\n", RegexOptions.CultureInvariant)]
    private static partial Regex PatchApplied();

    [GeneratedRegex(@"^Process exited with code (-?\d+)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ExitCode();
}
