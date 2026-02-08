namespace GeneralUpdate.Drivelution.Abstractions.Exceptions;

/// <summary>
/// 驱动更新基础异常
/// Base exception for driver update
/// </summary>
public class DrivelutionException : Exception
{
    public string ErrorCode { get; set; }
    public bool CanRetry { get; set; }

    public DrivelutionException(string message, string errorCode = "DU_UNKNOWN", bool canRetry = false)
        : base(message)
    {
        ErrorCode = errorCode;
        CanRetry = canRetry;
    }

    public DrivelutionException(string message, Exception innerException, string errorCode = "DU_UNKNOWN", bool canRetry = false)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        CanRetry = canRetry;
    }
}

/// <summary>
/// 权限异常
/// Permission exception
/// </summary>
public class DriverPermissionException : DrivelutionException
{
    public DriverPermissionException(string message)
        : base(message, "DU_PERMISSION_DENIED", false)
    {
    }

    public DriverPermissionException(string message, Exception innerException)
        : base(message, innerException, "DU_PERMISSION_DENIED", false)
    {
    }
}

/// <summary>
/// 校验失败异常
/// Validation failed exception
/// </summary>
public class DriverValidationException : DrivelutionException
{
    public string ValidationType { get; set; }

    public DriverValidationException(string message, string validationType)
        : base(message, "DU_VALIDATION_FAILED", false)
    {
        ValidationType = validationType;
    }

    public DriverValidationException(string message, string validationType, Exception innerException)
        : base(message, innerException, "DU_VALIDATION_FAILED", false)
    {
        ValidationType = validationType;
    }
}

/// <summary>
/// 安装失败异常
/// Installation failed exception
/// </summary>
public class DriverInstallationException : DrivelutionException
{
    public DriverInstallationException(string message, bool canRetry = true)
        : base(message, "DU_INSTALLATION_FAILED", canRetry)
    {
    }

    public DriverInstallationException(string message, Exception innerException, bool canRetry = true)
        : base(message, innerException, "DU_INSTALLATION_FAILED", canRetry)
    {
    }
}

/// <summary>
/// 备份失败异常
/// Backup failed exception
/// </summary>
public class DriverBackupException : DrivelutionException
{
    public DriverBackupException(string message)
        : base(message, "DU_BACKUP_FAILED", true)
    {
    }

    public DriverBackupException(string message, Exception innerException)
        : base(message, innerException, "DU_BACKUP_FAILED", true)
    {
    }
}

/// <summary>
/// 回滚失败异常
/// Rollback failed exception
/// </summary>
public class DriverRollbackException : DrivelutionException
{
    public DriverRollbackException(string message)
        : base(message, "DU_ROLLBACK_FAILED", false)
    {
    }

    public DriverRollbackException(string message, Exception innerException)
        : base(message, innerException, "DU_ROLLBACK_FAILED", false)
    {
    }
}

/// <summary>
/// 兼容性异常
/// Compatibility exception
/// </summary>
public class DriverCompatibilityException : DrivelutionException
{
    public DriverCompatibilityException(string message)
        : base(message, "DU_COMPATIBILITY_FAILED", false)
    {
    }

    public DriverCompatibilityException(string message, Exception innerException)
        : base(message, innerException, "DU_COMPATIBILITY_FAILED", false)
    {
    }
}
