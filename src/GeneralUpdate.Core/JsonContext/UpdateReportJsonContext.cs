using System.Text.Json;
using System.Text.Json.Serialization;
using GeneralUpdate.Core.Download.Reporting;

namespace GeneralUpdate.Core.JsonContext;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UpdateReport))]
public partial class UpdateReportJsonContext : JsonSerializerContext;
