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
    /// The payload is written to a sibling temp file first and atomically published, so a
    /// reader never observes a truncated ciphertext or a missing contract file if the writer
    /// crashes midway (<see cref="File.Replace"/> swaps in one OS operation when the
    /// destination exists; <see cref="File.Move"/> covers first publication).
    /// </summary>
    public static void EncryptToFile(byte[] plainBytes, string filePath, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var tempPath = filePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.Write(cipher, 0, cipher.Length);
            }
            // File.Move(src, dst, overwrite) is unavailable on netstandard2.0.
            if (File.Exists(filePath)) File.Replace(tempPath, filePath, null);
            else File.Move(tempPath, filePath);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
            throw;
        }
    }

    /// <summary>
    /// Read, AES-CBC decrypt, and auto-delete the file at <paramref name="filePath"/>.
    /// Returns <c>null</c> if the file does not exist, cannot be accessed (e.g. locked
    /// by a concurrent process — common during parallel test execution), or cannot be
    /// decrypted (truncated/corrupted ciphertext, wrong key — e.g. a leftover file from
    /// another product since the file name is shared).
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
                // Guard against files > 2 GB — IPC payloads should never be that large.
                if (fs.Length > int.MaxValue) return null;
                cipher = new byte[(int)fs.Length];
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
        catch (CryptographicException)
        {
            // Corrupted/truncated ciphertext or a key mismatch must not escape as an
            // exception: callers treat "null" as "no IPC data available" and the updater
            // degrades gracefully instead of crashing with a confusing stack.
            return null;
        }
        finally
        {
            try { File.Delete(filePath); } catch { /* best-effort cleanup */ }
        }
    }
}
