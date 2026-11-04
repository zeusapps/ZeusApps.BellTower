namespace BellTower.Tests.Testing;

internal static class BuiltApplication
{
    private static string Repository
    {
        get
        {
            var repository = new DirectoryInfo(AppContext.BaseDirectory);
            while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "ZeusApps.BellTower.slnx")))
            {
                repository = repository.Parent;
            }
            return repository!.FullName;
        }
    }

    public static string Executable
    {
        get
        {
            var output = new DirectoryInfo(AppContext.BaseDirectory);
            return Path.Combine(Repository, "src", "BellTower", "bin", output.Parent!.Name, output.Name, "bell-tower.exe");
        }
    }

    // The production fast-hook entry point, built beside the desktop executable's configuration and framework.
    public static string Ingress
    {
        get
        {
            var output = new DirectoryInfo(AppContext.BaseDirectory);
            return Path.Combine(Repository, "src", "BellTower.Ingress", "bin", output.Parent!.Name, output.Name, "win-x64", "bell-tower-ingress.exe");
        }
    }
}
