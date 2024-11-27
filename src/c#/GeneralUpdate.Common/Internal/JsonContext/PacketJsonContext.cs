using System.Text.Json.Serialization;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Internal.JsonContext;

[JsonSerializable(typeof(Packet))]
public partial class PacketJsonContext : JsonSerializerContext;