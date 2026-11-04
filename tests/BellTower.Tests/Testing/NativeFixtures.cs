using System.Text.Json;

namespace BellTower.Tests.Testing;

// One file per provider, version and hook event under Fixtures/native: <provider>-<version>-<event>.json. A refresh for
// a new provider version is "save a capture and add a file"; nothing here or in the tests names a provider or session.
// `source` states honestly whether the payload is a verbatim capture or a reconstruction of recorded shapes.
public sealed record NativeFixture(
    string Name, string Provider, string ProviderVersion, string Source, string Note, JsonElement Payload,
    string[]? AgentArguments, string[] AncestorNames, string ExpectedMode, bool ExpectedDedicated, string ExpectedLifecycleOwner)
{
    public string SessionId => Payload.GetProperty("session_id").GetString()!;

    public string Event => Payload.GetProperty("hook_event_name").GetString()!;

    public string PayloadText => Payload.GetRawText();

    public static string Directory => Path.Combine(AppContext.BaseDirectory, "Fixtures", "native");

    public static IReadOnlyList<string> Names() => System.IO.Directory.GetFiles(Directory, "*.json")
        .Select(path => Path.GetFileName(path)).OrderBy(name => name, StringComparer.Ordinal).ToArray();

    public static IReadOnlyList<NativeFixture> All() => Names().Select(Load).ToArray();

    public static NativeFixture Load(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Directory, name)));
        var root = document.RootElement.Clone();
        var expected = root.GetProperty("expected");
        return new NativeFixture(
            name,
            root.GetProperty("provider").GetString()!,
            root.GetProperty("providerVersion").GetString()!,
            root.GetProperty("source").GetString()!,
            root.GetProperty("note").GetString()!,
            root.GetProperty("payload"),
            root.GetProperty("agentArguments") is { ValueKind: JsonValueKind.Array } arguments ? arguments.EnumerateArray().Select(item => item.GetString()!).ToArray() : null,
            root.GetProperty("ancestorNames").EnumerateArray().Select(item => item.GetString()!).ToArray(),
            expected.GetProperty("mode").GetString()!,
            expected.GetProperty("dedicated").GetBoolean(),
            expected.GetProperty("lifecycleOwner").GetString()!);
    }

    // The SessionStart fixture of a provider (the newest version by name), for rows that need a real payload to pipe in.
    public static NativeFixture SessionStart(string provider) => All()
        .Where(fixture => fixture.Provider == provider && fixture.Event == "SessionStart")
        .OrderBy(fixture => fixture.Name, StringComparer.Ordinal).Last();
}
