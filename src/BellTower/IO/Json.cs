using System.Text.Json;
using System.Text.Json.Serialization;

namespace BellTower.IO;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        },
        MaxDepth = 32,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
    public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Decode<T>(string text) => JsonSerializer.Deserialize<T>(text, Options) ?? throw new InvalidDataException("Empty JSON document.");
}
