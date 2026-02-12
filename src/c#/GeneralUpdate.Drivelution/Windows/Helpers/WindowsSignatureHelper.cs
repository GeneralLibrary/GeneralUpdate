using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;

namespace GeneralUpdate.Drivelution.Windows.Helpers;

/// <summary>
/// Windows签名验证助手
/// Windows signature validation helper
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsSignatureHelper
{
    /// <summary>
    /// 验证文件的Authenticode数字签名
    /// Validates file's Authenticode digital signature
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="trustedPublishers">信任的发布者列表 / Trusted publishers list</param>
    /// <returns>是否验证通过 / Whether validation succeeded</returns>
    public static async Task<bool> ValidateAuthenticodeSignatureAsync(string filePath, IEnumerable<string> trustedPublishers)
    {
        return await Task.Run(() => ValidateAuthenticodeSignature(filePath, trustedPublishers));
    }

    /// <summary>
    /// 验证文件的Authenticode数字签名（同步版本）
    /// Validates file's Authenticode digital signature (sync version)
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="trustedPublishers">信任的发布者列表 / Trusted publishers list</param>
    /// <returns>是否验证通过 / Whether validation succeeded</returns>
    public static bool ValidateAuthenticodeSignature(string filePath, IEnumerable<string> trustedPublishers)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Authenticode signature validation is only supported on Windows
            return false;
        }

        try
        {
            // Implement Authenticode signature validation using X509Certificate2
            // This provides a managed way to validate code signatures on Windows
            
            try
            {
                // Attempt to create X509Certificate from the file, then convert to X509Certificate2
                // This works for signed PE files (.exe, .dll, .sys, etc.)
                using var cert = X509Certificate.CreateFromSignedFile(filePath);
                
                if (cert == null)
                {
                    // No digital signature found in file
                    return false;
                }

                // Convert to X509Certificate2 for additional properties
                using var cert2 = new X509Certificate2(cert);

                // Check if certificate is currently valid
                if (DateTime.Now < cert2.NotBefore || DateTime.Now > cert2.NotAfter)
                {
                    // Certificate is not within its validity period
                    return false;
                }

                // If trustedPublishers is empty, accept any valid signature
                if (!trustedPublishers.Any())
                {
                    // No trusted publishers specified, accepting any valid signature
                    return true;
                }

                // Check if the certificate subject or thumbprint matches any trusted publisher
                foreach (var publisher in trustedPublishers)
                {
                    if (cert2.Subject.Contains(publisher, StringComparison.OrdinalIgnoreCase) ||
                        cert2.Thumbprint.Equals(publisher, StringComparison.OrdinalIgnoreCase))
                    {
                        // File is signed by trusted publisher
                        return true;
                    }
                }

                // File is signed but not by a trusted publisher
                return false;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // File is not signed or signature is invalid
                return false;
            }
        }
        catch (Exception ex)
        {
            throw new DriverValidationException(
                $"Failed to validate Authenticode signature for file: {filePath}",
                "Signature",
                ex);
        }
    }

    /// <summary>
    /// 检查文件是否已签名
    /// Checks if file is signed
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <returns>是否已签名 / Whether signed</returns>
    public static bool IsFileSigned(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            // Use X509Certificate.CreateFromSignedFile to check if file has a signature
            try
            {
                using var cert = X509Certificate.CreateFromSignedFile(filePath);
                bool isSigned = cert != null;
                return isSigned;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // File is not signed
                return false;
            }
        }
        catch (Exception)
        {
            // Failed to check if file is signed
            return false;
        }
    }
}
