using System.Text.Json.Serialization;
using GeneralUpdate.Core.SelfCheck;

namespace GeneralUpdate.Core.JsonContext;

/// <summary>
///     AOT source-generation context for the update verification marker
///     (<c>generalupdate.verify.json</c>). Same pattern as the other JSON contexts in this
///     project: no reflection, safe under Native AOT and trimming.
/// </summary>
[JsonSerializable(typeof(UpdateVerificationMarkerInfo))]
public partial class UpdateVerificationMarkerJsonContext : JsonSerializerContext;
