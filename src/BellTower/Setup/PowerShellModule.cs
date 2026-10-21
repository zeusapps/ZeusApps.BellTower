using System.Text;

namespace BellTower.Setup;

public static class PowerShellModule
{
    public static string Render(string executable, string dataDirectory, IEnumerable<string> providers)
    {
        using var resource = typeof(PowerShellModule).Assembly.GetManifestResourceStream("BellTower.Integration.BellTower.psm1")!;
        using var reader = new StreamReader(resource);
        var text = new StringBuilder();
        text.AppendLine("$script:BellTowerExecutable = " + Literal(executable));
        text.AppendLine("$script:BellTowerDataDirectory = " + Literal(dataDirectory));
        text.AppendLine(reader.ReadToEnd());
        var roles = providers.Order(StringComparer.Ordinal).SelectMany(provider => RoleProfile.For(provider).Concat(RoleProfile.LegacyFor(provider))).ToArray();
        foreach (var role in roles)
        {
            text.AppendLine($$"""
                function {{role.Function}} {
                    [CmdletBinding(PositionalBinding = $false)]
                    param(
                        [string]$Name,
                        [string]$Change,
                        [string]$Directory,
                        [switch]$NoBackground,
                        [switch]$Silent,
                        [string]$Executable,
                        [Parameter(ValueFromRemainingArguments = $true)][string[]]$AgentArguments
                    )
                    Invoke-BellTowerSession -Provider {{Literal(role.Provider)}} -Role {{Literal(role.Role)}} @PSBoundParameters
                }
                """);
        }
        text.AppendLine("Export-ModuleMember -Function " + string.Join(", ", roles.Select(role => Literal(role.Function))));
        return text.ToString();
    }

    public static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
}
