using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Common.Internal.JsonContext;

[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Dictionary<string, object>))]
public partial class HttpParameterJsonContext: JsonSerializerContext;