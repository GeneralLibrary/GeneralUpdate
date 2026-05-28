using System;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Core.Configuration;

/// <summary>
///     表示更新服务器返回的版本信息对象。
///     对应服务端 JSON 响应的数据结构，包含版本标识、下载地址、更新日志、
///     升级模式等所有与单个版本相关的元数据。
/// </summary>
/// <remarks>
///     <para>
///         <c>VersionInfo</c> 是客户端与更新服务器之间的数据契约，从版本检查 API 的
///         JSON 响应中反序列化而来。每个 <c>VersionInfo</c> 实例代表一个可用的更新版本。
///     </para>
///     <para>
///         此对象在更新流程中的用途：
///         <list type="number">
///             <item>
///                 <description>
///                     版本检查阶段：从服务端获取版本列表，用于判断是否存在新版本。
///                 </description>
///             </item>
///             <item>
///                 <description>
///                     更新下载阶段：通过 <see cref="Url" /> 属性下载更新包。
///                 </description>
///             </item>
///             <item>
///                 <description>
///                     差异更新阶段：利用 <see cref="Hash" /> 进行文件校验，
///                     <see cref="FromVersion" /> 和 <see cref="ToVersion" /> 用于确定差异补丁的作用范围。
///                 </description>
///             </item>
///             <item>
///                 <description>
///                     IPC 传输阶段：作为 <see cref="ProcessInfo.UpdateVersions" /> 的列表元素
///                     传递给升级进程。
///                 </description>
///             </item>
///         </list>
///     </para>
///     <para>
///         所有属性均使用 <see cref="JsonPropertyNameAttribute" /> 注解，映射服务端 JSON 响应的字段名。
///         属性大多为可空类型，以容错服务端可能缺失的字段。
///     </para>
/// </remarks>
/// <seealso cref="ProcessInfo" />
/// <seealso cref="ConfigurationMapper" />
public class VersionInfo
{
    /// <summary>
    ///     版本记录的唯一标识符（主键 ID）。
    /// </summary>
    [JsonPropertyName("recordId")]
    public int RecordId { get; set; }

    /// <summary>
    ///     版本名称或标签（例如 "v1.0.1"、"Release Candidate 2"）。
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    ///     更新包文件的哈希值（通常为 SHA256），用于下载后的完整性校验。
    /// </summary>
    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    /// <summary>
    ///     版本的发布日期。
    /// </summary>
    [JsonPropertyName("releaseDate")]
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    ///     更新包的下载 URL 地址。
    ///     客户端通过此地址下载更新包文件。
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    ///     此版本信息的版本号字符串（例如 "1.0.0.1"）。
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    ///     应用程序类型标识，用于区分主应用更新和升级器更新。
    /// </summary>
    /// <remarks>
    ///     取值为整数枚举：通常 0 表示主应用（Client），1 表示升级器（Upgrade）。
    ///     与 <see cref="BaseConfigInfo.AppType" /> 配合使用，决定此版本是用于
    ///     <see cref="GlobalConfigInfo.IsMainUpdate" /> 还是
    ///     <see cref="GlobalConfigInfo.IsUpgradeUpdate" />。
    /// </remarks>
    [JsonPropertyName("appType")]
    public int? AppType { get; set; }

    /// <summary>
    ///     目标平台标识，用于区分不同操作系统或架构的更新包。
    /// </summary>
    [JsonPropertyName("platform")]
    public int? Platform { get; set; }

    /// <summary>
    ///     此版本关联的产品标识符，用于多产品环境下的版本筛选。
    /// </summary>
    [JsonPropertyName("productId")]
    public string? ProductId { get; set; }

    /// <summary>
    ///     是否强制更新。
    ///     如果为 <c>true</c>，客户端必须执行此更新才能继续使用。
    /// </summary>
    [JsonPropertyName("isForcibly")]
    public bool? IsForcibly { get; set; }

    /// <summary>
    ///     更新包的压缩格式名称（例如 "zip"、"7z"、"tar.gz"）。
    /// </summary>
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    /// <summary>
    ///     更新包文件的大小（字节）。
    /// </summary>
    [JsonPropertyName("size")]
    public long? Size { get; set; }

    /// <summary>
    ///     下载请求的 HTTP 身份验证方案（例如 "Bearer"、"Basic"）。
    /// </summary>
    /// <remarks>
    ///     当更新包的下载需要额外的身份验证时使用，与服务端配置的鉴权方式对应。
    /// </remarks>
    [JsonPropertyName("authScheme")]
    public string? AuthScheme { get; set; }

    /// <summary>
    ///     下载请求的 HTTP 身份验证令牌。
    /// </summary>
    /// <remarks>
    ///     配合 <see cref="AuthScheme" /> 使用，在下载更新包时附加到 HTTP 请求头中进行鉴权。
    /// </remarks>
    [JsonPropertyName("authToken")]
    public string? AuthToken { get; set; }

    /// <summary>
    ///     此版本的更新日志或发行说明文本。
    /// </summary>
    [JsonPropertyName("updateLog")]
    public string? UpdateLog { get; set; }

    /// <summary>
    ///     签名式下载 URL 的过期时间（UTC）。
    ///     过期后 URL 将失效，需要重新获取。
    /// </summary>
    [JsonPropertyName("urlExpireTimeUtc")]
    public DateTime? UrlExpireTimeUtc { get; set; }

    /// <summary>
    ///     升级模式：1 = 版本链式升级（VersionChain），2 = 跨版本升级（CrossVersion）。
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <list type="bullet">
    ///             <item>
    ///                 <description><c>1（VersionChain）</c>：按顺序逐个版本升级，跳过中间版本。</description>
    ///             </item>
    ///             <item>
    ///                 <description><c>2（CrossVersion）</c>：直接从一个旧版本升级到任意新版本。</description>
    ///             </item>
    ///         </list>
    ///     </para>
    /// </remarks>
    [JsonPropertyName("upgradeMode")]
    public int? UpgradeMode { get; set; }

    /// <summary>
    ///     是否为跨版本升级包。
    ///     <c>true</c> 表示此包用于直接从一个旧版本升级到新版本，而非按顺序链式升级。
    /// </summary>
    [JsonPropertyName("isCrossVersion")]
    public bool? IsCrossVersion { get; set; }

    /// <summary>
    ///     跨版本升级包的源版本号。
    ///     表示此差异包可以从哪个源版本应用。
    /// </summary>
    /// <remarks>
    ///     仅当 <see cref="IsCrossVersion" /> 为 <c>true</c> 时有效。
    ///     与 <see cref="ToVersion" /> 共同定义了差异补丁的版本作用范围。
    /// </remarks>
    [JsonPropertyName("fromVersion")]
    public string? FromVersion { get; set; }

    /// <summary>
    ///     跨版本升级包的目标版本号。
    ///     表示此差异包应用后将升级到的目标版本。
    /// </summary>
    /// <remarks>
    ///     仅当 <see cref="IsCrossVersion" /> 为 <c>true</c> 时有效。
    ///     与 <see cref="FromVersion" /> 共同定义了差异补丁的版本作用范围。
    /// </remarks>
    [JsonPropertyName("toVersion")]
    public string? ToVersion { get; set; }

    /// <summary>
    ///     此版本包是否被冻结（归档，不用于活跃更新）。
    ///     冻结的版本包不会被用于更新检测和下载。
    /// </summary>
    [JsonPropertyName("isFreeze")]
    public bool? IsFreeze { get; set; }
}
