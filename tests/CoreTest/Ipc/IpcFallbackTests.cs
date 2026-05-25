using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Ipc;
using Xunit;

namespace CoreTest.Ipc;

public class EncryptedFileIpcTests
{
    private static ProcessInfo CreateTestInfo(string appName, string currentVersion)
    {
        return new ProcessInfo
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
        var provider = new EncryptedFileProcessInfoProvider();
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
        var provider = new EncryptedFileProcessInfoProvider();
        var received = provider.Receive();
        Assert.Null(received);
    }

    [Fact]
    public void Data_Confidentiality_And_SingleRead()
    {
        var provider = new EncryptedFileProcessInfoProvider();
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
        var provider = new EncryptedFileProcessInfoProvider();
        var info = CreateTestInfo("AsyncApp", "1.0.0");

        await provider.SendAsync(info);
        var received = await provider.ReceiveAsync();

        Assert.NotNull(received);
        Assert.Equal("AsyncApp", received!.AppName);
    }
}
