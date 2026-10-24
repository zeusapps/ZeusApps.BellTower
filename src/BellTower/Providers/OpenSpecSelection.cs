namespace BellTower.Providers;

// Work evidence candidate normalized from a provider tool call. Verb is "edit", "skill" or "new".
public sealed record OpenSpecSelection(string Change, string? Store, string Operation, string Verb)
{
    public string? WorkRoot { get; init; }
    public bool Weak { get; init; }

    // Effective working directory of a shell command: its literal cd target, else the hook directory.
    public string? Directory { get; init; }
    public bool Redirected { get; init; }
}
