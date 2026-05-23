using System.Collections.Generic;
using System.Text.Json.Serialization;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(List<VersionOSS>))]
public partial class VersionOSSJsonContext : JsonSerializerContext;