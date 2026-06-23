using System.Diagnostics;
using System.Runtime.Versioning;
using GeneralUpdate.Drivelution.Abstractions.Exceptions;


namespace GeneralUpdate.Drivelution.Linux.Helpers;

/// <summary>
/// Linux签名验证助手
/// Linux signature validation helper
/// </summary>
[SupportedOSPlatform("linux")]
public static class LinuxSignatureHelper
{
    /// <summary>
    /// 验证GPG签名
    /// Validates GPG signature
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="signaturePath">签名文件路径 / Signature file path</param>
    /// <param name="trustedKeys">信任的GPG公钥列表 / Trusted GPG public keys</param>
    /// <returns>是否验证通过 / Whether validation succeeded</returns>
    public static async Task<bool> ValidateGpgSignatureAsync(
        string filePath,
        string signaturePath,
        IEnumerable<string> trustedKeys)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        if (!File.Exists(signaturePath))
        {
            // Signature file not found
            return false;
        }

        try
        {
            // Validating GPG signature for file: {FilePath}

            // Import trusted keys if provided
            if (trustedKeys.Any())
            {
                foreach (var key in trustedKeys)
                {
                    await ImportGpgKeyAsync(key);
                }
            }

            // Verify signature
            var startInfo = new ProcessStartInfo
            {
                FileName = "gpg",
                Arguments = $"--verify \"{signaturePath}\" \"{filePath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                // GPG verification output: {Output} // GPG outputs to stderr

                if (process.ExitCode == 0)
                {
                    // GPG signature validation succeeded
                    return true;
                }
                else
                {
                    // GPG signature validation failed
                    return false;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            // Failed to validate GPG signature
            throw new DriverValidationException(
                $"Failed to validate GPG signature: {ex.Message}",
                "Signature",
                ex);
        }
    }

    /// <summary>
    /// 导入GPG公钥
    /// Imports GPG public key
    /// </summary>
    /// <param name="keyId">公钥ID或文件路径 / Key ID or file path</param>
    private static async Task<bool> ImportGpgKeyAsync(string keyId)
    {
        try
        {
            string arguments;
            if (File.Exists(keyId))
            {
                // Import from file
                arguments = $"--import \"{keyId}\"";
            }
            else
            {
                // Import from keyserver
                arguments = $"--keyserver keyserver.ubuntu.com --recv-keys {keyId}";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "gpg",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }

            return false;
        }
        catch (Exception)
        {
            // Failed to import GPG key
            return false;
        }
    }

    /// <summary>
    /// 检查文件是否有GPG签名
    /// Checks if file has GPG signature
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <returns>是否有签名 / Whether has signature</returns>
    public static bool HasGpgSignature(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        // Check for common signature file extensions
        var signatureExtensions = new[] { ".sig", ".asc", ".sign" };
        return signatureExtensions.Any(ext => File.Exists(filePath + ext));
    }
}
