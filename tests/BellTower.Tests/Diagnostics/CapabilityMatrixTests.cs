using System.Text.Json;
using BellTower.Diagnostics;
using BellTower.IO;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Diagnostics;

// The capability matrix is the honest parity record shown by doctor and docs/compatibility.md: every surface states both
// providers, and a restriction is only allowed with the proof, fallback and regression evidence AGENTS.md requires.
public sealed class CapabilityMatrixTests
{
    private static readonly string[] Statuses = [CapabilityMatrix.Supported, CapabilityMatrix.Restricted, CapabilityMatrix.Unverified];

    [Fact]
    public void EverySurfaceStatesBothProvidersWithAKnownStatus()
    {
        Assert.NotEmpty(CapabilityMatrix.Rows);
        Assert.Equal(CapabilityMatrix.Rows.Count, CapabilityMatrix.Rows.Select(row => row.Surface).Distinct().Count());
        foreach (var row in CapabilityMatrix.Rows)
        {
            foreach (var provider in new[] { row.Claude, row.Codex })
            {
                Assert.Contains(provider.Status, Statuses);
                Assert.False(string.IsNullOrWhiteSpace(provider.Detail), row.Surface);
            }
        }
    }

    [Fact]
    public void ARestrictionNeverAppearsWithoutProofFallbackAndRegressionCoverage()
    {
        foreach (var row in CapabilityMatrix.Rows)
        {
            var restricted = row.Claude.Status == CapabilityMatrix.Restricted || row.Codex.Status == CapabilityMatrix.Restricted;
            if (restricted)
            {
                Assert.False(string.IsNullOrWhiteSpace(row.Proof), row.Surface);
                Assert.False(string.IsNullOrWhiteSpace(row.Fallback), row.Surface);
                Assert.False(string.IsNullOrWhiteSpace(row.Regression), row.Surface);
            }
            else
            {
                Assert.Null(row.Proof);
            }
        }
        // Unknown and unimplemented are never reported as restrictions: the unverified rows say what is missing.
        Assert.All(CapabilityMatrix.Rows.SelectMany(row => new[] { row.Claude, row.Codex }).Where(item => item.Status == CapabilityMatrix.Unverified),
            item => Assert.DoesNotContain("restriction", item.Detail.Replace("(not a provider restriction)", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DoctorReportsTheMatrixTheDeferredClaudeNativeWriteAndTheIdentityDocument()
    {
        using var workspace = new TestWorkspace();
        using var report = JsonDocument.Parse(Json.Encode(new DiagnosticsReporter(workspace.Paths).Report()));
        var root = report.RootElement;
        var matrix = root.GetProperty(Name(root, "capabilityMatrix"));
        Assert.Equal(CapabilityMatrix.Rows.Count, matrix.GetProperty(Name(matrix, "rows")).GetArrayLength());
        Assert.Contains("Claude Code", matrix.GetProperty(Name(matrix, "claude")).GetString());
        Assert.Contains("Codex", matrix.GetProperty(Name(matrix, "codex")).GetString());
        var claude = root.GetProperty(Name(root, "claude"));
        var naming = claude.GetProperty(Name(claude, "nameSynchronization")).GetString()!;
        Assert.Contains("sync-native-session-titles", naming);
        Assert.Contains("not a provider restriction", naming);
        Assert.DoesNotContain(claude.EnumerateObject(), property => property.Name.Contains("helper", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("docs/session-identity.md", root.GetProperty(Name(root, "identityDocumentation")).GetString());
    }

    // The report's property naming policy is a serializer detail; match it case-insensitively.
    private static string Name(JsonElement element, string wanted) =>
        element.EnumerateObject().Single(property => string.Equals(property.Name, wanted, StringComparison.OrdinalIgnoreCase)).Name;

    [Fact]
    public void TheClaudeNativeTitleWriteIsUnfinishedNotRestrictedAndTheCodexSkillRowIsUnverified()
    {
        var write = CapabilityMatrix.Rows.Single(row => row.Surface.StartsWith("Native chat-name write", StringComparison.Ordinal));
        Assert.Equal(CapabilityMatrix.Unverified, write.Claude.Status);
        Assert.Contains("sync-native-session-titles", write.Claude.Detail);
        Assert.Contains("not a provider restriction", write.Claude.Detail);
        Assert.Equal(CapabilityMatrix.Supported, write.Codex.Status);

        var skill = CapabilityMatrix.Rows.Single(row => row.Surface.Contains("workflow-skill", StringComparison.Ordinal));
        Assert.Equal(CapabilityMatrix.Supported, skill.Claude.Status);
        Assert.Equal(CapabilityMatrix.Unverified, skill.Codex.Status);
        Assert.Contains("UserPromptSubmit", skill.Codex.Detail);
        Assert.Null(skill.Proof);
        Assert.DoesNotContain(CapabilityMatrix.Rows, row => row.Claude.Status == CapabilityMatrix.Restricted || row.Codex.Status == CapabilityMatrix.Restricted);
    }
}
