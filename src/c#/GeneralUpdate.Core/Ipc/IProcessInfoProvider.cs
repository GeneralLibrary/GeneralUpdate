using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.JsonContext;

namespace GeneralUpdate.Core.Ipc;

/// <summary>IPC provider for Client-to-Upgrade process communication.</summary>
public interface IProcessInfoProvider
{
    Task SendAsync(ProcessInfo info, CancellationToken token = default);
    Task<ProcessInfo?> ReceiveAsync(CancellationToken token = default);
}

/// <summary>
/// AES-encrypted temporary file IPC — simplest, most reliable cross-platform approach.
/// File lives in %TEMP%/GeneralUpdate/ipc/ with a random name, auto-deleted after read.
/// </summary>
public class EncryptedFileProcessInfoProvider : IProcessInfoProvider
{
    private static readonly byte[] Key = SHA256.Create()
        .ComputeHash(Encoding.UTF8.GetBytes("GeneralUpdate.ProcessInfo.IPC.v1"));
    private static readonly byte[] IV = new byte[16] { 0x47, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    private readonly string _filePath;

    public EncryptedFileProcessInfoProvider(string? basePath = null)
    {
        var dir = basePath ?? Path.Combine(Path.GetTempPath(), "GeneralUpdate", "ipc");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, $"{Guid.NewGuid():N}.enc");
    }

    public Task SendAsync(ProcessInfo info, CancellationToken token = default)
    {
        Send(info);
        return Task.CompletedTask;
    }

    /// <summary>Synchronous send — all I/O is synchronous under the hood.</summary>
    public void Send(ProcessInfo info)
    {
        var json = JsonSerializer.Serialize(info, ProcessInfoJsonContext.Default.ProcessInfo);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        using var aes = Aes.Create();
        aes.Key = Key; aes.IV = IV;
        using var enc = aes.CreateEncryptor();
        var cipher = enc.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        File.WriteAllBytes(_filePath, cipher);
    }

    public Task<ProcessInfo?> ReceiveAsync(CancellationToken token = default)
        => Task.FromResult(Receive());

    /// <summary>Synchronous receive — reads and deletes the encrypted file.</summary>
    public ProcessInfo? Receive()
    {
        if (!File.Exists(_filePath)) return null;
        try
        {
            var cipher = File.ReadAllBytes(_filePath);
            using var aes = Aes.Create();
            aes.Key = Key; aes.IV = IV;
            using var dec = aes.CreateDecryptor();
            var plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
            var json = Encoding.UTF8.GetString(plain);
            return JsonSerializer.Deserialize(json, ProcessInfoJsonContext.Default.ProcessInfo);
        }
        finally { try { File.Delete(_filePath); } catch { } }
    }
}
