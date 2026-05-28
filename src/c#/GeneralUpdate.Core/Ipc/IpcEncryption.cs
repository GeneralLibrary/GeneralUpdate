using System.IO;
using System.Security.Cryptography;

namespace GeneralUpdate.Core.Ipc;

/// <summary>
/// Shared AES encryption utilities for IPC.
/// Used by both <see cref="Environments"/> (key-value IPC) and
/// <see cref="EncryptedFileProcessInfoProvider"/> (structured ProcessInfo IPC).
/// </summary>
public static class IpcEncryption
{
    /// <summary>
    /// AES-CBC encrypt <paramref name="plainBytes"/> and write to <paramref name="filePath"/>.
    /// </summary>
    public static void EncryptToFile(byte[] plainBytes, string filePath, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        File.WriteAllBytes(filePath, cipher);
    }

    /// <summary>
    /// Read, AES-CBC decrypt, and auto-delete the file at <paramref name="filePath"/>.
    /// Returns <c>null</c> if the file does not exist.
    /// </summary>
    public static byte[]? DecryptFromFile(string filePath, byte[] key, byte[] iv)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            var cipher = File.ReadAllBytes(filePath);
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        }
        finally
        {
            try { File.Delete(filePath); } catch { /* best-effort cleanup */ }
        }
    }
}
