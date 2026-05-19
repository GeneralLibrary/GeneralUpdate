using System.Text.Json.Serialization;

namespace Velaris.Sdk.Serialization;

/// <summary>
/// Source-generated JSON serializer context for Native AOT compatibility.
/// Registers all types that need JSON serialization/deserialization at compile time.
/// </summary>
[JsonSerializable(typeof(Platform.FlashPackMetadata))]
[JsonSerializable(typeof(Platform.SlotInfo))]
[JsonSerializable(typeof(Platform.VelaPlatform))]
[JsonSerializable(typeof(Platform.UpdateMethod))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class VelarisJsonContext : JsonSerializerContext
{
}
