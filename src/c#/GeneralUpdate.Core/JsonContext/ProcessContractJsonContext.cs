using System.Text.Json.Serialization;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(ProcessContract))]
public partial class ProcessContractJsonContext : JsonSerializerContext;