using GeneralUpdate.Core.Compress;

namespace CoreTest.Compress;

public class CompressProviderTests
{
    [Fact]
    public void Compress_ZipFormat_UsesZipStrategy()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"compress_test_{Guid.NewGuid():N}");
        var destZip = Path.Combine(Path.GetTempPath(), $"result_{Guid.NewGuid():N}.zip");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.txt"), "hello");
        try
        {
            var ex = Record.Exception(() =>
                CompressProvider.Compress(".zip", tempDir, destZip, false, System.Text.Encoding.UTF8));
            Assert.Null(ex);
            Assert.True(File.Exists(destZip));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (File.Exists(destZip)) File.Delete(destZip);
        }
    }

    [Fact]
    public void Compress_UnknownFormat_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CompressProvider.Compress("RAR", "source", "dest", false, System.Text.Encoding.UTF8));
    }

    [Fact]
    public void Decompress_ZipFormat_UsesZipStrategy()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"decompress_src_{Guid.NewGuid():N}");
        var zipPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.zip");
        var destDir = Path.Combine(Path.GetTempPath(), $"decompress_dst_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.txt"), "hello world");
        try
        {
            System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, zipPath);
            var ex = Record.Exception(() =>
                CompressProvider.Decompress(".zip", zipPath, destDir, System.Text.Encoding.UTF8));
            Assert.Null(ex);
            Assert.True(File.Exists(Path.Combine(destDir, "test.txt")));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    [Fact]
    public void Decompress_UnknownFormat_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CompressProvider.Decompress("7z", "source", "dest", System.Text.Encoding.UTF8));
    }
}
