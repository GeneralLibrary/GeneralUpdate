using System.Text.Json.Serialization;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(PushPayload))]
[JsonSerializable(typeof(PushValidationRequest))]
[JsonSerializable(typeof(PushValidationResponse))]
public partial class PushPayloadJsonContext : JsonSerializerContext;