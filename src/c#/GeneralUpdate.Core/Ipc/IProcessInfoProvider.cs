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
/// AES-encrypted temporary file IPC. Writes to a <b>deterministic</b> path under
/// %TEMP%/GeneralUpdate/ipc/ so that both the client (sender) and upgrade (receiver)
/// processes agree on the file location without needing out-of-band coordination.
/// File is deleted after a successful read.
/// </summary>
public class EncryptedFileProcessInfoProvider : IProcessInfoProvider
{
    private const string FileName = "process_info.enc";

    private static readonly byte[] Key = SHA256.Create()
        .ComputeHash(Encoding.UTF8.GetBytes("GeneralUpdate.ProcessInfo.IPC.v1"));
    private static readonly byte[] IV = new byte[16] { 0x47, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    private readonly string _filePath;

    /// <summary>
    /// Returns the deterministic IPC file path that both client and upgrade agree on.
    /// </summary>
    public static string GetDefaultFilePath(string? basePath = null)
    {
        var dir = basePath ?? Path.Combine(Path.GetTempPath(), "GeneralUpdate", "ipc");
        return Path.Combine(dir, FileName);
    }

    public EncryptedFileProcessInfoProvider(string? basePath = null)
    {
        var dir = basePath ?? Path.Combine(Path.GetTempPath(), "GeneralUpdate", "ipc");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, FileName);
    }

    public Task SendAsync(ProcessInfo info, CancellationToken token = default)
    {
        Send(info);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Encrypt <paramref name="info"/> and write to the deterministic IPC file.
    /// Overwrites any existing file from a previous (stale) session.
    /// </summary>
    public void Send(ProcessInfo info)
    {
        var json = JsonSerializer.Serialize(info, ProcessInfoJsonContext.Default.ProcessInfo);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        IpcEncryption.EncryptToFile(plainBytes, _filePath, Key, IV);
    }

    public Task<ProcessInfo?> ReceiveAsync(CancellationToken token = default)
        => Task.FromResult(Receive());

    /// <summary>
    /// Read and decrypt the IPC file, then delete it so a stale file is never re-read.
    /// Returns null if the file does not exist or decryption fails.
    /// </summary>
    public ProcessInfo? Receive()
    {
        if (!File.Exists(_filePath)) return null;

        var plain = IpcEncryption.DecryptFromFile(_filePath, Key, IV);
        if (plain == null) return null;

        try { File.Delete(_filePath); }
        catch { /* best-effort cleanup */ }

        var json = Encoding.UTF8.GetString(plain);
        return JsonSerializer.Deserialize(json, ProcessInfoJsonContext.Default.ProcessInfo);
    }
}
