$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$app = Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows/bell-tower.dll'
if (!(Test-Path -LiteralPath $app)) { $app = Join-Path $repo 'src/BellTower/bin/Release/net10.0-windows/BellTower.dll' }
$bin = Split-Path $app
foreach ($dependency in @('BellTower.Distribution.dll', 'BellTower.Protocol.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $bin $dependency))
}
[void][Reflection.Assembly]::LoadFrom($app)
$references = @((Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll').FullName) + @($app, (Join-Path $bin 'BellTower.Protocol.dll'))
Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;

public sealed class QaPartialWriter : ITerminalPresentationWriter
{
    public List<string> Requests = new();
    public string ActualTitle = "initial";
    public bool Succeed = true;
    public Task<bool> WriteAsync(DesiredTerminalPresentation desired, CancellationToken cancellation)
    {
        Requests.Add(desired.Title);
        ActualTitle = desired.Title; // SetTitle succeeds before optional background Write fails.
        return Task.FromResult(Succeed);
    }
}

public static class QaPartialWriteProbe
{
    public static string Resume(string root)
    {
        var paths = new DataPaths(root);
        paths.Ensure();
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false,
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30" })
            start.ArgumentList.Add(arg);
        using var child = Process.Start(start);
        try
        {
            var oldOwner = ProcessProbe.Identify(Environment.ProcessId);
            var freshOwner = ProcessProbe.Identify(child.Id);
            var at = DateTimeOffset.UtcNow.AddSeconds(-10);
            var store = new AttachmentStore(paths);
            store.Save(new Attachment("same-id", "claude", oldOwner,
                new HostTarget("windows-terminal", oldOwner, oldOwner, 111, "old-terminal"), root, at,
                EndedAt: at.AddSeconds(1), ManagedTitle: true));
            var captured = new Attachment("same-id", "claude", freshOwner,
                new HostTarget("vscode", freshOwner, freshOwner, 222, "new-terminal"), root, at.AddSeconds(2),
                ManagedTitle: true);
            var resumed = store.Adopt(captured, DateTimeOffset.UtcNow, resume: true);
            return "newAgent=" + (resumed.Process == freshOwner) + "; newShell=" + (resumed.Host.Shell == freshOwner) +
                "; newWindow=" + (resumed.Host.WindowProcess == freshOwner) + "; kind=" + resumed.Host.Kind +
                "; handle=" + resumed.Host.WindowHandle + "; terminal=" + resumed.Host.TerminalSession +
                "; expected=vscode/222/new-terminal; synthetic window handles only; no focus executed";
        }
        finally
        {
            if (!child.HasExited) child.Kill(entireProcessTree: false);
            child.WaitForExit();
        }
    }

    public static async Task<string> Run(string root)
    {
        var paths = new DataPaths(root);
        paths.Ensure();
        var live = ProcessProbe.Identify(Environment.ProcessId);
        var at = DateTimeOffset.UtcNow;
        new AttachmentStore(paths).Save(new Attachment("qa-attachment", "claude", live,
            new HostTarget("windows-terminal", live), root, at, ManagedTitle: true));
        var session = new SessionState { Key = "claude:qa", Provider = "claude", SessionId = "qa",
            AttachmentId = "qa-attachment", Directory = root, LastObserved = at };
        var identities = new IdentityStore(paths);
        var identity = identities.Get(session.Key, root);
        identity.ManualName = "A";
        identities.Save(identity);
        var writer = new QaPartialWriter();
        using var pump = new TerminalTitlePump(paths, new CheckoutCache(_ => new GitContext()), writer);
        await pump.RefreshAsync(new[] { session });
        identity.ManualName = "B";
        identities.Save(identity);
        writer.Succeed = false;
        await pump.RefreshAsync(new[] { session });
        identity.ManualName = "A";
        identities.Save(identity);
        writer.Succeed = true;
        await pump.RefreshAsync(new[] { session });
        return "requests=" + string.Join(",", writer.Requests) + "; desired=A; actual=" + writer.ActualTitle +
            "; expectedRequests=A,B,A; defect=" + (writer.ActualTitle != "A");
    }
}
'@
$data = Join-Path $repo ('artifacts/qa-liveness-partial/' + [guid]::NewGuid().ToString('N'))
[QaPartialWriteProbe]::Run($data).GetAwaiter().GetResult()
[QaPartialWriteProbe]::Resume($data + '-resume')
