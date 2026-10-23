using System.Diagnostics;
using System.Text;
using BellTower.Tests.Testing;
using Xunit;

namespace BellTower.Tests.Scripts;

public sealed class ReviewIdentityScriptTests
{
    [Fact]
    public void ReportsTrackedStagedUntrackedAndDeletedChangesWithStableOutput()
    {
        using var fixture = new Fixture();
        foreach (var name in new[] { "modified.txt", "staged.txt", "deleted.txt", "outside.txt" })
        {
            fixture.Write(name, name + " original\n");
        }

        var baseline = fixture.Commit();

        var clean = fixture.Run(baseline, null, "modified.txt", "staged.txt", "deleted.txt");
        Assert.Contains("HEAD: " + baseline, clean);
        Assert.Empty(ManifestOf(clean));
        Assert.Contains("files=0 ", clean);

        fixture.Write("modified.txt", "modified.txt changed\n");
        fixture.Write("staged.txt", "staged.txt changed\n");
        fixture.Git("add", "staged.txt");
        File.Delete(Path.Combine(fixture.Repository, "deleted.txt"));
        fixture.Write("new.txt", "untracked content\n");
        fixture.Write("outside.txt", "outside.txt changed\n");

        var paths = new[] { "modified.txt", "staged.txt", "deleted.txt", "new.txt" };
        var first = fixture.Run(baseline, null, paths);
        var second = fixture.Run(baseline, null, paths);
        Assert.Equal(first, second);
        Assert.Equal(["  ? new.txt", "  D deleted.txt", "  M modified.txt", "  M staged.txt"], ManifestOf(first));
        Assert.DoesNotContain("outside.txt", first);
        Assert.Contains("files=4 ", first);
        Assert.Matches(@"  [0-9a-f]{64}  new\.txt", first);
        Assert.NotEqual(clean, first);

        fixture.Write("modified.txt", "modified.txt changed again\n");
        var tracked = fixture.Run(baseline, null, paths);
        Assert.NotEqual(DiffLine(first), DiffLine(tracked));
        Assert.Equal(UntrackedLines(first), UntrackedLines(tracked));

        fixture.Write("new.txt", "untracked content changed\n");
        var untracked = fixture.Run(baseline, null, paths);
        Assert.Equal(DiffLine(tracked), DiffLine(untracked));
        Assert.NotEqual(UntrackedLines(tracked), UntrackedLines(untracked));
    }

    [Fact]
    public void AcceptsSpaceSeparatedAndCommaJoinedPathsIdentically()
    {
        using var fixture = new Fixture();
        fixture.Write("a.txt", "a\n");
        fixture.Write("b.txt", "b\n");
        var baseline = fixture.Commit();
        fixture.Write("a.txt", "a changed\n");
        fixture.Write("b.txt", "b changed\n");

        var spaced = fixture.RunFile("-Baseline", baseline, "-Paths", "a.txt", "b.txt");
        var comma = fixture.RunFile("-Baseline", baseline, "-Paths", "a.txt,b.txt");
        var positional = fixture.RunFile("-Baseline", baseline, "a.txt", "b.txt");
        Assert.Contains("files=2 ", spaced);
        Assert.Equal(spaced, comma);
        Assert.Equal(spaced, positional);

        // In-process invocation, captured from the success pipeline and compared with Compare-Object.
        var command = $"$a = & '{fixture.Script}' -Baseline {baseline} -Paths a.txt b.txt; "
            + $"$b = & '{fixture.Script}' -Baseline {baseline} -Paths a.txt,b.txt; "
            + "if (Compare-Object $a $b) { exit 3 }; $a";
        var inProcess = fixture.RunCommand(command);
        Assert.Equal(spaced, inProcess);
    }

    [Fact]
    public void ReportsStagedNewFileAsAdded()
    {
        using var fixture = new Fixture();
        fixture.Write("existing.txt", "existing\n");
        var baseline = fixture.Commit();
        fixture.Write("added.txt", "added\n");
        fixture.Git("add", "added.txt");

        var output = fixture.Run(baseline, null, "added.txt");
        Assert.Equal(["  A added.txt"], ManifestOf(output));
        Assert.Contains("files=1 ", output);
        Assert.Contains("untracked=0/", output);
    }

    [Fact]
    public void FailsWhenAPathMatchesNothingButAcceptsPathsDeletedSinceTheBaseline()
    {
        using var fixture = new Fixture();
        fixture.Write("gone.txt", "gone\n");
        var baseline = fixture.Commit();
        fixture.Git("rm", "-q", "gone.txt");
        fixture.Git("commit", "-q", "-m", "remove");

        var deleted = fixture.Run(baseline, null, "gone.txt");
        Assert.Equal(["  D gone.txt"], ManifestOf(deleted));

        var result = fixture.Execute(fixture.Shell, fixture.Repository, fixture.FileArguments("-Baseline", baseline, "-Paths", "gone.txt", "typo.txt"));
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("typo.txt", result.Error);
        Assert.Contains("matches nothing", result.Error);
        Assert.DoesNotContain("HEAD:", result.Output);
    }

    [Fact]
    public void ResolvesPathsRelativeToTheCallerDirectoryFromASubdirectory()
    {
        using var fixture = new Fixture();
        fixture.Write("sub/inner.txt", "inner\n");
        fixture.Write("top.txt", "top\n");
        var baseline = fixture.Commit();
        fixture.Write("sub/inner.txt", "inner changed\n");
        fixture.Write("top.txt", "top changed\n");
        var subdirectory = Path.Combine(fixture.Repository, "sub");

        var relative = fixture.Run(baseline, subdirectory, "inner.txt");
        Assert.Equal(["  M sub/inner.txt"], ManifestOf(relative));

        var parent = fixture.Run(baseline, subdirectory, "../top.txt", "inner.txt");
        Assert.Equal(["  M sub/inner.txt", "  M top.txt"], ManifestOf(parent));
        Assert.Equal(fixture.Run(baseline, null, "sub/inner.txt", "top.txt"), parent);

        var failure = fixture.Execute(fixture.Shell, subdirectory, fixture.FileArguments("-Baseline", baseline, "-Paths", "top.txt"));
        Assert.NotEqual(0, failure.ExitCode);
        Assert.Contains("matches nothing", failure.Error);
    }

    [Fact]
    public void ReportsARenameAsDeletePlusAddWithAStableDiff()
    {
        using var fixture = new Fixture();
        fixture.Write("old-name.txt", "line one\nline two\nline three\nline four\n");
        var baseline = fixture.Commit();
        fixture.Git("mv", "old-name.txt", "new-name.txt");
        fixture.Write("new-name.txt", "line one\nline two\nline three\nline four changed\n");

        var first = fixture.Run(baseline, null, "old-name.txt", "new-name.txt");
        Assert.Equal(["  A new-name.txt", "  D old-name.txt"], ManifestOf(first));
        Assert.Contains("files=2 ", first);
        Assert.Equal(first, fixture.Run(baseline, null, "old-name.txt", "new-name.txt"));
        Assert.Matches(@"^Tracked diff SHA-256: [0-9a-f]{64}$", DiffLine(first));
    }

    [Fact]
    public void KeepsTheDiffHashStableAcrossUserDiffSettings()
    {
        using var fixture = new Fixture();
        fixture.Write("code.txt", string.Join('\n', Enumerable.Range(1, 40).Select(n => "line " + n)) + "\n");
        var baseline = fixture.Commit();
        var lines = File.ReadAllLines(Path.Combine(fixture.Repository, "code.txt"));
        lines[3] = "line 4 changed";
        lines[4] = string.Empty;
        lines[12] = "line 13 changed";
        fixture.Write("code.txt", string.Join('\n', lines) + "\n");

        var expected = DiffLine(fixture.Run(baseline, null, "code.txt"));
        var settings = new[]
        {
            ("diff.algorithm", "patience"),
            ("diff.context", "7"),
            ("diff.interHunkContext", "5"),
            ("diff.suppressBlankEmpty", "true"),
            ("diff.indentHeuristic", "false"),
            ("diff.noprefix", "true"),
            ("diff.renames", "copies")
        };
        foreach (var (key, value) in settings)
        {
            var output = Fixture.Succeed(fixture.Execute(
                fixture.Shell,
                fixture.Repository,
                fixture.FileArguments("-Baseline", baseline, "-Paths", "code.txt"),
                new Dictionary<string, string>
                {
                    ["GIT_CONFIG_COUNT"] = "1",
                    ["GIT_CONFIG_KEY_0"] = key,
                    ["GIT_CONFIG_VALUE_0"] = value
                }));
            Assert.Equal(expected, DiffLine(output));
        }
    }

    [Fact]
    public void ResolvesAWrongCaseFileAndDirectoryToTheFileSystemSpelling()
    {
        using var fixture = new Fixture();
        fixture.Git("config", "core.ignorecase", "true");
        fixture.Write("Docs/Guide.md", "guide\n");
        fixture.Write("Other.txt", "other\n");
        var baseline = fixture.Commit();
        fixture.Write("Docs/Guide.md", "guide changed\n");
        fixture.Write("Other.txt", "other changed\n");

        var correct = fixture.Run(baseline, null, "Docs", "Other.txt");
        Assert.Equal(["  M Docs/Guide.md", "  M Other.txt"], ManifestOf(correct));

        var wrongCase = fixture.Run(baseline, null, "dOCS", "oTHER.TXT");
        Assert.Equal(correct, wrongCase);

        var wrongCaseNested = fixture.Run(baseline, null, "docs/guide.MD");
        Assert.Equal(["  M Docs/Guide.md"], ManifestOf(wrongCaseNested));
        Assert.Contains("Paths: Docs/Guide.md", wrongCaseNested);
    }

    private static string[] ManifestOf(string output) => output.Split('\n')
        .SkipWhile(line => !line.StartsWith("Manifest", StringComparison.Ordinal))
        .Skip(1)
        .TakeWhile(line => line.StartsWith("  ", StringComparison.Ordinal))
        .ToArray();

    private static string DiffLine(string output) => output.Split('\n').Single(line => line.StartsWith("Tracked diff", StringComparison.Ordinal));

    private static string[] UntrackedLines(string output) => output.Split('\n')
        .SkipWhile(line => !line.StartsWith("Untracked", StringComparison.Ordinal))
        .Skip(1)
        .TakeWhile(line => line.StartsWith("  ", StringComparison.Ordinal))
        .ToArray();

    private sealed record ProcessResult(int ExitCode, string Output, string Error);

    private sealed class Fixture : IDisposable
    {
        private readonly TestWorkspace workspace = new();
        private readonly string globalConfig;

        public Fixture()
        {
            Repository = Path.Combine(workspace.Root, "repo");
            Directory.CreateDirectory(Repository);
            globalConfig = Path.Combine(workspace.Root, "empty.gitconfig");
            File.WriteAllText(globalConfig, string.Empty);
            Script = Path.Combine(ProjectRoot(), "scripts", "review-identity.ps1");
            Git("init", "-q");
            Git("config", "user.name", "Review Identity Test");
            Git("config", "user.email", "review-identity@example.invalid");
            Git("config", "core.autocrlf", "false");
            Git("config", "commit.gpgsign", "false");
        }

        public string Repository { get; }

        public string Script { get; }

        public string Shell
        {
            get
            {
                var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
                return File.Exists(installed) ? installed : "pwsh";
            }
        }

        public void Write(string relative, string content)
        {
            var path = Path.Combine(Repository, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public string Commit()
        {
            Git("add", ".");
            Git("commit", "-q", "-m", "baseline");
            return Git("rev-parse", "HEAD").Trim();
        }

        public string Git(params string[] arguments) => Succeed(Execute("git", Repository, arguments), "git");

        public string Run(string baseline, string? workingDirectory, params string[] paths)
        {
            var arguments = FileArguments(["-Baseline", baseline, "-Paths", .. paths]);
            return Succeed(Execute(Shell, workingDirectory ?? Repository, arguments), "review-identity");
        }

        public string RunFile(params string[] scriptArguments) =>
            Succeed(Execute(Shell, Repository, FileArguments(scriptArguments)), "review-identity");

        public string RunCommand(string command) => Succeed(
            Execute(Shell, Repository, ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command]),
            "review-identity");

        public string[] FileArguments(params string[] scriptArguments) =>
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Script, .. scriptArguments];

        public ProcessResult Execute(
            string executable,
            string workingDirectory,
            IEnumerable<string> arguments,
            IReadOnlyDictionary<string, string>? environment = null)
        {
            var start = new ProcessStartInfo(executable)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            start.Environment["GIT_CONFIG_GLOBAL"] = globalConfig;
            start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            start.Environment["GIT_CONFIG_SYSTEM"] = globalConfig;
            foreach (var (key, value) in environment ?? new Dictionary<string, string>())
            {
                start.Environment[key] = value;
            }

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(60_000), "The process did not finish.");
            return new ProcessResult(
                process.ExitCode,
                output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal),
                error.GetAwaiter().GetResult());
        }

        public void Dispose()
        {
            // Git marks object files read-only, which blocks the workspace cleanup.
            foreach (var file in Directory.EnumerateFiles(Repository, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            workspace.Dispose();
        }

        public static string Succeed(ProcessResult result, string name = "review-identity")
        {
            Assert.True(result.ExitCode == 0, $"{name} failed: {result.Error}");
            return result.Output;
        }

        private static string ProjectRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ZeusApps.BellTower.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Project root missing.");
        }
    }
}
