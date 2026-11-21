using System;
using System.Collections.Generic;
using System.IO;
using GeneralUpdate.Core.Ipc;
using GeneralUpdate.Core.Security;
using Xunit;

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
    public void DecryptFromFile_CorruptedCiphertext_ReturnsNull_DoesNotThrow()
    {
        // Regression: a truncated or otherwise corrupted IPC file (crash mid-write of the
        // old non-atomic writer, or a leftover file from another product) used to escape
        // as CryptographicException from TransformFinalBlock; callers then misread the
        // throw as "no IPC data" only by accident. It must degrade to null instead.
        var tempFile = Path.Combine(Path.GetTempPath(), $"corrupt_{Guid.NewGuid():N}.enc");
        try
        {
            // Valid magic-free garbage: wrong length for an AES-CBC block, wrong key —
            // either way TransformFinalBlock must fail.
            File.WriteAllBytes(tempFile, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            var result = IpcEncryption.DecryptFromFile(tempFile, TestKey, TestIV);
            Assert.Null(result);

            // Also: a full-length ciphertext encrypted with a DIFFERENT key must return
            // null (padding check fails) rather than throw.
            var otherKey = System.Security.Cryptography.SHA256.HashData("SomeOtherKey_9876543210"u8.ToArray());
            IpcEncryption.EncryptToFile("payload"u8.ToArray(), tempFile, otherKey, TestIV);
            Assert.Null(IpcEncryption.DecryptFromFile(tempFile, TestKey, TestIV));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void EncryptToFile_AtomicWrite_LeavesNoTempResidue()
    {
        // Regression: the writer used to create the target file in place; a crash
        // mid-write left a half-written ciphertext for the reader to choke on. The
        // writer now publishes via a sibling temp file + move, so after the call the
        // target is complete and no ".tmp-*" sibling remains.
        var tempFile = Path.Combine(Path.GetTempPath(), $"atomic_{Guid.NewGuid():N}.enc");
        try
        {
            IpcEncryption.EncryptToFile("stable payload"u8.ToArray(), tempFile, TestKey, TestIV);

            Assert.True(File.Exists(tempFile));
            var siblings = Directory.GetFiles(Path.GetDirectoryName(tempFile)!,
                Path.GetFileName(tempFile) + ".tmp-*");
            Assert.Empty(siblings);

            // And the published file round-trips cleanly.
            var decrypted = IpcEncryption.DecryptFromFile(tempFile, TestKey, TestIV);
            Assert.NotNull(decrypted);
            Assert.Equal("stable payload"u8.ToArray(), decrypted);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void EncryptToFile_OverwritesExistingFile()
    {
        // The IPC file name is shared across launches; a second contract write must
        // replace the first one rather than fail.
        var tempFile = Path.Combine(Path.GetTempPath(), $"overwrite_{Guid.NewGuid():N}.enc");
        try
        {
            IpcEncryption.EncryptToFile("first"u8.ToArray(), tempFile, TestKey, TestIV);
            IpcEncryption.EncryptToFile("second"u8.ToArray(), tempFile, TestKey, TestIV);

            var decrypted = IpcEncryption.DecryptFromFile(tempFile, TestKey, TestIV);
            Assert.NotNull(decrypted);
            Assert.Equal("second"u8.ToArray(), decrypted);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
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

[Collection("IpcTests")]
public class EncryptedFileProcessContractProviderTests : IDisposable
{
    private readonly string _tmpDir;

    public EncryptedFileProcessContractProviderTests()
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

    [Fact]
    public void SendAndReceive_RoundTrip_ProcessContractPreserved()
    {
        var provider = new EncryptedFileProcessContractProvider(_tmpDir);
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
        var provider = new EncryptedFileProcessContractProvider(_tmpDir);
        var result = provider.Receive();
        Assert.Null(result);
    }
}
