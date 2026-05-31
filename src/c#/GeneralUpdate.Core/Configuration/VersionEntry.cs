using System;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Core.Configuration;

/// <summary>
///     Represents a version information object returned by the update server.
///     Corresponds to the data structure of the server-side JSON response, containing all metadata related to
///     a single version, including version identifier, download URL, update log, and upgrade mode.
/// </summary>
/// <remarks>
///     <para>
///         <c>VersionEntry</c> is the data contract between the client and the update server, deserialized from
///         the JSON response of the version check API. Each <c>VersionEntry</c> instance represents an available
///         update version.
///     </para>
///     <para>
///         This object is used throughout the update workflow:
///         <list type="number">
///             <item>
///                 <description>
///                     Version check phase: Retrieves the version list from the server to determine whether a
///                     new version exists.
///                 </description>
///             </item>
///             <item>
///                 <description>
///                     Update download phase: Downloads the update package via the <see cref="Url" /> property.
///                 </description>
///             </item>
///             <item>
///                 <description>
///                     Differential update phase: Uses <see cref="Hash" /> for file verification, and
///                     <see cref="FromVersion" /> and <see cref="ToVersion" /> to determine the scope of the
///                     differential patch.
///                 </description>
///             </item>
///             <item>
///                 <description>
///                     IPC transmission phase: Passed to the upgrade process as list elements in
///                     <see cref="ProcessContract.UpdateVersions" />.
///                 </description>
///             </item>
///         </list>
///     </para>
///     <para>
///         All properties are annotated with <see cref="JsonPropertyNameAttribute" /> to map server-side JSON
///         response field names. Most properties are nullable to tolerate potentially missing fields from the server.
///     </para>
/// </remarks>
/// <seealso cref="ProcessContract" />
/// <seealso cref="ConfigurationMapper" />
public class VersionEntry : VersionIdentity
{
    /// <summary>
    ///     The unique identifier (primary key ID) of the version record.
    /// </summary>
    [JsonPropertyName("recordId")]
    public int RecordId { get; set; }

    /// <summary>
    ///     The version name or label (e.g., "v1.0.1", "Release Candidate 2").
    /// </summary>
    [JsonPropertyName("name")]
    public override string? Name { get; set; }

    /// <summary>
    ///     The hash value of the update package file (typically SHA256), used for integrity verification after download.
    /// </summary>
    [JsonPropertyName("hash")]
    public override string? Hash { get; set; }

    /// <summary>
    ///     The release date of the version.
    /// </summary>
    [JsonPropertyName("releaseDate")]
    public override DateTime? ReleaseDate { get; set; }

    /// <summary>
    ///     The download URL of the update package.
    ///     The client downloads the update package file from this address.
    /// </summary>
    [JsonPropertyName("url")]
    public override string? Url { get; set; }

    /// <summary>
    ///     The version number string of this version information (e.g., "1.0.0.1").
    /// </summary>
    [JsonPropertyName("version")]
    public override string? Version { get; set; }

    /// <summary>
    ///     The application type identifier, used to distinguish between main application updates and updater updates.
    /// </summary>
    /// <remarks>
    ///     Takes an integer enum value: typically 0 for the main application (Client) and 1 for the updater (Upgrade).
    ///     Works with <see cref="UpdateConfiguration.AppType" /> to determine whether this version is used for
    ///     <see cref="UpdateContext.IsMainUpdate" /> or <see cref="UpdateContext.IsUpgradeUpdate" />.
    /// </remarks>
    [JsonPropertyName("appType")]
    public override int? AppType { get; set; }

    /// <summary>
    ///     The target platform identifier, used to distinguish update packages for different operating systems or architectures.
    /// </summary>
    [JsonPropertyName("platform")]
    public override int? Platform { get; set; }

    /// <summary>
    ///     The product identifier associated with this version, used for version filtering in multi-product environments.
    /// </summary>
    [JsonPropertyName("productId")]
    public override string? ProductId { get; set; }

    /// <summary>
    ///     Whether the update is forced.
    ///     If <c>true</c>, the client must apply this update to continue using the application.
    /// </summary>
    [JsonPropertyName("isForcibly")]
    public bool? IsForcibly { get; set; }

    /// <summary>
    ///     The compression format name of the update package (e.g., "zip", "7z", "tar.gz").
    /// </summary>
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    /// <summary>
    ///     The size of the update package file in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public long? Size { get; set; }

    /// <summary>
    ///     The HTTP authentication scheme for download requests (e.g., "Bearer", "Basic").
    /// </summary>
    /// <remarks>
    ///     Used when additional authentication is required for downloading the update package, corresponding to the
    ///     authentication method configured on the server side.
    /// </remarks>
    [JsonPropertyName("authScheme")]
    public string? AuthScheme { get; set; }

    /// <summary>
    ///     The HTTP authentication token for download requests.
    /// </summary>
    /// <remarks>
    ///     Used in conjunction with <see cref="AuthScheme" />, appended to HTTP request headers during update
    ///     package download for authentication.
    /// </remarks>
    [JsonPropertyName("authToken")]
    public string? AuthToken { get; set; }

    /// <summary>
    ///     The update log or release notes text for this version.
    /// </summary>
    [JsonPropertyName("updateLog")]
    public string? UpdateLog { get; set; }

    /// <summary>
    ///     The expiration time (UTC) of the signed download URL.
    ///     After expiration, the URL becomes invalid and must be re-acquired.
    /// </summary>
    [JsonPropertyName("urlExpireTimeUtc")]
    public DateTime? UrlExpireTimeUtc { get; set; }

    /// <summary>
    ///     The upgrade mode: 1 = VersionChain (sequential version upgrades), 2 = CrossVersion (cross-version upgrade).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <list type="bullet">
    ///             <item>
    ///                 <description><c>1 (VersionChain)</c>: Upgrades through versions sequentially, without skipping intermediate versions.</description>
    ///             </item>
    ///             <item>
    ///                 <description><c>2 (CrossVersion)</c>: Directly upgrades from an old version to any newer version.</description>
    ///             </item>
    ///         </list>
    ///     </para>
    /// </remarks>
    [JsonPropertyName("upgradeMode")]
    public int? UpgradeMode { get; set; }

    /// <summary>
    ///     Whether this is a cross-version upgrade package.
    ///     <c>true</c> indicates this package is used to upgrade directly from an old version to a new version,
    ///     rather than through sequential chain upgrades.
    /// </summary>
    [JsonPropertyName("isCrossVersion")]
    public bool? IsCrossVersion { get; set; }

    /// <summary>
    ///     The source version number for cross-version upgrade packages.
    ///     Indicates which source version this differential patch can be applied to.
    /// </summary>
    /// <remarks>
    ///     Only valid when <see cref="IsCrossVersion" /> is <c>true</c>.
    ///     Together with <see cref="ToVersion" />, defines the version scope of the differential patch.
    /// </remarks>
    [JsonPropertyName("fromVersion")]
    public string? FromVersion { get; set; }

    /// <summary>
    ///     The target version number for cross-version upgrade packages.
    ///     Indicates the target version that will be reached after applying this differential patch.
    /// </summary>
    /// <remarks>
    ///     Only valid when <see cref="IsCrossVersion" /> is <c>true</c>.
    ///     Together with <see cref="FromVersion" />, defines the version scope of the differential patch.
    /// </remarks>
    [JsonPropertyName("toVersion")]
    public string? ToVersion { get; set; }

    /// <summary>
    ///     Whether this version package is frozen (archived and not used for active updates).
    ///     Frozen version packages will not be used for update detection or download.
    /// </summary>
    [JsonPropertyName("isFreeze")]
    public bool? IsFreeze { get; set; }
}
