using System.Reflection;

namespace BellTower.Cli;

internal static class ApplicationInfo
{
    public static string Version { get; } = typeof(ApplicationInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
}
