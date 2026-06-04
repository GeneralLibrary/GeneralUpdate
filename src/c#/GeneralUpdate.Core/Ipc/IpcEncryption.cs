using System;
using System.IO;
using System.Security.Cryptography;

namespace GeneralUpdate.Core.Ipc;

/// <summary>
/// Shared AES encryption utilities for IPC.
/// Used by both <see cref="Environments"/> (key-value IPC) and
/// <see cref="EncryptedFileProcessContractProvider"/> (structured ProcessContract IPC).
/// </summary>
public static class IpcEncryption
{
    /// <summary>
    /// AES-CBC encrypt <paramref name="plainBytes"/> and write to <paramref name="filePath"/>.
    /// Uses <see cref="FileShare.Read"/> so that a concurrent receiver can begin reading
    /// before this writer closes the handle.
    /// </summary>
    public static void EncryptToFile(byte[] plainBytes, string filePath, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        fs.Write(cipher, 0, cipher.Length);
    }

    /// <summary>
    /// Read, AES-CBC decrypt, and auto-delete the file at <paramref name="filePath"/>.
    /// Returns <c>null</c> if the file does not exist or cannot be accessed (e.g. locked
    /// by a concurrent process — common during parallel test execution).
    /// </summary>
    public static byte[]? DecryptFromFile(string filePath, byte[] key, byte[] iv)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            byte[] cipher;
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                cipher = new byte[fs.Length];
                int offset = 0, remaining = cipher.Length;
                while (remaining > 0)
                {
                    int n = fs.Read(cipher, offset, remaining);
                    if (n == 0) return null;
                    offset += n;
                    remaining -= n;
                }
            }
            catch (IOException)
            {
                // File is locked by another process (e.g. parallel test runs) — gracefully
                // treat as "no IPC data available" instead of crashing.
                return null;
            }

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
