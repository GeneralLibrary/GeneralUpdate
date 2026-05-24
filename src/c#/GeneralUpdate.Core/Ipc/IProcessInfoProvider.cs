using System;
using System.IO;
using System.IO.Pipes;
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

/// <summary>Named pipe IPC — preferred (no file residue).</summary>
public class NamedPipeProcessInfoProvider : IProcessInfoProvider
{
    private readonly string _pipeName;

    public NamedPipeProcessInfoProvider(string pipeName = "GeneralUpdate.IPC")
        => _pipeName = pipeName;

    public async Task SendAsync(ProcessInfo info, CancellationToken token = default)
    {
        using var server = new NamedPipeServerStream(_pipeName, PipeDirection.Out);
        await server.WaitForConnectionAsync(token).ConfigureAwait(false);
        var json = JsonSerializer.Serialize(info, ProcessInfoJsonContext.Default.ProcessInfo);
        var bytes = Encoding.UTF8.GetBytes(json);
        await server.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
    }

    public async Task<ProcessInfo?> ReceiveAsync(CancellationToken token = default)
    {
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.In);
        await client.ConnectAsync(5000, token).ConfigureAwait(false);
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = await client.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
            await ms.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
        var json = Encoding.UTF8.GetString(ms.ToArray());
        return JsonSerializer.Deserialize(json, ProcessInfoJsonContext.Default.ProcessInfo);
    }
}

/// <summary>Encrypted file fallback IPC (AES).</summary>
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
        var json = JsonSerializer.Serialize(info, ProcessInfoJsonContext.Default.ProcessInfo);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        using var aes = Aes.Create();
        aes.Key = Key; aes.IV = IV;
        using var enc = aes.CreateEncryptor();
        var cipher = enc.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        File.WriteAllBytes(_filePath, cipher);
        return Task.CompletedTask;
    }

    public Task<ProcessInfo?> ReceiveAsync(CancellationToken token = default)
    {
        if (!File.Exists(_filePath)) return Task.FromResult<ProcessInfo?>(null);
        try
        {
            var cipher = File.ReadAllBytes(_filePath);
            using var aes = Aes.Create();
            aes.Key = Key; aes.IV = IV;
            using var dec = aes.CreateDecryptor();
            var plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
            var json = Encoding.UTF8.GetString(plain);
            return Task.FromResult<ProcessInfo?>(
                JsonSerializer.Deserialize(json, ProcessInfoJsonContext.Default.ProcessInfo));
        }
        finally { try { File.Delete(_filePath); } catch { } }
    }
}
