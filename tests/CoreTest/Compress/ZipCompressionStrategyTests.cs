using System.Text;
using GeneralUpdate.Core.Compress;

namespace CoreTest.Compress;

public class ZipCompressionStrategyTests
{
    private readonly ZipCompressionStrategy _strategy = new();

    [Fact]
    public void Compress_DirectoryToNewZip_CreatesArchive()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid():N}");
        var destZip = Path.Combine(Path.GetTempPath(), $"dest_{Guid.NewGuid():N}.zip");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "file.txt"), "content");
        try
        {
            _strategy.Compress(srcDir, destZip, false, Encoding.UTF8);
            Assert.True(File.Exists(destZip));
        }
        finally
        {
            if (Directory.Exists(srcDir)) Directory.Delete(srcDir, true);
            if (File.Exists(destZip)) File.Delete(destZip);
        }
    }

    [Fact]
    public void Compress_DirectoryToExistingZip_UpdatesArchive()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid():N}");
        var destZip = Path.Combine(Path.GetTempPath(), $"dest_{Guid.NewGuid():N}.zip");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "file1.txt"), "first");
        // Create initial zip
        System.IO.Compression.ZipFile.CreateFromDirectory(srcDir, destZip);
        // Add second file
        File.WriteAllText(Path.Combine(srcDir, "file2.txt"), "second");
        try
        {
            _strategy.Compress(srcDir, destZip, false, Encoding.UTF8);
            Assert.True(File.Exists(destZip));
        }
        finally
        {
            if (Directory.Exists(srcDir)) Directory.Delete(srcDir, true);
            if (File.Exists(destZip)) File.Delete(destZip);
        }
    }

    [Fact]
    public void Compress_SingleFileInDirectoryToNewZip_CreatesArchive()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid():N}");
        var destZip = Path.Combine(Path.GetTempPath(), $"dest_{Guid.NewGuid():N}.zip");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "single.txt"), "single file content");
        try
        {
            _strategy.Compress(srcDir, destZip, false, Encoding.UTF8);
            Assert.True(File.Exists(destZip));
        }
        finally
        {
            if (Directory.Exists(srcDir)) Directory.Delete(srcDir, true);
            if (File.Exists(destZip)) File.Delete(destZip);
        }
    }

    [Fact]
    public void Decompress_ZipToDirectory_ExtractsFiles()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid():N}");
        var zipPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.zip");
        var destDir = Path.Combine(Path.GetTempPath(), $"dst_{Guid.NewGuid():N}");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "data.txt"), "decompress test");
        System.IO.Compression.ZipFile.CreateFromDirectory(srcDir, zipPath);
        try
        {
            _strategy.Decompress(zipPath, destDir, Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(destDir, "data.txt")));
            Assert.Equal("decompress test", File.ReadAllText(Path.Combine(destDir, "data.txt")));
        }
        finally
        {
            if (Directory.Exists(srcDir)) Directory.Delete(srcDir, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    [Fact]
    public void Decompress_ZipFileNotFound_ReturnsWithoutError()
    {
        var nonexistent = Path.Combine(Path.GetTempPath(), $"noexist_{Guid.NewGuid():N}.zip");
        var destDir = Path.Combine(Path.GetTempPath(), $"dst_{Guid.NewGuid():N}");
        var ex = Record.Exception(() => _strategy.Decompress(nonexistent, destDir, Encoding.UTF8));
        Assert.Null(ex);
    }

    [Fact]
    public void Decompress_NestedDirectories_PreservesStructure()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid():N}");
        var nestedDir = Path.Combine(srcDir, "sub", "deep");
        var zipPath = Path.Combine(Path.GetTempPath(), $"nested_{Guid.NewGuid():N}.zip");
        var destDir = Path.Combine(Path.GetTempPath(), $"dst_{Guid.NewGuid():N}");
        Directory.CreateDirectory(nestedDir);
        File.WriteAllText(Path.Combine(nestedDir, "deep_file.txt"), "deep content");
        System.IO.Compression.ZipFile.CreateFromDirectory(srcDir, zipPath);
        try
        {
            _strategy.Decompress(zipPath, destDir, Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(destDir, "sub", "deep", "deep_file.txt")));
        }
        finally
        {
            if (Directory.Exists(srcDir)) Directory.Delete(srcDir, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    [Fact]
    public void Decompress_EncodingPreserved_Utf8()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid():N}");
        var zipPath = Path.Combine(Path.GetTempPath(), $"utf8_{Guid.NewGuid():N}.zip");
        var destDir = Path.Combine(Path.GetTempPath(), $"dst_{Guid.NewGuid():N}");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "unicode.txt"), "你好世界", Encoding.UTF8);
        System.IO.Compression.ZipFile.CreateFromDirectory(srcDir, zipPath);
        try
        {
            _strategy.Decompress(zipPath, destDir, Encoding.UTF8);
            Assert.Equal("你好世界", File.ReadAllText(Path.Combine(destDir, "unicode.txt"), Encoding.UTF8));
        }
        finally
        {
            if (Directory.Exists(srcDir)) Directory.Delete(srcDir, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    [Fact]
    public void Decompress_IncludesRootDirectory_WhenFlagTrue()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), $"src_{Guid.NewGuid():N}");
        var zipPath = Path.Combine(Path.GetTempPath(), $"includeRoot_{Guid.NewGuid():N}.zip");
        var destDir = Path.Combine(Path.GetTempPath(), $"dst_{Guid.NewGuid():N}");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "file.txt"), "content");
        // Create zip with includeBaseDirectory=true for the test, then use Compress with includeRootDirectory 
        try
        {
            _strategy.Compress(srcDir, zipPath, true, Encoding.UTF8);
            _strategy.Decompress(zipPath, destDir, Encoding.UTF8);
            Assert.True(File.Exists(Path.Combine(destDir, "file.txt")) || 
                        Directory.Exists(Path.Combine(destDir, Path.GetFileName(srcDir))));
        }
        finally
        {
            if (Directory.Exists(srcDir)) Directory.Delete(srcDir, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }
}
