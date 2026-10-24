using System.Security.Cryptography;
using System.Text.Json;
using BellTower.Core;
using BellTower.IO;
using BellTower.Setup;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Setup;

// Explicit legacy badge import (task 4.3): OSC rgb-to-hex, title as-is, no role parsing, unchanged source, readable contrast.
public sealed class LegacyImportTests
{
    [Theory]
    [InlineData("#1C2030", "#1c2030")]
    [InlineData("rgb:1c1c/2020/3030", "#1c2030")]
    [InlineData("rgb:ffff/0000/8080", "#ff0080")]
    [InlineData("rgb:f/0/8", "#ff0088")]
    [InlineData("rgb:ff/00/80", "#ff0080")]
    [InlineData("rgb:fff/000/800", "#ff0080")]
    [InlineData("rgb:0/0/0", "#000000")]
    [InlineData("\u001b]11;rgb:1c1c/2020/3030\a", "#1c2030")]
    [InlineData("\u001b]11;rgb:1c1c/2020/3030\u001b\\", "#1c2030")]
    [InlineData("\u001b]11;rgb:f/0/8\a", "#ff0088")]
    [InlineData("\u001b]11;rgb:FFFF/0000/8080\u001b\\", "#ff0080")]
    public void SupportedHistoricalBackgroundFormatsConvertToHex(string value, string expected) =>
        Assert.Equal(expected, LegacyImport.NormalizeBackground(value));

    [Theory]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#abc")]
    [InlineData("rgb:1/2")]
    [InlineData("rgb:12345/0/0")]
    [InlineData("rgb:gg/00/00")]
    [InlineData("rgb:1c1c/2020/3030 ")]
    [InlineData("rgb:1c1c/2020/3030\n")]
    [InlineData("xrgb:1/2/3")]
    [InlineData("\u001b]11;rgb:1c1c/2020/3030")]
    [InlineData("\u001b]11;?\a")]
    [InlineData("\u001b]11;rgb:1/2/3\a\u001b]52;c;QUJD\a")]
    [InlineData("\u001b]52;c;QUJD\a")]
    [InlineData("\u001b[31m")]
    [InlineData("\u001b]0;title\a")]
    [InlineData("\u001b]11;rgb:1/2/3\u001b\\\u001b\\")]
    [InlineData("\u001b]10;rgb:1/2/3\a")]
    public void ArbitraryEscapesAndMalformedColorsAreRejected(string value) =>
        Assert.Throws<InvalidDataException>(() => LegacyImport.NormalizeBackground(value));

    private static string WriteBadge(TestWorkspace workspace, object badge, string name = "badge.json")
    {
        var file = Path.Combine(workspace.Root, name);
        File.WriteAllText(file, JsonSerializer.Serialize(badge));
        return file;
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void ImportKeepsTheTitleAsIsParsesNoRoleKeepsTheSourceUnchangedAndEmitsNothing(string provider)
    {
        using var workspace = new TestWorkspace();
        var file = WriteBadge(workspace, new { title = "PM: ttyd-download-chips", background = "\u001b]11;rgb:1c1c/2020/3030\a", accent = "#4080C0" });
        var before = SHA256.HashData(File.ReadAllBytes(file));
        var stamp = File.GetLastWriteTimeUtc(file);
        var output = Console.Out;
        using var captured = new StringWriter();
        Console.SetOut(captured);
        SessionIdentity identity;
        try
        { identity = LegacyImport.ImportBadge(workspace.Paths, file, provider, "one", workspace.Root); }
        finally { Console.SetOut(output); }
        Assert.Empty(captured.ToString());
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(file)));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(file));
        Assert.Equal(provider + ":one", identity.Key);
        // Bell Tower has no session role: the imported title is the manual title as-is, with no role parsed.
        Assert.Null(identity.Role);
        Assert.Equal("PM: ttyd-download-chips", identity.ManualName);
        Assert.Equal("PM: ttyd-download-chips", identity.Name);
        Assert.Equal(new Palette("#1c2030", "#4080c0", identity.Palette.Foreground), identity.Palette);
        Assert.Equal("#4080c0", identity.ColorPin);
        Assert.Equal(identity.Palette, new IdentityStore(workspace.Paths).Get(identity.Key, workspace.Root).Palette);
    }

    [Theory]
    [InlineData("#4080c0")]
    [InlineData("#808080")]
    [InlineData("#777777")]
    [InlineData("#ffff00")]
    [InlineData("#0000ff")]
    [InlineData("#d84b4b")]
    [InlineData("#39a866")]
    [InlineData("#101010")]
    public void ImportedAccentsAlwaysGetAReadableForeground(string accent)
    {
        using var workspace = new TestWorkspace();
        var file = WriteBadge(workspace, new { title = "Task", background = "#102030", accent });
        var identity = LegacyImport.ImportBadge(workspace.Paths, file, "claude", "one", workspace.Root);
        Assert.True(Palette.Contrast(identity.Palette.Accent, identity.Palette.Foreground) >= 4.5, accent);
        Assert.Null(identity.Role);
    }

    [Fact]
    public void RejectedBadgesNeverCreateOrChangeAnIdentity()
    {
        using var workspace = new TestWorkspace();
        var bad = new object[]
        {
            new { title = "T", background = "\u001b]52;c;QUJD\a", accent = "#4080c0" },
            new { title = "T", background = "#102030", accent = "\u001b[31m" },
            new { title = "T", background = "#102030" },
            new { title = 5, background = "#102030", accent = "#4080c0" },
            new { title = "", background = "#102030", accent = "#4080c0" }
        };
        for (var index = 0; index < bad.Length; index++)
        {
            var file = WriteBadge(workspace, bad[index], "bad" + index + ".json");
            Assert.Throws<InvalidDataException>(() => LegacyImport.ImportBadge(workspace.Paths, file, "claude", "one", workspace.Root));
        }
        var notObject = Path.Combine(workspace.Root, "array.json");
        File.WriteAllText(notObject, "[1,2]");
        Assert.Throws<InvalidDataException>(() => LegacyImport.ImportBadge(workspace.Paths, notObject, "codex", "one", workspace.Root));
        var oversized = Path.Combine(workspace.Root, "large.json");
        File.WriteAllText(oversized, "{\"title\":\"" + new string('x', 70_000) + "\"}");
        Assert.Throws<InvalidDataException>(() => LegacyImport.ImportBadge(workspace.Paths, oversized, "codex", "one", workspace.Root));
        Assert.Throws<ArgumentException>(() => LegacyImport.ImportBadge(workspace.Paths, notObject, "other", "one", workspace.Root));
        Assert.Throws<ArgumentException>(() => LegacyImport.ImportBadge(workspace.Paths, notObject, "claude", " ", workspace.Root));
        Assert.Empty(Directory.GetFiles(workspace.Paths.Identities, "*.json"));
    }

    [Fact]
    public void ControlCharactersInTheTitleNeverSurviveAndLongTitlesParseNoRole()
    {
        using var workspace = new TestWorkspace();
        var file = WriteBadge(workspace, new { title = "\u001b]52;c;QUJD\a Fix the thing", background = "#102030", accent = "#4080c0" });
        var identity = LegacyImport.ImportBadge(workspace.Paths, file, "codex", "one", workspace.Root);
        Assert.DoesNotContain('\u001b', identity.Name);
        Assert.DoesNotContain('\a', identity.Name);
        var long_ = WriteBadge(workspace, new { title = "This is far too long to be a role: subject", background = "#102030", accent = "#4080c0" }, "long.json");
        Assert.Null(LegacyImport.ImportBadge(workspace.Paths, long_, "codex", "two", workspace.Root).Role);
    }
}
