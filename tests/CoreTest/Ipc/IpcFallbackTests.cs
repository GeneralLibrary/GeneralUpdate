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
    private static readonly string IpcFilePath =
        EncryptedFileProcessContractProvider.GetDefaultFilePath();

    public EncryptedFileIpcTests()
    {
        // Clean up any leftover IPC file from a previous (possibly parallel) test run
        // to ensure deterministic test behaviour.
        try { if (File.Exists(IpcFilePath)) File.Delete(IpcFilePath); } catch { /* best-effort */ }
    }

    public void Dispose()
    {
        try { if (File.Exists(IpcFilePath)) File.Delete(IpcFilePath); } catch { /* best-effort */ }
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
        var provider = new EncryptedFileProcessContractProvider();
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
        var provider = new EncryptedFileProcessContractProvider();
        var received = provider.Receive();
        Assert.Null(received);
    }

    [Fact]
    public void Data_Confidentiality_And_SingleRead()
    {
        var provider = new EncryptedFileProcessContractProvider();
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
        var provider = new EncryptedFileProcessContractProvider();
        var info = CreateTestInfo("AsyncApp", "1.0.0");

        await provider.SendAsync(info);
        var received = await provider.ReceiveAsync();

        Assert.NotNull(received);
        Assert.Equal("AsyncApp", received!.AppName);
    }
}
