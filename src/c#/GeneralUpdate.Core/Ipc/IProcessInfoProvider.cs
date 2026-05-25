using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core;
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
        token.ThrowIfCancellationRequested();
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

/// <summary>Shared memory fallback IPC (Linux-friendly, no file residue).</summary>
public class SharedMemoryProcessInfoProvider : IProcessInfoProvider
{
    private readonly string _mapName;
    private MemoryMappedFile? _mmf;
    private const int MaxPayload = 4096;

    public SharedMemoryProcessInfoProvider(string mapName = "GeneralUpdate.IPC.Shm")
        => _mapName = mapName;

    public Task SendAsync(ProcessInfo info, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(info, ProcessInfoJsonContext.Default.ProcessInfo);
        var bytes = Encoding.UTF8.GetBytes(json);
        if (bytes.Length > MaxPayload - 4)
            throw new InvalidOperationException($"ProcessInfo payload exceeds {MaxPayload - 4} bytes.");

        _mmf = MemoryMappedFile.CreateOrOpen(_mapName, MaxPayload);
        using var accessor = _mmf.CreateViewAccessor(0, MaxPayload);
        accessor.Write(0, bytes.Length);
        accessor.WriteArray(4, bytes, 0, bytes.Length);
        return Task.CompletedTask;
    }

    public Task<ProcessInfo?> ReceiveAsync(CancellationToken token = default)
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(_mapName);
            using var accessor = mmf.CreateViewAccessor(0, MaxPayload);
            int length = accessor.ReadInt32(0);
            if (length <= 0 || length > MaxPayload - 4)
                return Task.FromResult<ProcessInfo?>(null);
            var bytes = new byte[length];
            accessor.ReadArray(4, bytes, 0, length);
            var json = Encoding.UTF8.GetString(bytes);
            return Task.FromResult<ProcessInfo?>(
                JsonSerializer.Deserialize(json, ProcessInfoJsonContext.Default.ProcessInfo));
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<ProcessInfo?>(null);
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<ProcessInfo?>(null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Platform-specific failures (e.g. Linux /dev/shm not mounted)
            GeneralTracer.Warn($"SharedMemoryProvider: receive failed: {ex.Message}");
            return Task.FromResult<ProcessInfo?>(null);
        }
    }
}

/// <summary>
/// Auto-fallback IPC provider. Tries providers in order:
/// NamedPipe → SharedMemory → EncryptedFile.
/// On send, uses the first provider that succeeds.
/// On receive, waits for data from the most reliable available provider.
/// </summary>
public class AutoProcessInfoProvider : IProcessInfoProvider
{
    private readonly IProcessInfoProvider[] _providers;

    public AutoProcessInfoProvider()
    {
        _providers = new IProcessInfoProvider[]
        {
            new NamedPipeProcessInfoProvider(),
            new SharedMemoryProcessInfoProvider(),
            new EncryptedFileProcessInfoProvider()
        };
    }

    public AutoProcessInfoProvider(params IProcessInfoProvider[] providers)
        => _providers = providers;

    public async Task SendAsync(ProcessInfo info, CancellationToken token = default)
    {
        Exception? last = null;
        foreach (var provider in _providers)
        {
            try
            {
                await provider.SendAsync(info, token).ConfigureAwait(false);
                GeneralTracer.Debug($"AutoProcessInfoProvider: sent via {provider.GetType().Name}.");
                return;
            }
            catch (Exception ex)
            {
                GeneralTracer.Warn($"AutoProcessInfoProvider: {provider.GetType().Name} failed: {ex.Message}");
                last = ex;
            }
        }
        throw new InvalidOperationException("All IPC providers failed to send.", last);
    }

    public async Task<ProcessInfo?> ReceiveAsync(CancellationToken token = default)
    {
        foreach (var provider in _providers)
        {
            try
            {
                var result = await provider.ReceiveAsync(token).ConfigureAwait(false);
                if (result != null)
                {
                    GeneralTracer.Debug($"AutoProcessInfoProvider: received via {provider.GetType().Name}.");
                    return result;
                }
            }
            catch (Exception ex)
            {
                GeneralTracer.Warn($"AutoProcessInfoProvider: {provider.GetType().Name} receive failed: {ex.Message}");
            }
        }
        return null;
    }
}
