using BellTower.IO;
using Xunit;

namespace BellTower.Tests.IO;

public sealed class TrayInvocationCommandTests
{
    private const string Executable = @"C:\Program Files\O'Brien Bell Tower\bell-tower.exe";
    private const string DataDirectory = @"D:\Data With Spaces\Mária and O'Brien";
    private const string OtherDataDirectory = @"D:\Other Data";

    [Theory]
    [InlineData("{0} tray --data-dir {1}")]
    [InlineData("{0} --data-dir {1} tray --silent")]
    [InlineData("{0} \"tray\" --data-dir={1}")]
    [InlineData("{0} --data-dir={1} \"tray\"")]
    public void ClassifiesTrayWithQuotedVerbAndRecursiveDataOption(string template)
    {
        var commandLine = string.Format(template, Quote(Executable), Quote(DataDirectory));

        Assert.Equal(TrayInvocationKind.ThisDataDirectory, TrayInvocationCommand.Classify(commandLine, Executable, DataDirectory));
    }

    [Fact]
    public void ClassifiesRootDefaultActionAsTray()
    {
        Assert.Equal(TrayInvocationKind.ThisDataDirectory,
            TrayInvocationCommand.Classify($"{Quote(Executable)} --data-dir {Quote(DataDirectory)}", Executable, DataDirectory));
    }

    [Fact]
    public void ClassifiesKnownNonTrayCommandWithoutMatchingArgumentText()
    {
        var commandLine = $"{Quote(Executable)} --data-dir {Quote(DataDirectory)} event --provider claude --session-id tray";

        Assert.Equal(TrayInvocationKind.NotTray, TrayInvocationCommand.Classify(commandLine, Executable, DataDirectory));
    }

    [Fact]
    public void IdentifiesTrayForAnotherExplicitDataDirectoryWithoutAdoptingIt()
    {
        var commandLine = $"{Quote(Executable)} tray --data-dir {Quote(OtherDataDirectory)}";

        Assert.Equal(TrayInvocationKind.OtherDataDirectory, TrayInvocationCommand.Classify(commandLine, Executable, DataDirectory));
    }

    [Theory]
    [InlineData("{0} tray")]
    [InlineData("{0} tray --data-dir")]
    [InlineData("{0} tray --data-dir {1} --data-dir {2}")]
    [InlineData("{0} tray --unknown")]
    [InlineData("{0} unknown --data-dir {1}")]
    public void RefusesUnknownOrAmbiguousTrayArguments(string template)
    {
        var commandLine = string.Format(template, Quote(Executable), Quote(DataDirectory), Quote(OtherDataDirectory));

        Assert.Throws<InvalidDataException>(() => TrayInvocationCommand.Classify(commandLine, Executable, DataDirectory));
    }

    [Fact]
    public void RefusesCommandLineWhoseArgumentZeroDiffersFromLiveImage()
    {
        var commandLine = $"{Quote(OtherDataDirectory + "\\bell-tower.exe")} tray --data-dir {Quote(DataDirectory)}";

        Assert.Throws<InvalidDataException>(() => TrayInvocationCommand.Classify(commandLine, Executable, DataDirectory));
    }

    [Theory]
    [InlineData("../data")]
    [InlineData("D:data")]
    [InlineData("\\data")]
    public void RefusesDataDirectoryWithoutAnUnambiguousAbsoluteRoot(string dataDirectory)
    {
        var commandLine = $"{Quote(Executable)} tray --data-dir {Quote(dataDirectory)}";

        Assert.Throws<InvalidDataException>(() => TrayInvocationCommand.Classify(commandLine, Executable, DataDirectory));
    }

    [Fact]
    public void WindowsArgvParserPreservesQuotedTrailingBackslash()
    {
        var directoryWithTrailingSlash = DataDirectory + "\\";
        var commandLine = $"{Quote(Executable)} tray --data-dir {Quote(directoryWithTrailingSlash)}";

        Assert.Equal(TrayInvocationKind.ThisDataDirectory,
            TrayInvocationCommand.Classify(commandLine, Executable, DataDirectory));
    }

    private static string Quote(string value)
    {
        var result = new System.Text.StringBuilder(value.Length + 2).Append('"');
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\')
            {
                slashes++;
                continue;
            }

            if (character == '"')
            { result.Append('\\', (slashes * 2) + 1).Append('"'); }
            else
            { result.Append('\\', slashes).Append(character); }
            slashes = 0;
        }

        result.Append('\\', slashes * 2).Append('"');
        return result.ToString();
    }
}
