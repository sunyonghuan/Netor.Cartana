using System.Text.Json;
using System.Text.Json.Serialization;

namespace Netor.Cortana.Plugin;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    Converters = [typeof(JsonStringEnumConverter<PluginRuntime>)])]
[JsonSerializable(typeof(PluginManifest))]
internal partial class PluginManifestJsonContext : JsonSerializerContext;
