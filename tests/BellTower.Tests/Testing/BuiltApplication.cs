namespace BellTower.Tests.Testing;

internal static class BuiltApplication
{
    public static string Executable
    {
        get
        {
            var output = new DirectoryInfo(AppContext.BaseDirectory);
            var repository = output;
            while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "ZeusApps.BellTower.slnx")))
            {
                repository = repository.Parent;
            }
            return Path.Combine(repository!.FullName, "src", "BellTower", "bin", output.Parent!.Name, output.Name, "bell-tower.exe");
        }
    }
}
