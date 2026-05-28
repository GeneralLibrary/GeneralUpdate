using System.Text.Json.Serialization;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(GlobalConfigInfoOss))]
public partial class GlobalConfigInfoOssJsonContext : JsonSerializerContext;