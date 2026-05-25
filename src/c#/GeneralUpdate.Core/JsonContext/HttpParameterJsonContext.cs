using System.Collections.Generic;
using System.Text.Json.Serialization;
using GeneralUpdate.Core.Download.Abstractions;

namespace GeneralUpdate.Core.JsonContext;

[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(PacketDTO))]
[JsonSerializable(typeof(List<PacketDTO>))]
public partial class HttpParameterJsonContext: JsonSerializerContext;