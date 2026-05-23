using System.Text.Json.Serialization;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(GlobalConfigInfoOSS))]
public partial class GlobalConfigInfoOSSJsonContext : JsonSerializerContext;