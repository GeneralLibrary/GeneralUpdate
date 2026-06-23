using System;
using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Ipc;
using Xunit;

namespace CoreTest.Ipc;

[Collection("IpcTests")]
public class EncryptedFileIpcTests : IDisposable
{
    private readonly string _tmpDir;

    public EncryptedFileIpcTests()
    {
        // Each test instance gets its own isolated IPC directory to prevent
        // cross-test file collisions (especially on Linux where file-system
        // operation ordering differs from Windows).
        _tmpDir = Path.Combine(Path.GetTempPath(), "CoreTest.Ipc", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, true); } catch { /* best-effort */ }
    }

    private EncryptedFileProcessContractProvider CreateProvider()
    {
        return new EncryptedFileProcessContractProvider(_tmpDir);
    }

    private static ProcessContract CreateTestInfo(string appName, string currentVersion)
    {
        return new ProcessContract
        {
            AppName = appName,
            CurrentVersion = currentVersion,
            LastVersion = "2.0.0",
            InstallPath = "/test/path"
        };
    }

    [Fact]
    public void Send_And_Receive_RoundTrip()
    {
        var provider = CreateProvider();
        var info = CreateTestInfo("TestApp", "1.0.0");

        provider.Send(info);
        var received = provider.Receive();

        Assert.NotNull(received);
        Assert.Equal("TestApp", received!.AppName);
        Assert.Equal("1.0.0", received.CurrentVersion);
    }

    [Fact]
    public void Receive_Without_Send_ReturnsNull()
    {
        var provider = CreateProvider();
        var received = provider.Receive();
        Assert.Null(received);
    }

    [Fact]
    public void Data_Confidentiality_And_SingleRead()
    {
        var provider = CreateProvider();
        var info = CreateTestInfo("SecureApp", "1.0.0");

        provider.Send(info);

        var received = provider.Receive();
        Assert.NotNull(received);
        Assert.Equal("SecureApp", received!.AppName);

        // Second receive should return null (file was auto-deleted)
        var second = provider.Receive();
        Assert.Null(second);
    }

    [Fact]
    public async Task Async_Api_Delegates_To_Sync()
    {
        var provider = CreateProvider();
        var info = CreateTestInfo("AsyncApp", "1.0.0");

        await provider.SendAsync(info);
        var received = await provider.ReceiveAsync();

        Assert.NotNull(received);
        Assert.Equal("AsyncApp", received!.AppName);
    }
}
