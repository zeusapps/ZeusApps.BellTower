using System.Text;
using System.Text.Json;
using BellTower.Core;

namespace BellTower.Tests.Testing;

// Loads the labeled identity fixtures (redacted or synthetic, never live provider trials) and builds transcripts.
public static class IdentityFixtures
{
    public static string Slashed(string path) => path.Replace('\\', '/');

    public static string Load(string name, string root) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "identity", name)).Replace("{ROOT}", Slashed(root));

    public static SessionState Session(string provider, string id, string directory) => new()
    {
        Key = provider + ":" + id,
        Provider = provider,
        SessionId = id,
        AttachmentId = "attachment-" + id,
        Directory = directory
    };

    public static string Change(string root, string name, bool scaffold = false)
    {
        var directory = Path.Combine(root, "openspec", "changes", name);
        Directory.CreateDirectory(directory);
        if (scaffold)
        {
            File.WriteAllText(Path.Combine(directory, ".openspec.yaml"), "schema: spec-driven\n");
        }
        return directory;
    }

    public static string Timestamp(int seconds) => new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero).AddSeconds(seconds)
        .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    // Claude transcript lines (observed 2.1.306 envelope with Messages API content blocks).
    public static class Claude
    {
        private static string Line(object value) => JsonSerializer.Serialize(value);

        public static string Use(string session, string id, string tool, object input, string cwd, int at, bool sidechain = false) => Line(new Dictionary<string, object?>
        {
            ["isSidechain"] = sidechain,
            ["agentId"] = sidechain ? "agent-sub" : null,
            ["type"] = "assistant",
            ["message"] = new { role = "assistant", content = new[] { new { type = "tool_use", id, name = tool, input } } },
            ["timestamp"] = Timestamp(at),
            ["cwd"] = Slashed(cwd),
            ["sessionId"] = session
        }.Where(pair => pair.Value is not null).ToDictionary());

        public static string Result(string session, string id, int at, bool error = false, string text = "ok", bool sidechain = false) => Line(new Dictionary<string, object?>
        {
            ["isSidechain"] = sidechain,
            ["agentId"] = sidechain ? "agent-sub" : null,
            ["type"] = "user",
            ["message"] = new { role = "user", content = new[] { new { type = "tool_result", tool_use_id = id, is_error = error ? (bool?)true : null, content = text } } },
            ["timestamp"] = Timestamp(at),
            ["sessionId"] = session
        }.Where(pair => pair.Value is not null).ToDictionary());

        public static string Write(string session, string id, string root, string change, int at) =>
            Use(session, id, "Write", new { file_path = Slashed(Path.Combine(root, "openspec", "changes", change, "proposal.md")), content = "x" }, root, at);

        public static string Title(string session, string title) => Line(new { type = "custom-title", customTitle = title, sessionId = session });

        // A conversational record that history recovery must skip cheaply.
        public static string Chatter(string session, int at, int bytes) => Line(new
        {
            isSidechain = false,
            type = "assistant",
            message = new { role = "assistant", content = new[] { new { type = "text", text = new string('x', bytes) } } },
            timestamp = Timestamp(at),
            sessionId = session
        });
    }

    // Codex rollout lines (timestamp/type/payload envelope).
    public static class Codex
    {
        private static string Line(int at, object payload) => JsonSerializer.Serialize(new { timestamp = Timestamp(at), type = "response_item", payload });

        public static string Exec(string id, string command, int at, string? workdir = null) => Line(at, new
        {
            type = "function_call",
            name = "exec_command",
            arguments = JsonSerializer.Serialize(workdir is null ? (object)new { cmd = command } : new { cmd = command, workdir = Slashed(workdir) }),
            call_id = id
        });

        // Codex serializes the output as text or as a content-item array; both shapes are accepted.
        public static string ExecResult(string id, int at, int exit = 0, string output = "", bool items = false)
        {
            var text = "Chunk ID: 1\nWall time: 0.1000 seconds\nProcess exited with code " + exit + "\nOutput:\n" + output;
            return Line(at, new { type = "function_call_output", call_id = id, output = items ? (object)new[] { new { type = "input_text", text } } : text });
        }

        public static string Patch(string id, string root, string change, int at) => Line(at, new
        {
            type = "custom_tool_call",
            name = "apply_patch",
            input = "*** Begin Patch\n*** Add File: " + Slashed(Path.Combine(root, "openspec", "changes", change, "proposal.md")) + "\n+x\n*** End Patch",
            call_id = id
        });

        // The freeform apply_patch result as asserted by openai/codex rust-v0.164.0 (core/tests/suite/apply_patch_cli.rs).
        public static string PatchResult(string id, int at, string? text = null, bool items = false)
        {
            var output = text ?? "Exit code: 0\nWall time: 0.1 seconds\nOutput:\nSuccess. Updated the following files:\nA x\n";
            return Line(at, new { type = "custom_tool_call_output", call_id = id, output = items ? (object)new[] { new { type = "input_text", text = output } } : output });
        }

        public static string Chatter(int at, int bytes) => Line(at, new { type = "message", role = "assistant", content = new[] { new { type = "output_text", text = new string('x', bytes) } } });
    }

    public static void Write(string path, params string[] lines) => File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
    public static void Append(string path, params string[] lines) => File.AppendAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
}
