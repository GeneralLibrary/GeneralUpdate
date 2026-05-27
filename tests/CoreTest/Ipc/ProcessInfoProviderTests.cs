using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Ipc;
using Xunit;

namespace CoreTest.Ipc;

public class ProcessInfoProviderTests
{
    [Fact]
    public async Task EncryptedFileProvider_SendReceive_RoundTrips()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "CoreTest.Ipc", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        try
        {
            var provider = new EncryptedFileProcessInfoProvider(tmpDir);
            var info = new ProcessInfo(
                appName: "test-app",
                installPath: Path.GetTempPath(),
                currentVersion: "1.0.0",
                lastVersion: "2.0.0",
                updateLogUrl: "https://example.com/log",
                compressEncoding: System.Text.Encoding.UTF8,
                compressFormat: "ZIP",
                downloadTimeOut: 30,
                appSecretKey: "secret",
                updateVersions: new List<VersionInfo> { new() { Version = "1.0.0", Name = "test" } },
                reportUrl: "https://example.com/report",
                backupDirectory: "/tmp/backup",
                bowl: "",
                scheme: "",
                token: "",
                driverDirectory: "",
                tempPath: "",
                blackFileFormats: new List<string> { ".pdb" },
                blackFiles: new List<string> { "test.dll" },
                skipDirectories: new List<string> { "logs" }
            );

            await provider.SendAsync(info);
            var result = await provider.ReceiveAsync();

            Assert.NotNull(result);
            Assert.Equal("test-app", result!.AppName);
            Assert.Equal("1.0.0", result.CurrentVersion);
            Assert.NotEmpty(result.BlackFileFormats);

            Assert.False(Directory.GetFiles(tmpDir, "*.enc").Length > 0,
                "Encrypted file should be deleted after ReceiveAsync");
        }
        finally
        {
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        }
    }

    [Fact]
    public async Task EncryptedFileProvider_NoFile_ReturnsNull()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "CoreTest.Ipc.Empty." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        try
        {
            var provider = new EncryptedFileProcessInfoProvider(tmpDir);
            var result = await provider.ReceiveAsync();
            Assert.Null(result);
        }
        finally
        {
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        }
    }
}
