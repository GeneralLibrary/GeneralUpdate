using GeneralUpdate.Core.Download.Pipeline;

namespace CoreTest.Download;

/// <summary>
/// AAAT unit tests for <see cref="DefaultDownloadPipeline"/> — SHA256 hash verification pipeline.
/// Covers: no-hash passthrough, matching hash, mismatched hash, null hash, empty hash, file not found, cancelled token, whitespace hash.
/// </summary>
public class DefaultDownloadPipelineTests
{
    private static string TempFile(string content = "test content")
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    #region No hash — passthrough

    [Fact]
    public async Task ProcessAsync_NullHash_ReturnsPath()
    {
        var pipeline = new DefaultDownloadPipeline(null);
        var filePath = TempFile();

        try
        {
            var result = await pipeline.ProcessAsync(filePath);
            Assert.Equal(filePath, result);
        }
        finally { TryDelete(filePath); }
    }

    [Fact]
    public async Task ProcessAsync_EmptyHash_ReturnsPath()
    {
        var pipeline = new DefaultDownloadPipeline(string.Empty);
        var filePath = TempFile();

        try
        {
            var result = await pipeline.ProcessAsync(filePath);
            Assert.Equal(filePath, result);
        }
        finally { TryDelete(filePath); }
    }

    [Fact]
    public async Task ProcessAsync_WhitespaceHash_TriggersVerification()
    {
        // Whitespace is NOT null or empty, so it triggers hash verification which will fail
        var pipeline = new DefaultDownloadPipeline("   ");
        var filePath = TempFile();

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => pipeline.ProcessAsync(filePath));
        }
        finally { TryDelete(filePath); }
    }

    [Fact]
    public async Task ProcessAsync_DefaultCtor_NoHash_ReturnsPath()
    {
        var pipeline = new DefaultDownloadPipeline();
        var filePath = TempFile();

        try
        {
            var result = await pipeline.ProcessAsync(filePath);
            Assert.Equal(filePath, result);
        }
        finally { TryDelete(filePath); }
    }

    #endregion

    #region Hash verification

    [Fact]
    public async Task ProcessAsync_MatchingHash_Succeeds()
    {
        var filePath = TempFile("Hello World");
        var expectedHash = ComputeSha256(filePath);

        var pipeline = new DefaultDownloadPipeline(expectedHash);

        try
        {
            var result = await pipeline.ProcessAsync(filePath);
            Assert.Equal(filePath, result);
        }
        finally { TryDelete(filePath); }
    }

    [Fact]
    public async Task ProcessAsync_MismatchedHash_ThrowsInvalidDataException()
    {
        var filePath = TempFile("Hello World");

        // Deliberately wrong hash
        var pipeline = new DefaultDownloadPipeline("0000000000000000000000000000000000000000000000000000000000000000");
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => pipeline.ProcessAsync(filePath));
        }
        finally { TryDelete(filePath); }
    }

    [Fact]
    public async Task ProcessAsync_HashCaseInsensitive_MatchSucceeds()
    {
        var filePath = TempFile("case test");
        var lowerHash = ComputeSha256(filePath);
        var upperHash = lowerHash.ToUpperInvariant();

        Assert.NotEqual(lowerHash, upperHash); // confirm case differs

        var pipeline = new DefaultDownloadPipeline(upperHash);

        try
        {
            var result = await pipeline.ProcessAsync(filePath);
            Assert.Equal(filePath, result);
        }
        finally { TryDelete(filePath); }
    }

    #endregion

    #region Error handling

    [Fact]
    public async Task ProcessAsync_FileNotFound_Throws()
    {
        var pipeline = new DefaultDownloadPipeline("abc");
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "nonexistent_" + Guid.NewGuid().ToString("N") + ".bin");
        await Assert.ThrowsAnyAsync<Exception>(() =>
            pipeline.ProcessAsync(nonExistentPath));
    }

    [Fact]
    public async Task ProcessAsync_CancelledTokenWithHash_ThrowsOperationCanceledException()
    {
        // When a hash IS provided, the pipeline actually performs async I/O (SHA256),
        // which can observe cancellation
        var filePath = TempFile("test content for cancellation");
        var expectedHash = ComputeSha256(filePath);
        var pipeline = new DefaultDownloadPipeline(expectedHash);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                pipeline.ProcessAsync(filePath, cts.Token));
        }
        finally { TryDelete(filePath); }
    }

    #endregion

    #region Empty file

    [Fact]
    public async Task ProcessAsync_EmptyFileHashMatch_Succeeds()
    {
        var filePath = TempFile(string.Empty);
        var expectedHash = ComputeSha256(filePath);

        var pipeline = new DefaultDownloadPipeline(expectedHash);

        try
        {
            var result = await pipeline.ProcessAsync(filePath);
            Assert.Equal(filePath, result);
        }
        finally { TryDelete(filePath); }
    }

    #endregion

    #region Helpers

    private static string ComputeSha256(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fs = File.OpenRead(path);
        var hash = sha.ComputeHash(fs);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    #endregion
}
