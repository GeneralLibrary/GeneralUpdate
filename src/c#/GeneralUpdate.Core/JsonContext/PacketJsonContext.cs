using System.Text.Json.Serialization;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(Packet))]
public partial class PacketJsonContext : JsonSerializerContext;