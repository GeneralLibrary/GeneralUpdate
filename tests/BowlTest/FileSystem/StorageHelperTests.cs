using System.Text.Json;
using GeneralUpdate.Bowl.FileSystem;

namespace BowlTest.FileSystem;

/// <summary>
/// Unit tests for <see cref="StorageHelper"/> following AAAT pattern.
/// Tests directory operations, JSON creation, and edge cases.
/// </summary>
public class StorageHelperTests : IDisposable
{
    private readonly string _testBasePath;

    public StorageHelperTests()
    {
        _testBasePath = Path.Combine(Path.GetTempPath(), $"StorageHelperTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testBasePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testBasePath))
        {
            try { Directory.Delete(_testBasePath, recursive: true); }
            catch { /* cleanup failure is non-fatal */ }
        }
    }

    #region CreateJson

    [Fact]
    public void CreateJson_WritesValidJsonFile()
    {
        var filePath = Path.Combine(_testBasePath, "test.json");
        var obj = new { Name = "Test", Value = 42 };

        StorageHelper.CreateJson(filePath, obj);

        Assert.True(System.IO.File.Exists(filePath));
        var content = System.IO.File.ReadAllText(filePath);
        Assert.Contains("Test", content);
        Assert.Contains("42", content);
    }

    [Fact]
    public void CreateJson_CreatesDirectoryIfMissing()
    {
        var subDir = Path.Combine(_testBasePath, "nested", "deep");
        var filePath = Path.Combine(subDir, "data.json");
        var obj = new { Key = "value" };

        StorageHelper.CreateJson(filePath, obj);

        Assert.True(System.IO.File.Exists(filePath));
    }

    [Fact]
    public void CreateJson_ProducesDeserializableJson()
    {
        var filePath = Path.Combine(_testBasePath, "roundtrip.json");
        var original = new TestModel { Id = 1, Name = "roundtrip" };

        StorageHelper.CreateJson(filePath, original);

        var json = System.IO.File.ReadAllText(filePath);
        var deserialized = JsonSerializer.Deserialize<TestModel>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Name, deserialized.Name);
    }

    #endregion

    #region Restore

    [Fact]
    public void Restore_CopiesFilesFromBackupToSource()
    {
        var backupPath = Path.Combine(_testBasePath, "backup");
        var sourcePath = Path.Combine(_testBasePath, "source");
        Directory.CreateDirectory(backupPath);
        System.IO.File.WriteAllText(Path.Combine(backupPath, "file1.txt"), "content1");
        System.IO.File.WriteAllText(Path.Combine(backupPath, "file2.txt"), "content2");

        StorageHelper.Restore(backupPath, sourcePath);

        Assert.True(System.IO.File.Exists(Path.Combine(sourcePath, "file1.txt")));
        Assert.True(System.IO.File.Exists(Path.Combine(sourcePath, "file2.txt")));
        Assert.Equal("content1", System.IO.File.ReadAllText(Path.Combine(sourcePath, "file1.txt")));
    }

    [Fact]
    public void Restore_CreatesSourceDirectoryIfMissing()
    {
        var backupPath = Path.Combine(_testBasePath, "backup2");
        var sourcePath = Path.Combine(_testBasePath, "new_source");
        Directory.CreateDirectory(backupPath);
        System.IO.File.WriteAllText(Path.Combine(backupPath, "readme.txt"), "backup content");

        StorageHelper.Restore(backupPath, sourcePath);

        Assert.True(Directory.Exists(sourcePath));
        Assert.True(System.IO.File.Exists(Path.Combine(sourcePath, "readme.txt")));
    }

    [Fact]
    public void Restore_CopiesNestedDirectories()
    {
        var backupPath = Path.Combine(_testBasePath, "nested_backup");
        var sourcePath = Path.Combine(_testBasePath, "nested_source");
        var nestedDir = Path.Combine(backupPath, "subdir");
        Directory.CreateDirectory(nestedDir);
        System.IO.File.WriteAllText(Path.Combine(nestedDir, "nested.txt"), "nested content");
        System.IO.File.WriteAllText(Path.Combine(backupPath, "root.txt"), "root content");

        StorageHelper.Restore(backupPath, sourcePath);

        Assert.True(System.IO.File.Exists(Path.Combine(sourcePath, "root.txt")));
        Assert.True(System.IO.File.Exists(Path.Combine(sourcePath, "subdir", "nested.txt")));
        Assert.Equal("nested content",
            System.IO.File.ReadAllText(Path.Combine(sourcePath, "subdir", "nested.txt")));
    }

    #endregion

    #region DeleteDirectory

    [Fact]
    public void DeleteDirectory_RemovesDirectoryAndContents()
    {
        var targetDir = Path.Combine(_testBasePath, "to_delete");
        Directory.CreateDirectory(targetDir);
        System.IO.File.WriteAllText(Path.Combine(targetDir, "temp.txt"), "temp");

        StorageHelper.DeleteDirectory(targetDir);

        Assert.False(Directory.Exists(targetDir));
    }

    [Fact]
    public void DeleteDirectory_RemovesNestedDirectories()
    {
        var targetDir = Path.Combine(_testBasePath, "nested_delete");
        var nestedDir = Path.Combine(targetDir, "inner");
        Directory.CreateDirectory(nestedDir);
        System.IO.File.WriteAllText(Path.Combine(nestedDir, "file.txt"), "data");

        StorageHelper.DeleteDirectory(targetDir);

        Assert.False(Directory.Exists(targetDir));
    }

    [Fact]
    public void DeleteDirectory_RemovesReadOnlyFiles()
    {
        var targetDir = Path.Combine(_testBasePath, "readonly_delete");
        Directory.CreateDirectory(targetDir);
        var filePath = Path.Combine(targetDir, "readonly.txt");
        System.IO.File.WriteAllText(filePath, "readonly");
        System.IO.File.SetAttributes(filePath, FileAttributes.ReadOnly);

        // Should not throw
        StorageHelper.DeleteDirectory(targetDir);

        Assert.False(Directory.Exists(targetDir));
    }

    #endregion

    #region Helper model

    private sealed class TestModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    #endregion
}
