using System;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Common.Shared.Object;

/// <summary>
/// Currently used only for upgrade push.
/// </summary>
public class Packet
{
    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("Hash")]
    public string Hash { get; set; }

    [JsonPropertyName("ReleaseDate")]
    public DateTime? ReleaseDate { get; set; }

    [JsonPropertyName("Url")]
    public string? Url { get; set; }

    [JsonPropertyName("Version")]
    public string? Version { get; set; }

    [JsonPropertyName("AppType")]
    public int? AppType { get; set; }

    [JsonPropertyName("Platform")]
    public int? Platform { get; set; }

    [JsonPropertyName("ProductId")]
    public string? ProductId { get; set; }

    [JsonPropertyName("IsForcibly")]
    public bool? IsForcibly { get; set; }

    [JsonPropertyName("IsFreeze")]
    public bool? IsFreeze { get; set; }
}