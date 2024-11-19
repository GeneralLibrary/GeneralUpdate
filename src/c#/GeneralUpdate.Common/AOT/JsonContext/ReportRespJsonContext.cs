using System.Text.Json.Serialization;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.AOT.JsonContext;

[JsonSerializable(typeof(BaseResponseDTO<bool>))]
public partial class ReportRespJsonContext : JsonSerializerContext;