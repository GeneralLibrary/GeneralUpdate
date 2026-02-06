using System;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Common.Shared.Object;

public class VersionInfo
{
    [JsonPropertyName("recordId")]
    public int RecordId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    [JsonPropertyName("releaseDate")]
    public DateTime? ReleaseDate { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("appType")]
    public int? AppType { get; set; }

    [JsonPropertyName("platform")]
    public int? Platform { get; set; }

    [JsonPropertyName("productId")]
    public string? ProductId { get; set; }

    [JsonPropertyName("isForcibly")]
    public bool? IsForcibly { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    /// <summary>
    /// HTTP authentication scheme (e.g., "Bearer", "Basic") for download requests.
    /// </summary>
    [JsonPropertyName("authScheme")]
    public string? AuthScheme { get; set; }

    /// <summary>
    /// HTTP authentication token for download requests.
    /// </summary>
    [JsonPropertyName("authToken")]
    public string? AuthToken { get; set; }
}