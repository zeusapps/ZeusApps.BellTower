using System.Text.Json.Nodes;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>One row of the installed <c>list</c> command (the fields the field checks compare).</summary>
/// <param name="CheckoutSource">The persisted data token <c>list</c> prints for where the working checkout came from (for example <c>retained</c>).</param>
/// <param name="Checkout">The working checkout <c>list</c> prints (the launch directory when no other checkout applies).</param>
public sealed record SessionRow(string Key, string Activity, string AttachmentHealth, string Name, JsonObject Identity, string CheckoutSource = "", string Checkout = "");

/// <summary>Reads the installed build's own command output (<c>list</c>, <c>doctor</c>) and its on-disk identity stores.</summary>
public static class TowerReader
{
    public static IReadOnlyList<SessionRow> List(Instance instance)
    {
        var result = instance.Tower("list");
        if (!result.Succeeded)
        {
            throw new IOException("list failed: " + result.Error);
        }
        var rows = new List<SessionRow>();
        foreach (var node in JsonNode.Parse(result.Output)!.AsArray())
        {
            var identity = node!["identity"]!.AsObject();
            rows.Add(new(node["key"]!.GetValue<string>(), node["activity"]?.GetValue<string>() ?? "", node["attachmentHealth"]?.GetValue<string>() ?? "",
                identity["name"]?.GetValue<string>() ?? "", identity, node["checkoutSource"]?.GetValue<string>() ?? "", node["checkout"]?.GetValue<string>() ?? ""));
        }
        return rows;
    }

    public static JsonNode Doctor(Instance instance)
    {
        var result = instance.Tower("doctor");
        if (!result.Succeeded)
        {
            throw new IOException("doctor failed: " + result.Error);
        }
        return JsonNode.Parse(result.Output)!;
    }

    public static bool TrayRunning(Instance instance) => Doctor(instance)["trayRunning"]?.GetValue<bool>() ?? false;

    /// <summary>Waits until the installed tray has consumed the captures and <c>list</c> shows every expected session.</summary>
    public static IReadOnlyList<SessionRow> WaitForSessions(Instance instance, IReadOnlyCollection<string> keys, int timeoutMilliseconds = 40000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        IReadOnlyList<SessionRow> rows = [];
        while (DateTime.UtcNow < deadline)
        {
            rows = List(instance);
            if (keys.All(key => rows.Any(row => row.Key == key)))
            {
                return rows;
            }
            Thread.Sleep(500);
        }
        return rows;
    }

    /// <summary>
    /// Waits until the condition holds. Returns false on a timeout (callers where the outcome matters must check it and fail a check).
    /// Throws <see cref="RunInterruptedException"/> once the run was asked to stop.
    /// </summary>
    public static bool WaitFor(Func<bool> condition, int timeoutMilliseconds = 30000, RunAbort? abort = null)
    {
        abort ??= RunAbort.Current;
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            abort.ThrowIfRequested();
            if (condition())
            {
                return true;
            }
            Thread.Sleep(300);
        }
        abort.ThrowIfRequested();
        return false;
    }

    /// <summary>The identity fields 0.1.9 already writes and a later build must keep: names, pins, change association and the palette.</summary>
    public static JsonObject IdentityView(JsonObject identity)
    {
        var view = new JsonObject();
        foreach (var name in new[] { "key", "defaultName", "manualName", "change", "changeSource", "colorPin", "checkoutPin", "launchDirectory" })
        {
            view[name] = identity[name]?.DeepClone();
        }
        view["palette"] = identity["palette"]?.DeepClone();
        return view;
    }

    /// <summary>The identity stores on disk (one JSON file per session key), keyed by session key.</summary>
    public static IReadOnlyDictionary<string, JsonObject> IdentityStores(Instance instance)
    {
        var stores = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var directory = Path.Combine(instance.Layout.Data, "identities");
        if (!Directory.Exists(directory))
        {
            return stores;
        }
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            var node = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            stores[node["key"]!.GetValue<string>()] = node;
        }
        return stores;
    }

    public static string Text(JsonNode? node, params string[] path)
    {
        foreach (var segment in path)
        {
            node = node?[segment];
        }
        return node?.ToString() ?? "";
    }
}
