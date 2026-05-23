using System.Text.Json.Serialization;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(ProcessInfo))]
public partial class ProcessInfoJsonContext : JsonSerializerContext;