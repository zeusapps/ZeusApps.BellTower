using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Evidence-only simulation of the released tray's old ordering:
// CanStartTray succeeds, then the process pauses before constructing the
// named tray-instance mutex. This is not the original 0.1.8 executable.
if (args.Length < 3 || args[0] != "tray" || args[1] != "--data-dir")
    return 64;

var dataRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[2]));
var controlRoot = Environment.GetEnvironmentVariable("QA004_DELAYED_TRAY_CONTROL");
if (string.IsNullOrWhiteSpace(controlRoot))
    return 65;
Directory.CreateDirectory(controlRoot);
var pid = Environment.ProcessId;
var image = Environment.ProcessPath ?? "";
var entered = Path.Combine(controlRoot, "can-start-passed.json");
var resultPath = Path.Combine(controlRoot, "mutex-result.json");
var planPath = Path.Combine(dataRoot, "updates", "restart.json");

bool canStart;
try
{
    if (!File.Exists(planPath))
        canStart = true;
    else
    {
        using var document = JsonDocument.Parse(File.ReadAllText(planPath));
        var plan = document.RootElement;
        var created = plan.GetProperty("created").GetDateTimeOffset();
        var owner = plan.GetProperty("owner");
        var ownerPid = owner.GetProperty("pid").GetInt32();
        var ownerTicks = owner.GetProperty("startedUtcTicks").GetInt64();
        canStart = DateTimeOffset.UtcNow - created >= TimeSpan.FromMinutes(10) || !IsAlive(ownerPid, ownerTicks) ||
            string.Equals(Environment.GetEnvironmentVariable("BELLTOWER_UPDATE_ACTIVATION"), plan.GetProperty("id").GetString(), StringComparison.Ordinal);
    }
}
catch
{
    // Match UpdateTransaction.RestartPending's fail-open treatment of a bad marker.
    canStart = true;
}

if (!canStart)
    return 0;

File.WriteAllText(entered, JsonSerializer.Serialize(new { pid, image, dataRoot, canStart = true, utc = DateTimeOffset.UtcNow }));
var releasePath = Path.Combine(controlRoot, "release-before-instance-mutex");
var deadline = Stopwatch.StartNew();
while (!File.Exists(releasePath) && deadline.Elapsed < TimeSpan.FromMinutes(5))
    await Task.Delay(25);
if (!File.Exists(releasePath))
    return 66;

var hashInput = dataRoot.ToUpperInvariant() + "tray-instance";
var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))).ToLowerInvariant()[..24];
var mutexName = "Local\\BellTower-" + hash;
bool createdNew;
using (var instance = new Mutex(true, mutexName, out createdNew))
{
    File.WriteAllText(resultPath, JsonSerializer.Serialize(new { pid, image, dataRoot, createdNew, utc = DateTimeOffset.UtcNow }));
    if (createdNew)
    {
        var presence = Path.Combine(dataRoot, "updates", "fixture-tray-owner.json");
        File.WriteAllText(presence, JsonSerializer.Serialize(new { pid, image, dataRoot, utc = DateTimeOffset.UtcNow }));
        var ownerDeadline = Stopwatch.StartNew();
        while (ownerDeadline.Elapsed < TimeSpan.FromSeconds(30))
            await Task.Delay(50);
        File.Delete(presence);
    }
}
return 0;

static bool IsAlive(int pid, long startedUtcTicks)
{
    try
    {
        using var process = Process.GetProcessById(pid);
        return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == startedUtcTicks;
    }
    catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
    {
        return false;
    }
}
