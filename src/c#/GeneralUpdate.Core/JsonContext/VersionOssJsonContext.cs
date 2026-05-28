using System.Collections.Generic;
using System.Text.Json.Serialization;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(List<VersionOss>))]
public partial class VersionOssJsonContext : JsonSerializerContext;