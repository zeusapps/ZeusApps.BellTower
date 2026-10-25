using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace BellTower.Tests.Scripts;

// scripts/package.ps1 -PlanOnly and -CheckToolchain exercise version policy, dev numbering, build metadata and toolchain discovery
// without publishing anything. The real dev package build is recorded in the change evidence.
public sealed class PackageScriptTests : IDisposable
{
    private readonly string root = ProjectRoot();
    private readonly string output;
    private readonly string installation;

    public PackageScriptTests()
    {
        var id = Guid.NewGuid().ToString("N");
        output = Path.Combine(root, "artifacts", "dev-builds", "plan-test-" + id);
        installation = Path.Combine(root, "artifacts", "unit-tests", "plan-install-" + id);
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(installation);
    }

    public void Dispose()
    {
        foreach (var directory in new[] { output, installation })
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void DevNumberStartsInTheReservedRangeAboveTheBaseVersion()
    {
        var (major, minor) = BaseVersion();
        var plan = Plan();
        Assert.Equal($"{major}.{minor}.9001", plan.GetProperty("version").GetString());
        Assert.Equal(output, plan.GetProperty("outputDirectory").GetString());
    }

    [Fact]
    public void DevNumberSkipsVersionsInTheOutputFolderAndTheTargetInstallation()
    {
        var (major, minor) = BaseVersion();
        File.WriteAllText(Path.Combine(output, $"bell-tower-{major}.{minor}.9001-win-x64.zip"), "package");
        Directory.CreateDirectory(Path.Combine(installation, "versions", $"{major}.{minor}.9003"));
        Directory.CreateDirectory(Path.Combine(installation, "versions", $"{major}.{minor}.7"));
        Assert.Equal($"{major}.{minor}.9004", Plan().GetProperty("version").GetString());
    }

    [Fact]
    public void ExplicitDevNumbersAreValidatedAndMustBeFree()
    {
        var (major, minor) = BaseVersion();
        Directory.CreateDirectory(Path.Combine(installation, "versions", $"{major}.{minor}.9005"));
        Assert.Equal($"{major}.{minor}.9010", Plan("-DevVersion", $"{major}.{minor}.9010").GetProperty("version").GetString());
        Assert.Contains("already exists", Fail("-Dev", "-PlanOnly", "-DevVersion", $"{major}.{minor}.9005"));
        Assert.Contains("PATCH >= 9000", Fail("-Dev", "-PlanOnly", "-DevVersion", $"{major}.{minor}.8999"));
        Assert.Contains("PATCH >= 9000", Fail("-Dev", "-PlanOnly", "-DevVersion", $"{major}.{minor + 1}.9001"));
        Assert.Contains("needs -Dev", Fail("-PlanOnly", "-DevVersion", $"{major}.{minor}.9010"));
    }

    [Fact]
    public void StablePackagingRefusesTheReservedRangeBeforeBuildingAnything()
    {
        var message = Fail("-Version", "0.1.9001", "-PlanOnly");
        Assert.Contains("reserved development range", message);
        Assert.Contains("-Dev", message);
        Assert.Contains("cannot be combined", Fail("-Dev", "-Version", "0.1.8", "-PlanOnly"));
        Assert.Contains("cannot be combined", Fail("-Dev", "-FixtureVersion", "0.1.9", "-PlanOnly"));
    }

    [Fact]
    public void StablePlanCarriesNoDevMarkerAndFixtureModeIsUnchanged()
    {
        var (major, minor) = BaseVersion();
        var stable = Run("-PlanOnly");
        Assert.Equal(0, stable.ExitCode);
        using var document = JsonDocument.Parse(stable.Output);
        Assert.StartsWith($"{major}.{minor}.", document.RootElement.GetProperty("version").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("build").ValueKind);
        Assert.Equal(document.RootElement.GetProperty("version").GetString(), document.RootElement.GetProperty("informationalVersion").GetString());

        var fixture = Run("-PlanOnly", "-FixtureVersion", "0.1.9001", "-OutputDirectory", "artifacts/update-acceptance/plan-only");
        Assert.Equal(0, fixture.ExitCode);
        using var fixturePlan = JsonDocument.Parse(fixture.Output);
        Assert.Equal("0.1.9001", fixturePlan.RootElement.GetProperty("version").GetString());
        Assert.Equal(JsonValueKind.Null, fixturePlan.RootElement.GetProperty("build").ValueKind);
        Assert.False(Directory.Exists(Path.Combine(root, "artifacts", "update-acceptance", "plan-only")));
    }

    [Fact]
    public void DevPackagesMustStayUnderTheDevBuildFolder()
    {
        Assert.Contains("artifacts/dev-builds", Fail("-Dev", "-PlanOnly", "-OutputDirectory", "artifacts/releases"));
    }

    [Fact]
    public void DevDescriptorCarriesChannelCommitDirtyFlagBuildTimeAndBaseVersion()
    {
        var (major, minor) = BaseVersion();
        var plan = Plan();
        var build = plan.GetProperty("build");
        var commit = Git("rev-parse", "--short=7", "HEAD").Trim();
        Assert.Equal("dev", build.GetProperty("channel").GetString());
        Assert.Equal(commit, build.GetProperty("commit").GetString());
        Assert.True(build.GetProperty("dirty").ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.Equal($"{major}.{minor}.{BasePatch()}", build.GetProperty("baseVersion").GetString());
        Assert.True(DateTimeOffset.TryParse(build.GetProperty("builtAt").GetString(), out var builtAt));
        Assert.InRange(DateTimeOffset.UtcNow - builtAt, TimeSpan.FromMinutes(-1), TimeSpan.FromMinutes(5));
        var informational = plan.GetProperty("informationalVersion").GetString();
        Assert.StartsWith($"{plan.GetProperty("version").GetString()}-dev+{commit}", informational);
        Assert.Equal(build.GetProperty("dirty").GetBoolean(), informational!.EndsWith(".dirty", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingToolchainFailsWithAMessageNamingTheComponents()
    {
        var message = Fail("-CheckToolchain", "-VsWherePath", Path.Combine(root, "artifacts", "no-such-folder", "vswhere.exe"));
        Assert.Contains("vswhere.exe was not found", message);
        Assert.Contains("Build Tools", message);
        Assert.Contains("Windows SDK", message);
    }

    [Fact]
    public void ToolchainIsFoundWithoutChangingPath()
    {
        var installer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(installer))
        {
            return; // This machine has no Visual Studio Installer; the missing-toolchain test covers the failure message.
        }
        var result = Run("-CheckToolchain");
        if (result.ExitCode != 0)
        {
            // Build Tools or the SDK are absent here; the failure must name what is missing.
            Assert.Matches("C\\+\\+|build tools|Windows SDK", result.Output);
            return;
        }
        using var document = JsonDocument.Parse(result.Output);
        Assert.Equal(installer, document.RootElement.GetProperty("VsWhere").GetString(), ignoreCase: true);
    }

    private JsonElement Plan(params string[] extra)
    {
        var result = Run([.. new[] { "-Dev", "-PlanOnly", "-OutputDirectory", output, "-TargetInstallation", installation }, .. extra]);
        Assert.True(result.ExitCode == 0, result.Output);
        return JsonDocument.Parse(result.Output).RootElement.Clone();
    }

    private string Fail(params string[] arguments)
    {
        var result = Run([.. arguments, "-TargetInstallation", installation]);
        Assert.NotEqual(0, result.ExitCode);
        return result.Output;
    }

    private (string Output, int ExitCode) Run(params string[] arguments)
    {
        var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
        var start = new ProcessStartInfo(File.Exists(shell) ? shell : "pwsh")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = root
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(root, "scripts", "package.ps1") }.Concat(arguments))
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120000), "package.ps1 did not finish.");
        return (stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult(), process.ExitCode);
    }

    private static string Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true, WorkingDirectory = ProjectRoot() };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var text = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return text;
    }

    private (int Major, int Minor) BaseVersion()
    {
        var parts = BaseVersionText().Split('.');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    private int BasePatch() => int.Parse(BaseVersionText().Split('.')[2]);

    private string BaseVersionText() =>
        XDocument.Load(Path.Combine(root, "Directory.Build.props")).Descendants("Version").Single().Value;

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
