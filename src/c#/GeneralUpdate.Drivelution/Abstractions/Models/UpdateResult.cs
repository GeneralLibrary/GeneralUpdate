namespace GeneralUpdate.Drivelution.Abstractions.Models;

/// <summary>
/// 更新结果模型
/// Update result model
/// </summary>
public class UpdateResult
{
    /// <summary>
    /// 更新是否成功
    /// Whether update succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 更新状态
    /// Update status
    /// </summary>
    public UpdateStatus Status { get; set; }

    /// <summary>
    /// 错误信息
    /// Error information
    /// </summary>
    public ErrorInfo? Error { get; set; }

    /// <summary>
    /// 更新开始时间
    /// Update start time
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 更新结束时间
    /// Update end time
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// 更新耗时（毫秒）
    /// Update duration (milliseconds)
    /// </summary>
    public long DurationMs => (long)(EndTime - StartTime).TotalMilliseconds;

    /// <summary>
    /// 备份路径（如果有备份）
    /// Backup path (if backed up)
    /// </summary>
    public string? BackupPath { get; set; }

    /// <summary>
    /// 是否已回滚
    /// Whether rolled back
    /// </summary>
    public bool RolledBack { get; set; }

    /// <summary>
    /// 附加消息
    /// Additional message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 更新步骤日志
    /// Update step logs
    /// </summary>
    public List<string> StepLogs { get; set; } = new();
}

/// <summary>
/// 更新状态枚举
/// Update status enumeration
/// </summary>
public enum UpdateStatus
{
    /// <summary>未开始 / Not started</summary>
    NotStarted,
    /// <summary>验证中 / Validating</summary>
    Validating,
    /// <summary>备份中 / Backing up</summary>
    BackingUp,
    /// <summary>更新中 / Updating</summary>
    Updating,
    /// <summary>验证更新结果 / Verifying update</summary>
    Verifying,
    /// <summary>成功 / Succeeded</summary>
    Succeeded,
    /// <summary>失败 / Failed</summary>
    Failed,
    /// <summary>已回滚 / Rolled back</summary>
    RolledBack
}
