using System.Diagnostics;
using Xunit;

namespace BellTower.Tests.Testing;

// Every test git invocation goes through here (or through Isolate) so a fixture can never act on the real repository:
// the parent's repository-selecting variables are scrubbed and discovery is stopped at the test workspace root.
public static class GitHelper
{
    // Variables that redirect git to another repository, index or object store (QA-1700).
    private static readonly string[] Selectors =
    [
        "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_COMMON_DIR", "GIT_OBJECT_DIRECTORY",
        "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_NAMESPACE"
    ];

    public static string Run(string directory, params string[] args) => RunWith(directory, null, args);

    // `ambient` stands for a hostile parent environment (for example one exported by a git hook); it is applied before the scrub.
    public static string RunWith(string directory, IReadOnlyDictionary<string, string>? ambient, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var (key, value) in ambient ?? new Dictionary<string, string>())
        { start.Environment[key] = value; }
        foreach (var arg in args)
        { start.ArgumentList.Add(arg); }
        Isolate(start, directory);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }

    // Removes the repository selectors only; for callers that deliberately read the real repository.
    public static void Scrub(ProcessStartInfo start)
    {
        foreach (var name in Selectors)
        { start.Environment.Remove(name); }
    }

    // Scrubs the selectors and stops repository discovery above the test workspace root, so a directory that is not
    // its own repository can never resolve to an enclosing one.
    public static void Isolate(ProcessStartInfo start, string directory)
    {
        Scrub(start);
        start.Environment["GIT_CEILING_DIRECTORIES"] = Ceiling(directory);
    }

    // The parent of the test workspace root (artifacts/unit-tests) when the directory is inside one; otherwise the directory's parent.
    internal static string Ceiling(string directory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(directory));
        for (var candidate = current; candidate?.Parent is not null; candidate = candidate.Parent)
        {
            if (candidate.Parent.Name == "unit-tests" && candidate.Parent.Parent?.Name == "artifacts")
            { return candidate.Parent.FullName.Replace(Path.DirectorySeparatorChar, '/'); }
        }
        return (current.Parent ?? current).FullName.Replace(Path.DirectorySeparatorChar, '/');
    }
}
