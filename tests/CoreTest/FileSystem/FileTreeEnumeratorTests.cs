using System.IO;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.FileSystem;
using Xunit;

namespace CoreTest.FileSystem;

public class FileTreeEnumeratorTests
{
    [Fact]
    public void EnumerateFiles_ReturnsAllFilesInFlatDirectory()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ft_test_{System.Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tmpDir);
            File.WriteAllText(Path.Combine(tmpDir, "a.txt"), "a");
            File.WriteAllText(Path.Combine(tmpDir, "b.dll"), "b");

            var enumerator = FileTreeEnumerator.FromConfig(BlackListConfig.Empty);
            var files = enumerator.EnumerateFiles(tmpDir).ToList();

            Assert.Equal(2, files.Count);
        }
        finally { Directory.Delete(tmpDir, true); }
    }

    [Fact]
    public void EnumerateFiles_BlacklistedFormat_Excluded()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ft_test_{System.Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tmpDir);
            File.WriteAllText(Path.Combine(tmpDir, "app.exe"), "a");
            File.WriteAllText(Path.Combine(tmpDir, "data.pdb"), "b");
            File.WriteAllText(Path.Combine(tmpDir, "config.xml"), "c");

            var config = new BlackListConfig(BlackFormats: new[] { ".pdb", ".xml" });
            var enumerator = FileTreeEnumerator.FromConfig(config);
            var files = enumerator.EnumerateFiles(tmpDir).ToList();

            Assert.Single(files);
            Assert.EndsWith(".exe", files[0]);
        }
        finally { Directory.Delete(tmpDir, true); }
    }

    [Fact]
    public void EnumerateFiles_BlacklistedDirectory_Skipped()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"ft_test_{System.Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tmpDir);
            var logDir = Path.Combine(tmpDir, "logs");
            Directory.CreateDirectory(logDir);
            File.WriteAllText(Path.Combine(tmpDir, "main.exe"), "a");
            File.WriteAllText(Path.Combine(logDir, "app.log"), "b");

            var config = new BlackListConfig(SkipDirectorys: new[] { "logs" });
            var enumerator = FileTreeEnumerator.FromConfig(config);
            var files = enumerator.EnumerateFiles(tmpDir).ToList();

            Assert.Single(files);
            Assert.EndsWith("main.exe", files[0]);
        }
        finally { Directory.Delete(tmpDir, true); }
    }

    [Fact]
    public void EnumerateFiles_NonExistentDirectory_ReturnsEmpty()
    {
        var enumerator = FileTreeEnumerator.FromConfig(BlackListConfig.Empty);
        var files = enumerator.EnumerateFiles("C:\\does\\not\\exist").ToList();
        Assert.Empty(files);
    }
}
