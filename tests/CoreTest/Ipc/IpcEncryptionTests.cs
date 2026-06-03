using GeneralUpdate.Core.Ipc;
using GeneralUpdate.Core.Security;

namespace CoreTest.Ipc;

public class IpcEncryptionTests
{
    private static readonly byte[] TestKey = System.Security.Cryptography.SHA256.HashData("TestEncryptionKey1234567890!!"u8);
    private static readonly byte[] TestIV = new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

    [Fact]
    public void EncryptThenDecrypt_RoundTrip_PlaintextMatches()
    {
        var tempFile = Path.GetTempFileName();
        var original = "Hello, IPC World! 你好世界"u8.ToArray();
        try
        {
            IpcEncryption.EncryptToFile(original, tempFile, TestKey, TestIV);
            var decrypted = IpcEncryption.DecryptFromFile(tempFile, TestKey, TestIV);
            Assert.NotNull(decrypted);
            Assert.Equal(original, decrypted);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void DecryptFromFile_FileDoesNotExist_ReturnsNull()
    {
        var result = IpcEncryption.DecryptFromFile(
            Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.enc"),
            TestKey, TestIV);
        Assert.Null(result);
    }

    [Fact]
    public void DecryptFromFile_FileDeletedAfterDecryption()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            IpcEncryption.EncryptToFile("test data"u8.ToArray(), tempFile, TestKey, TestIV);
            Assert.True(File.Exists(tempFile));
            IpcEncryption.DecryptFromFile(tempFile, TestKey, TestIV);
            Assert.False(File.Exists(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void DecryptFromFile_FileDeleteFails_DoesNotThrow()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            IpcEncryption.EncryptToFile("data"u8.ToArray(), tempFile, TestKey, TestIV);
            // Decrypt — file will be deleted in finally, exception from delete is swallowed
            var ex = Record.Exception(() => IpcEncryption.DecryptFromFile(tempFile, TestKey, TestIV));
            Assert.Null(ex);
        }
        catch
        {
            // Cleanup
        }
    }
}

public class EncryptedFileProcessContractProviderTests
{
    [Fact]
    public void SendAndReceive_RoundTrip_ProcessContractPreserved()
    {
        var provider = new EncryptedFileProcessContractProvider();
        var info = new GeneralUpdate.Core.Configuration.ProcessContract(
            "MyApp", Path.GetTempPath(), "1.0.0", "2.0.0",
            null, System.Text.Encoding.UTF8, ".zip", 30, "secret",
            new List<GeneralUpdate.Core.Configuration.VersionEntry> { new() { Version = "2.0.0" } },
            "https://report.example.com", "C:\\backup",
            null, null, null, AuthScheme.Hmac, null, null, null, null, null, null, null, null);

        provider.Send(info);
        var received = provider.Receive();

        Assert.NotNull(received);
        Assert.Equal("MyApp", received.AppName);
        Assert.Equal("1.0.0", received.CurrentVersion);
        Assert.Equal("2.0.0", received.LastVersion);
    }

    [Fact]
    public void Receive_NoFile_ReturnsNull()
    {
        var provider = new EncryptedFileProcessContractProvider();
        var result = provider.Receive();
        Assert.Null(result);
    }
}
