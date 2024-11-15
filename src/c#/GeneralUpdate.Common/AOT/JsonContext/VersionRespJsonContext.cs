using System.Text.Json.Serialization;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.AOT.JsonContext;

[JsonSerializable(typeof(VersionRespDTO))]
public partial class VersionRespJsonContext : JsonSerializerContext;