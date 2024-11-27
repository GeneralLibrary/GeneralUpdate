using System.Text.Json.Serialization;

namespace GeneralUpdate.Bowl.Internal;

[JsonSerializable(typeof(Crash))]
internal partial class CrashJsonContext : JsonSerializerContext;