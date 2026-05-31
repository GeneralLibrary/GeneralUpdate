using System.Text.Json.Serialization;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(PushPayload))]
public partial class PushPayloadJsonContext : JsonSerializerContext;