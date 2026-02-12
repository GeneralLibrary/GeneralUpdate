using GeneralUpdate.Drivelution.Abstractions.Models;

namespace GeneralUpdate.Drivelution.Abstractions;

/// <summary>
/// 驱动验证器接口
/// Driver validator interface
/// </summary>
public interface IDriverValidator
{
    /// <summary>
    /// 异步验证驱动文件完整性（哈希校验）
    /// Validates driver file integrity (hash validation) asynchronously
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="expectedHash">期望的哈希值 / Expected hash</param>
    /// <param name="hashAlgorithm">哈希算法 / Hash algorithm (SHA256, MD5)</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>验证是否通过 / Validation result</returns>
    Task<bool> ValidateIntegrityAsync(string filePath, string expectedHash, string hashAlgorithm = "SHA256", CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步验证驱动数字签名
    /// Validates driver digital signature asynchronously
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="trustedPublishers">信任的发布者列表 / Trusted publishers list</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>验证是否通过 / Validation result</returns>
    Task<bool> ValidateSignatureAsync(string filePath, IEnumerable<string> trustedPublishers, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步验证驱动兼容性
    /// Validates driver compatibility asynchronously
    /// </summary>
    /// <param name="driverInfo">驱动信息 / Driver information</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>验证是否通过 / Validation result</returns>
    Task<bool> ValidateCompatibilityAsync(DriverInfo driverInfo, CancellationToken cancellationToken = default);
}
