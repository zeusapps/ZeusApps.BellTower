using System.Text.Json.Serialization;

namespace BellTower.Protocol;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, MaxDepth = 32)]
[JsonSerializable(typeof(CaptureEnvelope))]
public partial class CaptureJson : JsonSerializerContext;
