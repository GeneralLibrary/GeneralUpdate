using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GeneralUpdate.Core.Ipc;

namespace GeneralUpdate.Core.Configuration;

/// <summary>
/// Secure IPC environment variable provider.
/// AES-encrypted temp files in a dedicated subdirectory, auto-deleted after read.
/// </summary>
public static class Environments
{
    // Fixed key/IV derived from a constant — not crypto-grade, but sufficient for
    // ephemeral IPC where the file lives < 1 second and is in a per-user directory.
    private static readonly byte[] _aesKey = SHA256.Create()
        .ComputeHash(Encoding.UTF8.GetBytes("GeneralUpdate.IPC.EnvironmentProvider.v1"));
    private static readonly byte[] _aesIV = new byte[16] { 0x47, 0x55, 0x50, 0x44, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    /// <summary>
    /// Allowed characters for IPC key names: alphanumeric, underscore, hyphen, dot.
    /// </summary>
    private static readonly char[] KeyAllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-.".ToCharArray();

    private static string IpcDir
    {
        get
        {
            var dir = Path.Combine(Path.GetTempPath(), "GeneralUpdate", "ipc");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static void SetEnvironmentVariable(string key, string value)
    {
        ValidateKey(key);
        var filePath = Path.Combine(IpcDir, $"{key}.enc");
        var plainBytes = Encoding.UTF8.GetBytes(value);
        IpcEncryption.EncryptToFile(plainBytes, filePath, _aesKey, _aesIV);
    }

    public static string GetEnvironmentVariable(string key)
    {
        ValidateKey(key);
        var filePath = Path.Combine(Path.GetTempPath(), "GeneralUpdate", "ipc", $"{key}.enc");
        var plainBytes = IpcEncryption.DecryptFromFile(filePath, _aesKey, _aesIV);
        return plainBytes != null ? Encoding.UTF8.GetString(plainBytes) : string.Empty;
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("IPC key must not be null or whitespace.", nameof(key));
        if (key.Any(c => !KeyAllowedChars.Contains(c)))
            throw new ArgumentException(
                $"IPC key '{key}' contains invalid characters. Only alphanumeric, underscore, hyphen, and dot are allowed.",
                nameof(key));
    }
}
