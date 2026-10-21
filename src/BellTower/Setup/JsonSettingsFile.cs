using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BellTower.IO;

namespace BellTower.Setup;

public sealed class JsonSettingsFile
{
    private static readonly JsonDocumentOptions Options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
    private readonly string path;
    private readonly string? before;
    private string text;
    public JsonObject Root { get; }

    public JsonSettingsFile(string path)
    {
        this.path = path;
        before = File.Exists(path) ? File.ReadAllText(path) : null;
        text = before ?? "{}";
        Root = JsonNode.Parse(text, documentOptions: Options)?.AsObject() ?? throw new InvalidDataException("Invalid terminal settings.");
    }

    public void Set(string name, JsonNode? value)
    {
        text = Replace(text, name, value);
        if (value is null)
        {
            Root.Remove(name);
        }
        else
        {
            Root[name] = value.DeepClone();
        }
    }

    public void AddTo(List<FileChange> changes)
    {
        if (text != before)
        {
            changes.Add(new(path, before, text));
        }
    }

    public static bool Same(JsonNode? node, string? encoded) => JsonNode.DeepEquals(node,
        encoded is null ? null : JsonNode.Parse(encoded));

    private static string Replace(string text, string name, JsonNode? value)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var properties = new List<(string Name, int Start, int ValueStart, int End)>();
        var closing = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 0)
            {
                closing = checked((int)reader.TokenStartIndex);
            }
            if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1)
            {
                continue;
            }
            var property = reader.GetString()!;
            var start = checked((int)reader.TokenStartIndex);
            reader.Read();
            var valueStart = checked((int)reader.TokenStartIndex);
            reader.Skip();
            properties.Add((property, start, valueStart, checked((int)reader.BytesConsumed)));
        }
        int Character(int offset) => Encoding.UTF8.GetCharCount(bytes.AsSpan(0, offset));
        var index = properties.FindIndex(property => property.Name == name);
        var replacement = value?.ToJsonString(Json.Options).Replace("\n", "\n  ");
        if (index >= 0)
        {
            var property = properties[index];
            if (value is not null)
            {
                return text[..Character(property.ValueStart)] + replacement + text[Character(property.End)..];
            }
            var start = Character(property.Start);
            var end = Character(property.End);
            var next = Significant(text, end);
            if (next < text.Length && text[next] == ',')
            {
                end = next + 1;
            }
            else if (index > 0)
            {
                var comma = Significant(text, Character(properties[index - 1].End));
                // Keep comments between the previous value and this owned property.
                return text[..comma] + text[(comma + 1)..start] + text[end..];
            }
            return text[..start] + text[end..];
        }
        if (value is null)
        {
            return text;
        }
        var last = properties.LastOrDefault();
        var closingCharacter = Character(closing);
        if (properties.Count > 0 && text[Significant(text, Character(last.End))] != ',')
        {
            text = text.Insert(Character(last.End), ",");
            closingCharacter++;
        }
        return text[..closingCharacter] + "\n  " + JsonSerializer.Serialize(name) + ": " + replacement + "\n" + text[closingCharacter..];
    }

    private static int Significant(string text, int index)
    {
        while (index < text.Length)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
            }
            else if (text.AsSpan(index).StartsWith("//"))
            {
                var end = text.IndexOf('\n', index);
                index = end < 0 ? text.Length : end + 1;
            }
            else if (text.AsSpan(index).StartsWith("/*"))
            {
                var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                index = end < 0 ? text.Length : end + 2;
            }
            else
            {
                break;
            }
        }
        return index;
    }
}
