using System.Collections.Generic;
using System.Text.Json.Serialization;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Internal.JsonContext;

[JsonSerializable(typeof(List<VersionOSS>))]
public partial class VersionOSSJsonContext : JsonSerializerContext;