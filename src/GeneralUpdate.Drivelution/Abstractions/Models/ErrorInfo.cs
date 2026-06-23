namespace GeneralUpdate.Drivelution.Abstractions.Models;

/// <summary>
/// 错误信息模型
/// Error information model
/// </summary>
public class ErrorInfo
{
    /// <summary>
    /// 错误码
    /// Error code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 错误类型
    /// Error type
    /// </summary>
    public ErrorType Type { get; set; }

    /// <summary>
    /// 错误消息
    /// Error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 详细错误信息
    /// Detailed error information
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// 异常堆栈跟踪
    /// Exception stack trace
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// 内部异常
    /// Inner exception
    /// </summary>
    public Exception? InnerException { get; set; }

    /// <summary>
    /// 错误发生时间
    /// Error occurrence time
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 是否可以重试
    /// Whether retry is possible
    /// </summary>
    public bool CanRetry { get; set; }

    /// <summary>
    /// 建议的处理方式
    /// Suggested resolution
    /// </summary>
    public string SuggestedResolution { get; set; } = string.Empty;
}

/// <summary>
/// 错误类型枚举
/// Error type enumeration
/// </summary>
public enum ErrorType
{
    /// <summary>权限不足 / Insufficient permission</summary>
    PermissionDenied,
    /// <summary>签名验证失败 / Signature validation failed</summary>
    SignatureValidationFailed,
    /// <summary>哈希验证失败 / Hash validation failed</summary>
    HashValidationFailed,
    /// <summary>兼容性验证失败 / Compatibility validation failed</summary>
    CompatibilityValidationFailed,
    /// <summary>文件不存在 / File not found</summary>
    FileNotFound,
    /// <summary>文件已损坏 / File corrupted</summary>
    FileCorrupted,
    /// <summary>备份失败 / Backup failed</summary>
    BackupFailed,
    /// <summary>安装失败 / Installation failed</summary>
    InstallationFailed,
    /// <summary>回滚失败 / Rollback failed</summary>
    RollbackFailed,
    /// <summary>网络错误 / Network error</summary>
    NetworkError,
    /// <summary>超时 / Timeout</summary>
    Timeout,
    /// <summary>用户取消 / User cancelled</summary>
    UserCancelled,
    /// <summary>系统不支持 / System not supported</summary>
    SystemNotSupported,
    /// <summary>未知错误 / Unknown error</summary>
    Unknown
}
