using System.Text.Json.Serialization;

namespace GeneralUpdate.Core.Configuration;

/// <summary>
/// Server push payload — raw data deserialized from <c>TbPacket</c> push notifications.
/// Inherits core version identity fields from <see cref="VersionIdentity"/>.
/// </summary>
public class PushPayload : VersionIdentity
{
    [JsonPropertyName("IsForcibly")]
    public bool? IsForcibly { get; set; }

    [JsonPropertyName("IsFreeze")]
    public bool? IsFreeze { get; set; }
}
