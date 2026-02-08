using System.Security.Cryptography;
using System.Text;

namespace GeneralUpdate.Drivelution.Core.Utilities;

/// <summary>
/// 哈希验证工具类
/// Hash validation utility
/// </summary>
public static class HashValidator
{
    /// <summary>
    /// 异步计算文件哈希值
    /// Calculates file hash asynchronously
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="hashAlgorithm">哈希算法（SHA256, MD5）/ Hash algorithm (SHA256, MD5)</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>哈希值（十六进制字符串）/ Hash value (hex string)</returns>
    public static async Task<string> ComputeHashAsync(string filePath, string hashAlgorithm = "SHA256", CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        
        byte[] hashBytes;
        switch (hashAlgorithm.ToUpperInvariant())
        {
            case "SHA256":
                hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
                break;
            case "MD5":
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms (MD5 supported for compatibility)
                hashBytes = await MD5.HashDataAsync(stream, cancellationToken);
#pragma warning restore CA5351
                break;
            default:
                throw new ArgumentException($"Unsupported hash algorithm: {hashAlgorithm}");
        }

        return ByteArrayToHexString(hashBytes);
    }

    /// <summary>
    /// 异步验证文件哈希值
    /// Validates file hash asynchronously
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="expectedHash">期望的哈希值 / Expected hash</param>
    /// <param name="hashAlgorithm">哈希算法 / Hash algorithm</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>是否匹配 / Whether it matches</returns>
    public static async Task<bool> ValidateHashAsync(string filePath, string expectedHash, string hashAlgorithm = "SHA256", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            throw new ArgumentException("Expected hash cannot be null or empty");
        }

        var actualHash = await ComputeHashAsync(filePath, hashAlgorithm, cancellationToken);
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 计算字符串哈希值
    /// Calculates string hash
    /// </summary>
    /// <param name="input">输入字符串 / Input string</param>
    /// <param name="hashAlgorithm">哈希算法 / Hash algorithm</param>
    /// <returns>哈希值（十六进制字符串）/ Hash value (hex string)</returns>
    public static string ComputeStringHash(string input, string hashAlgorithm = "SHA256")
    {
        if (string.IsNullOrEmpty(input))
        {
            throw new ArgumentException("Input string cannot be null or empty");
        }

        var bytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes;

        switch (hashAlgorithm.ToUpperInvariant())
        {
            case "SHA256":
                hashBytes = SHA256.HashData(bytes);
                break;
            case "MD5":
#pragma warning disable CA5351
                hashBytes = MD5.HashData(bytes);
#pragma warning restore CA5351
                break;
            default:
                throw new ArgumentException($"Unsupported hash algorithm: {hashAlgorithm}");
        }

        return ByteArrayToHexString(hashBytes);
    }

    /// <summary>
    /// 将字节数组转换为十六进制字符串
    /// Converts byte array to hex string
    /// </summary>
    private static string ByteArrayToHexString(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.AppendFormat("{0:x2}", b);
        }
        return sb.ToString();
    }
}
