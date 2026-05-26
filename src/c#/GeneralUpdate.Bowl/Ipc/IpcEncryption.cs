using System;
using System.IO;
using System.Security.Cryptography;

namespace GeneralUpdate.Bowl.Ipc;

/// <summary>
/// Shared AES encryption utilities for IPC.
/// </summary>
public static class IpcEncryption
{
    public static void EncryptToFile(byte[] plainBytes, string filePath, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        File.WriteAllBytes(filePath, cipher);
    }

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
