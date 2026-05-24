using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

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
        var filePath = Path.Combine(IpcDir, $"{key}.enc");
        var plainBytes = Encoding.UTF8.GetBytes(value);
        using var aes = Aes.Create();
        aes.Key = _aesKey;
        aes.IV = _aesIV;
        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        File.WriteAllBytes(filePath, encrypted);
    }

    public static string GetEnvironmentVariable(string key)
    {
        var filePath = Path.Combine(Path.GetTempPath(), "GeneralUpdate", "ipc", $"{key}.enc");
        if (!File.Exists(filePath))
            return string.Empty;

        try
        {
            var encrypted = File.ReadAllBytes(filePath);
            using var aes = Aes.Create();
            aes.Key = _aesKey;
            aes.IV = _aesIV;
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            try { File.Delete(filePath); } catch { /* best-effort cleanup */ }
        }
    }
}
