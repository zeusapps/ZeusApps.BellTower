using System.Runtime.CompilerServices;
using BellTower.IO;

namespace BellTower.Tests.Testing;

/// <summary>
/// Puts the disabling provider title setting in a disposable provider home (design decision 11), the way the opt-in step or the user would,
/// and points the title-state service at that home for the calling test. Without it the service reads a home under the test output folder
/// that holds nothing, so no test ever reads a real provider profile.
/// </summary>
internal static class ProviderTitleFixture
{
    [ModuleInitializer]
    internal static void DefaultToAnEmptyHome() =>
        ProviderTitleState.DefaultHome = provider => Path.Combine(AppContext.BaseDirectory, "no-provider-home", provider);

    public static string Home(DataPaths paths) => Path.Combine(paths.Root, "provider-home");

    /// <summary>Scopes the title state to <paramref name="home"/> (the flow of the calling test).</summary>
    public static void Use(Func<string, string> home) => ProviderTitleState.ScopedHome.Value = provider => home(provider);

    /// <summary>
    /// Writes the setting for each provider into the disposable home, dated <paramref name="writtenAgo"/> ago (a year by default, so every
    /// session of the test started after it), and scopes the state to that home.
    /// </summary>
    public static void Apply(DataPaths paths, params string[] providers) => Apply(paths, TimeSpan.FromDays(365), providers);

    public static void Apply(DataPaths paths, TimeSpan writtenAgo, params string[] providers)
    {
        var root = Home(paths);
        Use(provider => Path.Combine(root, provider));
        foreach (var provider in providers)
        {
            var file = ProviderTitleState.SettingsFile(provider);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, provider == "claude"
                ? "{\"env\":{\"" + ProviderTitleState.ClaudeKey + "\":\"1\"}}"
                : "[tui]\nterminal_title = []\n");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow - writtenAgo);
        }
    }

    /// <summary>The service's answer for a session of <paramref name="provider"/> that started <paramref name="startedAgo"/> ago.</summary>
    public static bool DisabledFor(DataPaths paths, string provider, TimeSpan startedAgo) =>
        new ProviderTitleState(paths).DisabledFor(provider, (DateTime.UtcNow - startedAgo).Ticks);
}
