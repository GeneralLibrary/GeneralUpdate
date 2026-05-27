using System;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Core.Configuration;

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

    /// <summary>
    /// Update log or release notes for this version.
    /// </summary>
    [JsonPropertyName("updateLog")]
    public string? UpdateLog { get; set; }

    /// <summary>URL expiry time (UTC) for signed download URLs.</summary>
    [JsonPropertyName("urlExpireTimeUtc")]
    public DateTime? UrlExpireTimeUtc { get; set; }

    /// <summary>Upgrade mode: 1=VersionChain, 2=CrossVersion.</summary>
    [JsonPropertyName("upgradeMode")]
    public int? UpgradeMode { get; set; }

    /// <summary>Whether this is a cross-version packet.</summary>
    [JsonPropertyName("isCrossVersion")]
    public bool? IsCrossVersion { get; set; }

    /// <summary>Source version for cross-version packets.</summary>
    [JsonPropertyName("fromVersion")]
    public string? FromVersion { get; set; }

    /// <summary>Target version for cross-version packets.</summary>
    [JsonPropertyName("toVersion")]
    public string? ToVersion { get; set; }

    /// <summary>Whether this packet is frozen (archived, not for active updates).</summary>
    [JsonPropertyName("isFreeze")]
    public bool? IsFreeze { get; set; }
}