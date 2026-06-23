using System.Text.Json;
using System.Text.Json.Serialization;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.JsonContext;

/// <summary>
/// Source-generated JSON serialization context for <see cref="UpdateRequest"/> with
/// case-insensitive property matching. Used by <see cref="UpdateRequestBuilder.LoadFromConfigFile"/>
/// to support <c>update_config.json</c> files where property casing may differ from the C# model.
/// Full Native AOT compatible — case-insensitivity is resolved at compile time.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(UpdateRequest))]
internal partial class UpdateRequestConfigJsonContext : JsonSerializerContext;
