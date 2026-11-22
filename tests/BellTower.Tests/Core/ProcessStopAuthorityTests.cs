using System.Text.RegularExpressions;
using BellTower.Sessions;
using Xunit;

namespace BellTower.Tests.Core;

// Task 3.3 guard (a tripwire over the known terminating API call sites, not a proof of completeness): every place Bell Tower
// terminates a process is a handle it created in the same operation, or a descendant of one. A new stop site must be added here with its
// owner, so a management action cannot start signalling a process by PID, image name or record.
public sealed class ProcessStopAuthorityTests
{
    // file -> (terminating calls, the process whose handle Bell Tower created in that operation)
    private static readonly Dictionary<string, (int Count, string Owner)> Allowed = new()
    {
        ["BellTower.Protocol/TrayProcess.cs"] = (2, "the suspended tray child this hook start created with CREATE_SUSPENDED, terminated before any of its code ran when the job check, the query or ResumeThread fails (one call site), and the P/Invoke declaration for that call (one site)"),
        ["BellTower/Hosts/WindowsTerminalNavigator.cs"] = (1, "the disposable tab-selection helper it started"),
        ["BellTower/IO/GitReader.cs"] = (1, "the git child it started"),
        ["BellTower/IO/OwnedProcessRunner.cs"] = (1, "the single helper child it started"),
        ["BellTower/Sessions/CodexRuntimeHost.cs"] = (2, "the runtime-host manager it launched, and the backend that manager launched together with the backend's descendants"),
        ["BellTower/Setup/IngressProvision.cs"] = (1, "the protocol-probe child it started"),
        ["BellTower/Setup/ProviderHookProbe.cs"] = (2, "the isolated hook-probe children it started"),
        ["BellTower/Updates/ReleasePackage.cs"] = (1, "the release probe child it started")
    };

    private static readonly Regex Terminating = new(@"\.Kill\(|TerminateProcess|Stop-Process|taskkill|CloseMainWindow|\.CloseMainWindow|GenerateConsoleCtrlEvent",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void OnlyHandlesCreatedByTheOperationAreEverTerminated()
    {
        var source = Path.Combine(Repository(), "src");
        var found = new Dictionary<string, int>();
        foreach (var file in Directory.EnumerateFiles(source, "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".psm1", StringComparison.Ordinal) || file.EndsWith(".ps1", StringComparison.Ordinal))
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)))
        {
            var count = File.ReadAllLines(file).Count(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal) && Terminating.IsMatch(line));
            if (count > 0)
            {
                found[Path.GetRelativePath(source, file).Replace('\\', '/')] = count;
            }
        }
        Assert.Equal(Allowed.ToDictionary(pair => pair.Key, pair => pair.Value.Count).OrderBy(pair => pair.Key),
            found.OrderBy(pair => pair.Key));
    }

    [Fact]
    public void OnlyTheOwnedAccessRecordNamesARuntimeBellTowerStarted()
    {
        // The shared record carries a Codex home and no process, endpoint or token, so nothing can be stopped through it.
        Assert.Equal(["Home"], typeof(CodexAccess.Shared).GetProperties().Where(p => p.DeclaringType == typeof(CodexAccess.Shared) && p.Name is not ("EqualityContract")).Select(p => p.Name).Where(n => n != "Key"));
        Assert.Contains(typeof(CodexAccess.Owned).GetProperties(), p => p.Name == "TokenPath");
    }

    private static string Repository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ZeusApps.BellTower.slnx")))
        {
            directory = directory.Parent;
        }
        return directory!.FullName;
    }
}
