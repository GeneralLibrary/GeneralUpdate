using System.Text.Json.Serialization;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(BaseResponseDTO<bool>))]
public partial class ReportRespJsonContext : JsonSerializerContext;