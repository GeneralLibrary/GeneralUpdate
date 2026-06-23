using System.Text.Json.Serialization;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(VersionRespDTO))]
public partial class VersionRespJsonContext : JsonSerializerContext;