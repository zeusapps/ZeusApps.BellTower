using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using BellTower.Core;
using BellTower.Hosts;
using BellTower.IO;
using BellTower.Providers;

namespace BellTower.Sessions;

public sealed class CodexRuntimeHost(DataPaths paths)
{
    public async Task<CodexRuntime> StartAsync(string attachment, string executable, CancellationToken cancellation)
    {
        var store = new CodexRuntimeStore(paths);
        if (!int.TryParse(Environment.GetEnvironmentVariable("BELLTOWER_SHELL_PID"), out var shellPid) ||
            ProcessProbe.Identify(shellPid) is null || store.Get(attachment) is not null)
        {
            throw new InvalidOperationException("A new managed shell attachment is required.");
        }
        using var manager = CodexRuntimeProcess.Start(Environment.ProcessPath!,
            ["codex-runtime-host", "--attachment", attachment, "--executable", executable, "--data-dir", paths.Root]);
        try
        {
            while (!manager.HasExited)
            {
                cancellation.ThrowIfCancellationRequested();
                if (store.Get(attachment) is { } runtime)
                {
                    return runtime;
                }
                await Task.Delay(100, cancellation).ConfigureAwait(false);
            }
            throw new IOException("Dedicated Codex runtime was unavailable.");
        }
        catch
        {
            store.Stop(attachment);
            if (!manager.HasExited)
            {
                manager.Kill(entireProcessTree: true);
                manager.WaitForExit(2000);
            }
            store.StopAndWait(attachment);
            throw;
        }
    }

    public async Task RunAsync(string attachment, string executable, CancellationToken cancellation)
    {
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable) ||
            !int.TryParse(Environment.GetEnvironmentVariable("BELLTOWER_SHELL_PID"), out var shellPid) ||
            ProcessProbe.Identify(shellPid) is not { } shell)
        {
            throw new InvalidDataException("A verified launcher and shell are required.");
        }
        if (!CodexRuntimeConsole.Join(shell))
        {
            throw new IOException("The dedicated runtime could not join its managed shell console.");
        }
        var store = new CodexRuntimeStore(paths);
        var tokenPath = store.TokenPath(attachment);
        Directory.CreateDirectory(Path.GetDirectoryName(tokenPath)!);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        AtomicFile.Write(tokenPath, token);
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var endpoint = "ws://127.0.0.1:" + port;
            var arguments = new[] { "app-server", "--listen", endpoint, "--ws-auth", "capability-token", "--ws-token-file", tokenPath };
            var native = Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase);
            var directory = Path.GetDirectoryName(executable)!;
            var npmScript = Path.Combine(directory, "node_modules", "@openai", "codex", "bin", "codex.js");
            var node = Path.Combine(directory, "node.exe");
            var npm = !native && Path.GetFileNameWithoutExtension(executable).Equals("codex", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(npmScript) && File.Exists(node);
            var start = new ProcessStartInfo(native ? executable : npm ? node : "powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (native || npm)
            {
                if (npm)
                {
                    start.ArgumentList.Add(npmScript);
                }
                foreach (var argument in arguments)
                {
                    start.ArgumentList.Add(argument);
                }
            }
            else
            {
                static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
                var script = "$ErrorActionPreference = 'Stop'; & " + Literal(executable) + " " +
                    string.Join(' ', arguments.Select(Literal)) + "; exit $LASTEXITCODE";
                foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) })
                {
                    start.ArgumentList.Add(argument);
                }
            }
            using var server = Process.Start(start) ?? throw new IOException("Dedicated Codex server could not start.");
            // Drain without retaining provider output, prompts, or diagnostics containing payloads.
            var output = server.StandardOutput.BaseStream.CopyToAsync(Stream.Null, cancellation);
            var error = server.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellation);
            try
            {
                var ready = false;
                for (var attempt = 0; attempt < 40 && !server.HasExited && !File.Exists(store.StopPath(attachment)); attempt++)
                {
                    using var connection = new CodexConnection();
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    timeout.CancelAfter(TimeSpan.FromSeconds(1));
                    try
                    {
                        await connection.ConnectAsync(endpoint, token, timeout.Token).ConfigureAwait(false);
                        ready = true;
                        break;
                    }
                    catch (Exception exception) when (exception is IOException or System.Net.WebSockets.WebSocketException or OperationCanceledException)
                    {
                        await Task.Delay(100, cancellation).ConfigureAwait(false);
                    }
                }
                if (!ready)
                {
                    new LocalLog(paths).Write(server.HasExited ? "codex-runtime-exit-" + server.ExitCode : "codex-runtime-timeout");
                    throw new IOException("Dedicated Codex server did not become ready.");
                }
                AtomicFile.Write(store.RecordPath(attachment), Json.Encode(new CodexRuntime(attachment, endpoint,
                    ProcessProbe.Identify(Environment.ProcessId)!, shell)));
                while (!server.HasExited && ProcessProbe.IsAlive(shell) && !File.Exists(store.StopPath(attachment)))
                {
                    await Task.Delay(250, cancellation).ConfigureAwait(false);
                }
            }
            finally
            {
                if (!server.HasExited)
                {
                    server.Kill(entireProcessTree: true);
                    await server.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
                await Task.WhenAll(output, error).ConfigureAwait(false);
            }
        }
        finally
        {
            File.Delete(store.RecordPath(attachment));
            File.Delete(tokenPath);
            File.Delete(store.StopPath(attachment));
        }
    }
}
