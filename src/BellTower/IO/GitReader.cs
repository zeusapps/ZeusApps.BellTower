using System.Diagnostics;
using BellTower.Core;

namespace BellTower.IO;

public sealed class GitReader
{
    private readonly Dictionary<string, (DateTimeOffset At, GitContext Value)> cache = new(StringComparer.OrdinalIgnoreCase);
    public GitContext Read(string directory)
    {
        if (cache.TryGetValue(directory, out var hit) && DateTimeOffset.UtcNow - hit.At < TimeSpan.FromSeconds(5))
        {
            return hit.Value;
        }

        var root = Run(directory, "rev-parse", "--show-toplevel");
        if (root is null)
        {
            return new();
        }

        var branch = Run(directory, "symbolic-ref", "--short", "-q", "HEAD") ?? Run(directory, "rev-parse", "--short", "HEAD");
        var gitDir = Run(directory, "rev-parse", "--absolute-git-dir");
        var common = Run(directory, "rev-parse", "--path-format=absolute", "--git-common-dir");
        var worktree = gitDir is not null && common is not null && !string.Equals(gitDir, common, StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(root) : null;
        var result = new GitContext(branch, worktree);
        if (cache.Count > 256)
        {
            cache.Clear();
        }

        cache[directory] = (DateTimeOffset.UtcNow, result);
        return result;
    }

    private static string? Run(string directory, params string[] arguments)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in arguments)
            {
                start.ArgumentList.Add(arg);
            }

            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(2000))
            {
                process.Kill();
                return null;
            } // Only this short-lived read-only Git child.

            Task.WaitAll(output, error);
            var text = output.Result.Trim();
            return process.ExitCode == 0 && !string.IsNullOrEmpty(text) ? text : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
