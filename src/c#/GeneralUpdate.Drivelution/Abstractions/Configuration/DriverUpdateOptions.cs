namespace GeneralUpdate.Drivelution.Abstractions.Configuration;

/// <summary>
/// 驱动更新配置选项
/// Driver update configuration options
/// </summary>
public class DriverUpdateOptions
{
    /// <summary>
    /// 默认备份路径
    /// Default backup path
    /// </summary>
    public string DefaultBackupPath { get; set; } = "./DriverBackups";

    /// <summary>
    /// 日志级别（Debug/Info/Warn/Error/Fatal）
    /// Log level (Debug/Info/Warn/Error/Fatal)
    /// </summary>
    public string LogLevel { get; set; } = "Info";

    /// <summary>
    /// 日志文件路径
    /// Log file path
    /// </summary>
    public string LogFilePath { get; set; } = "./Logs/driver-update-.log";

    /// <summary>
    /// 是否启用控制台日志
    /// Enable console logging
    /// </summary>
    public bool EnableConsoleLogging { get; set; } = true;

    /// <summary>
    /// 是否启用文件日志
    /// Enable file logging
    /// </summary>
    public bool EnableFileLogging { get; set; } = true;

    /// <summary>
    /// 默认重试次数
    /// Default retry count
    /// </summary>
    public int DefaultRetryCount { get; set; } = 3;

    /// <summary>
    /// 默认重试间隔（秒）
    /// Default retry interval (seconds)
    /// </summary>
    public int DefaultRetryIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// 默认超时时间（秒）
    /// Default timeout (seconds)
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 是否在调试模式下跳过签名验证
    /// Skip signature validation in debug mode
    /// </summary>
    public bool DebugModeSkipSignature { get; set; } = false;

    /// <summary>
    /// 是否在调试模式下跳过哈希验证
    /// Skip hash validation in debug mode
    /// </summary>
    public bool DebugModeSkipHash { get; set; } = false;

    /// <summary>
    /// 权限校验失败时是否强制终止
    /// Force terminate on permission check failure
    /// </summary>
    public bool ForceTerminateOnPermissionFailure { get; set; } = true;

    /// <summary>
    /// 是否自动清理旧备份（保留最近N个）
    /// Auto cleanup old backups (keep recent N)
    /// </summary>
    public bool AutoCleanupBackups { get; set; } = true;

    /// <summary>
    /// 保留的备份数量
    /// Number of backups to keep
    /// </summary>
    public int BackupsToKeep { get; set; } = 5;

    /// <summary>
    /// 信任的证书指纹列表（用于签名验证）
    /// Trusted certificate thumbprints (for signature validation)
    /// </summary>
    public List<string> TrustedCertificateThumbprints { get; set; } = new();

    /// <summary>
    /// 信任的GPG公钥列表（Linux用）
    /// Trusted GPG public keys (for Linux)
    /// </summary>
    public List<string> TrustedGpgKeys { get; set; } = new();
}
