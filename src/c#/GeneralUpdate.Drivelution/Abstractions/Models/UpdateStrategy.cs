namespace GeneralUpdate.Drivelution.Abstractions.Models;

/// <summary>
/// 更新策略模型
/// Update strategy model
/// </summary>
public class UpdateStrategy
{
    /// <summary>
    /// 更新模式（全量更新/增量更新）
    /// Update mode (Full/Incremental)
    /// </summary>
    public UpdateMode Mode { get; set; } = UpdateMode.Full;

    /// <summary>
    /// 是否强制更新（强制更新不允许用户取消）
    /// Force update (forced update cannot be cancelled by user)
    /// </summary>
    public bool ForceUpdate { get; set; } = false;

    /// <summary>
    /// 是否需要备份
    /// Whether backup is required
    /// </summary>
    public bool RequireBackup { get; set; } = true;

    /// <summary>
    /// 备份路径
    /// Backup path
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    /// 失败重试次数
    /// Retry count on failure
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试间隔（秒）
    /// Retry interval (seconds)
    /// </summary>
    public int RetryIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// 更新优先级（用于分批更新）
    /// Update priority (for batch updates)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// 更新完成后是否需要重启
    /// Whether restart is required after update
    /// </summary>
    public RestartMode RestartMode { get; set; } = RestartMode.Prompt;

    /// <summary>
    /// 是否跳过签名验证（仅调试模式）
    /// Skip signature validation (debug mode only)
    /// </summary>
    public bool SkipSignatureValidation { get; set; } = false;

    /// <summary>
    /// 是否跳过哈希验证（仅调试模式）
    /// Skip hash validation (debug mode only)
    /// </summary>
    public bool SkipHashValidation { get; set; } = false;

    /// <summary>
    /// 超时时间（秒）
    /// Timeout (seconds)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
}

/// <summary>
/// 更新模式枚举
/// Update mode enumeration
/// </summary>
public enum UpdateMode
{
    /// <summary>全量更新 / Full update</summary>
    Full,
    /// <summary>增量更新 / Incremental update</summary>
    Incremental
}

/// <summary>
/// 重启模式枚举
/// Restart mode enumeration
/// </summary>
public enum RestartMode
{
    /// <summary>不需要重启 / No restart required</summary>
    None,
    /// <summary>提示用户重启 / Prompt user to restart</summary>
    Prompt,
    /// <summary>延迟重启 / Delayed restart</summary>
    Delayed,
    /// <summary>立即重启 / Immediate restart</summary>
    Immediate
}
