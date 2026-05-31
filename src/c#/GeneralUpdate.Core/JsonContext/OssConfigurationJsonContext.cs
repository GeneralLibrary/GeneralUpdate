using System.Text.Json.Serialization;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(OssConfiguration))]
public partial class OssConfigurationJsonContext : JsonSerializerContext;