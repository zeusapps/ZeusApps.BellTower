using System.Text;
using System.Text.Json;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>
/// Modeled provider transcripts: the record shapes the product's history recovery reads, labeled as models. They are not captured from
/// an authenticated provider run. The Claude shapes follow the observed 2.1.306 transcript envelope with Messages API tool_use and
/// tool_result blocks, and the three-field <c>custom-title</c> record from the isolated SDK probe; the Codex shapes follow the
/// rollout <c>timestamp/type/payload</c> envelope with a freeform <c>apply_patch</c> call and its success header (openai/codex
/// rust-v0.164.0 sources). Provenance and caveats are in <c>tests/BellTower.Tests/Fixtures/identity/provenance.json</c>; a unit test pins
/// these builders to the test fixture builders so the two cannot drift apart.
/// </summary>
public static class TranscriptModel
{
    public static string Timestamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);

    public static string Slashed(string path) => path.Replace('\\', '/');

    private static string Line(object value) => JsonSerializer.Serialize(value);

    // ---- Claude Code ---------------------------------------------------------------------------------------------

    /// <summary>The assistant tool_use record of a Write into a change folder.</summary>
    public static string ClaudeWriteUse(string session, string toolUseId, string checkout, string change, string timestamp) => Line(new Dictionary<string, object?>
    {
        ["isSidechain"] = false,
        ["type"] = "assistant",
        ["message"] = new { role = "assistant", content = new[] { new { type = "tool_use", id = toolUseId, name = "Write", input = (object)new { file_path = Slashed(Path.Combine(checkout, "openspec", "changes", change, "proposal.md")), content = "x" } } } },
        ["timestamp"] = timestamp,
        ["cwd"] = Slashed(checkout),
        ["sessionId"] = session
    });

    /// <summary>The user tool_result record that reports the tool call succeeded.</summary>
    public static string ClaudeResult(string session, string toolUseId, string timestamp) => Line(new Dictionary<string, object?>
    {
        ["isSidechain"] = false,
        ["type"] = "user",
        ["message"] = new { role = "user", content = new[] { new { type = "tool_result", tool_use_id = toolUseId, content = "ok" } } },
        ["timestamp"] = timestamp,
        ["sessionId"] = session
    });

    /// <summary>The native custom-title record a user /rename leaves in the transcript.</summary>
    public static string ClaudeCustomTitle(string session, string title) => Line(new { type = "custom-title", customTitle = title, sessionId = session });

    // ---- Codex ---------------------------------------------------------------------------------------------------

    /// <summary>The custom_tool_call record of a freeform apply_patch that adds a change folder file.</summary>
    public static string CodexPatchUse(string callId, string checkout, string change, string timestamp) => CodexLine(timestamp, new
    {
        type = "custom_tool_call",
        name = "apply_patch",
        input = "*** Begin Patch\n*** Add File: " + Slashed(Path.Combine(checkout, "openspec", "changes", change, "proposal.md")) + "\n+x\n*** End Patch",
        call_id = callId
    });

    /// <summary>The custom_tool_call_output record with the success header Codex prints for a freeform apply_patch.</summary>
    public static string CodexPatchResult(string callId, string timestamp) => CodexLine(timestamp, new
    {
        type = "custom_tool_call_output",
        call_id = callId,
        output = "Exit code: 0\nWall time: 0.1 seconds\nOutput:\nSuccess. Updated the following files:\nA x\n"
    });

    private static string CodexLine(string timestamp, object payload) => JsonSerializer.Serialize(new { timestamp, type = "response_item", payload });

    public static void Write(string path, IEnumerable<string> lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
    }

    public static void Append(string path, IEnumerable<string> lines) =>
        File.AppendAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
}
