using System.Text.Json.Serialization;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Internal.JsonContext;

[JsonSerializable(typeof(BaseResponseDTO<bool>))]
public partial class ReportRespJsonContext : JsonSerializerContext;