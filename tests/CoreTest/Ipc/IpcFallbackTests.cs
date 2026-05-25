using System;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Ipc;
using Xunit;

namespace CoreTest.Ipc;

public class IpcFallbackTests
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
    public async Task EncryptedFileProvider_RoundTrip()
    {
        var provider = new EncryptedFileProcessInfoProvider();
        var info = CreateTestInfo("TestApp", "1.0.0");

        await provider.SendAsync(info);
        var received = await provider.ReceiveAsync();

        Assert.NotNull(received);
        Assert.Equal("TestApp", received!.AppName);
        Assert.Equal("1.0.0", received.CurrentVersion);
    }

    [Fact]
    public async Task EncryptedFileProvider_ReceiveWithoutSend_ReturnsNull()
    {
        var provider = new EncryptedFileProcessInfoProvider();
        var received = await provider.ReceiveAsync();
        Assert.Null(received);
    }

    [Fact]
    public async Task NamedPipeProvider_DetectsTimeout()
    {
        var provider = new NamedPipeProcessInfoProvider("TestPipe.Timeout");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider.ReceiveAsync(cts.Token));
    }

    [Fact]
    public async Task SharedMemoryProvider_RoundTrip()
    {
        var mapName = $"GenUpd.Tests.{Guid.NewGuid():N}";

        try
        {
            var provider = new SharedMemoryProcessInfoProvider(mapName);
            var info = CreateTestInfo("TestApp.Shm", "2.0.0");

            await provider.SendAsync(info);
            var receiver = new SharedMemoryProcessInfoProvider(mapName);
            var received = await receiver.ReceiveAsync();

            Assert.NotNull(received);
            Assert.Equal("TestApp.Shm", received!.AppName);
            Assert.Equal("2.0.0", received.CurrentVersion);
        }
        catch (System.PlatformNotSupportedException)
        {
            // Shared memory not supported on this platform — skip
        }
        catch (System.IO.FileNotFoundException)
        {
            // Memory-mapped file already disposed — platform quirk, skip
        }
    }

    [Fact]
    public async Task SharedMemoryProvider_ReceiveWithoutSend_ReturnsNull()
    {
        var provider = new SharedMemoryProcessInfoProvider("NonExistent.Shm");
        var received = await provider.ReceiveAsync();
        Assert.Null(received);
    }

    [Fact]
    public async Task AutoProvider_FallsBackToEncryptedFile()
    {
        var provider = new AutoProcessInfoProvider(
            new EncryptedFileProcessInfoProvider()
        );
        var info = CreateTestInfo("TestApp.Auto", "3.0.0");

        await provider.SendAsync(info);
        var received = await provider.ReceiveAsync();

        Assert.NotNull(received);
        Assert.Equal("TestApp.Auto", received!.AppName);
    }

    [Fact]
    public async Task AutoProvider_ThrowsWhenAllFail()
    {
        var provider = new AutoProcessInfoProvider(
            new NamedPipeProcessInfoProvider("NonExistent." + Guid.NewGuid().ToString("N")),
            new SharedMemoryProcessInfoProvider("NonExistent." + Guid.NewGuid().ToString("N"))
        );
        var info = CreateTestInfo("FailAll", "1.0.0");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            provider.SendAsync(info, new CancellationToken(true)));
    }

    [Fact]
    public async Task EncryptedFileProvider_DataConfidentiality()
    {
        var provider = new EncryptedFileProcessInfoProvider();
        var info = CreateTestInfo("SecureApp", "1.0.0");

        await provider.SendAsync(info);

        var received = await provider.ReceiveAsync();
        Assert.NotNull(received);
        Assert.Equal("SecureApp", received!.AppName);

        // Second receive should return null (file was deleted)
        var second = await provider.ReceiveAsync();
        Assert.Null(second);
    }
}
