using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GeneralUpdate.Core.Ipc;

namespace GeneralUpdate.Core.Configuration;

/// <summary>
/// Secure IPC environment variable provider.
/// AES-encrypted temp files in a dedicated subdirectory, auto-deleted after read.
/// Encryption is delegated to <see cref="IpcEncryption"/>.
/// </summary>
public static class Environments
{
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
        IpcEncryption.EncryptToFile(plainBytes, filePath, _aesKey, _aesIV);
    }

    public static string GetEnvironmentVariable(string key)
    {
        var filePath = Path.Combine(Path.GetTempPath(), "GeneralUpdate", "ipc", $"{key}.enc");
        var plainBytes = IpcEncryption.DecryptFromFile(filePath, _aesKey, _aesIV);
        return plainBytes != null ? Encoding.UTF8.GetString(plainBytes) : string.Empty;
    }
}
