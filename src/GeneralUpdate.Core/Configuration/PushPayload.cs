using System.Text.Json.Serialization;

namespace GeneralUpdate.Core.Configuration;

/// <summary>
/// Server push payload — raw data deserialized from <c>TbPacket</c> push notifications.
/// Inherits core version identity fields from <see cref="VersionIdentity"/>.
/// </summary>
public class PushPayload : VersionIdentity
{
    /// <summary>Push record identifier, absent when sent by older servers.</summary>
    [JsonPropertyName("RecordId")]
    public int? RecordId { get; set; }

    [JsonPropertyName("Size")]
    public long Size { get; set; }

    [JsonPropertyName("PackageType")]
    public int PackageType { get; set; }

    [JsonPropertyName("Format")]
    public string? Format { get; set; }

    [JsonPropertyName("IsForcibly")]
    public bool? IsForcibly { get; set; }

    [JsonPropertyName("IsFreeze")]
    public bool? IsFreeze { get; set; }
}
