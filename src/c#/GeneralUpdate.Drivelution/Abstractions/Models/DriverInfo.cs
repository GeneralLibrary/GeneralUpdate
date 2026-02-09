namespace GeneralUpdate.Drivelution.Abstractions.Models;

/// <summary>
/// 驱动信息模型
/// Driver information model
/// </summary>
public class DriverInfo
{
    /// <summary>
    /// 驱动名称
    /// Driver name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 驱动版本（遵循SemVer 2.0规范）
    /// Driver version (follows SemVer 2.0 specification)
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 驱动文件路径
    /// Driver file path
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 适配操作系统
    /// Supported operating system
    /// </summary>
    public string TargetOS { get; set; } = string.Empty;

    /// <summary>
    /// 适配系统架构（x86, x64, ARM, ARM64）
    /// Supported system architecture (x86, x64, ARM, ARM64)
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// 硬件ID（Windows硬件ID或Linux PCI/USB设备ID）
    /// Hardware ID (Windows hardware ID or Linux PCI/USB device ID)
    /// </summary>
    public string HardwareId { get; set; } = string.Empty;

    /// <summary>
    /// 文件哈希值（用于完整性校验）
    /// File hash (for integrity validation)
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// 哈希算法（SHA256, MD5）
    /// Hash algorithm (SHA256, MD5)
    /// </summary>
    public string HashAlgorithm { get; set; } = "SHA256";

    /// <summary>
    /// 信任的发布者列表
    /// Trusted publishers list
    /// </summary>
    public List<string> TrustedPublishers { get; set; } = new();

    /// <summary>
    /// 驱动描述
    /// Driver description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 驱动发布日期
    /// Driver release date
    /// </summary>
    public DateTime ReleaseDate { get; set; }

    /// <summary>
    /// 附加元数据
    /// Additional metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
