namespace CoreTest.FileSystem;

using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.FileSystem;

/// <summary>
/// AAAT unit tests for <see cref="FileTreeSnapshot"/> — additional edge case coverage.
/// Covers: null args, empty entries, root path normalization, CreatedAt timestamp, FromEnumerator with empty dir.
/// </summary>
public class FileTreeSnapshotExtendedTests
{
    #region Constructor edge cases

    [Fact]
    public void Ctor_NullRootPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FileTreeSnapshot(null!, Array.Empty<FileEntry>()));
    }

    [Fact]
    public void Ctor_NullEntries_TreatedAsEmpty()
    {
        var snapshot = new FileTreeSnapshot("/root", null!);

        Assert.NotNull(snapshot.Entries);
        Assert.Empty(snapshot.Entries);
    }

    [Fact]
    public void Ctor_EmptyEntries_Works()
    {
        var snapshot = new FileTreeSnapshot("/root", Array.Empty<FileEntry>());

        Assert.Empty(snapshot.Entries);
        Assert.Equal("/root", snapshot.RootPath);
    }

    [Fact]
    public void Ctor_RootPathWithTrailingSlash_Preserved()
    {
        var root = "C:\\test\\";
        var snapshot = new FileTreeSnapshot(root, Array.Empty<FileEntry>());

        Assert.Equal(root, snapshot.RootPath);
    }

    [Fact]
    public void CreatedAt_IsAroundNow()
    {
        var before = DateTime.UtcNow;
        var snapshot = new FileTreeSnapshot("/root", Array.Empty<FileEntry>());
        var after = DateTime.UtcNow;

        Assert.True(snapshot.CreatedAt >= before && snapshot.CreatedAt <= after);
    }

    #endregion

    #region Empty static factory

    [Fact]
    public void Empty_ReturnsSnapshotWithEmptyEntries()
    {
        var snapshot = FileTreeSnapshot.Empty("/some/root");

        Assert.Empty(snapshot.Entries);
        Assert.Equal("/some/root", snapshot.RootPath);
    }

    #endregion

    #region FromEnumerator

    [Fact]
    public void FromEnumerator_EmptyDirectory_ReturnsEmptyEntries()
    {
        var safeDir = "GenUpdSnapEmpty_" + Path.GetRandomFileName();
        var rootPath = Path.Combine(Path.GetTempPath(), safeDir);
        Directory.CreateDirectory(rootPath);
        try
        {
            var config = BlackListConfig.Empty;
            var enumerator = FileTreeEnumerator.FromConfig(config);
            var snapshot = FileTreeSnapshot.FromEnumerator(rootPath, enumerator);

            Assert.Equal(rootPath, snapshot.RootPath);
            Assert.NotNull(snapshot.Entries);
            Assert.Empty(snapshot.Entries);
        }
        finally
        {
            try { Directory.Delete(rootPath, false); } catch { }
        }
    }

    [Fact]
    public void FromEnumerator_MultipleFiles_ReturnsAll()
    {
        var safeDir = "GenUpdSnapMulti_" + Path.GetRandomFileName();
        var rootPath = Path.Combine(Path.GetTempPath(), safeDir);
        Directory.CreateDirectory(rootPath);
        try
        {
            File.WriteAllText(Path.Combine(rootPath, "a.txt"), "a");
            File.WriteAllText(Path.Combine(rootPath, "b.txt"), "bb");
            File.WriteAllText(Path.Combine(rootPath, "c.txt"), "ccc");

            var config = BlackListConfig.Empty;
            var enumerator = FileTreeEnumerator.FromConfig(config);
            var snapshot = FileTreeSnapshot.FromEnumerator(rootPath, enumerator);

            Assert.Equal(3, snapshot.Entries.Count);
            Assert.All(snapshot.Entries, e =>
            {
                Assert.StartsWith(rootPath, Path.Combine(rootPath, e.RelativePath));
                Assert.True(e.Size > 0);
                Assert.True(e.LastWriteTimeUtc <= DateTime.UtcNow);
            });
        }
        finally
        {
            try
            {
                foreach (var f in Directory.GetFiles(rootPath))
                {
                    File.SetAttributes(f, FileAttributes.Normal);
                    File.Delete(f);
                }
                Directory.Delete(rootPath, false);
            }
            catch { }
        }
    }

    [Fact]
    public void FromEnumerator_Subdirectories_EnumeratedRecursively()
    {
        var safeDir = "GenUpdSnapSub_" + Path.GetRandomFileName();
        var rootPath = Path.Combine(Path.GetTempPath(), safeDir);
        Directory.CreateDirectory(rootPath);
        var subDir = Path.Combine(rootPath, "sub");
        Directory.CreateDirectory(subDir);
        try
        {
            File.WriteAllText(Path.Combine(rootPath, "root.txt"), "r");
            File.WriteAllText(Path.Combine(subDir, "sub.txt"), "s");

            var config = BlackListConfig.Empty;
            var enumerator = FileTreeEnumerator.FromConfig(config);
            var snapshot = FileTreeSnapshot.FromEnumerator(rootPath, enumerator);

            Assert.Equal(2, snapshot.Entries.Count);

            // Verify relative paths use separator
            var relativePaths = snapshot.Entries.Select(e => e.RelativePath).ToArray();
            Assert.Contains(relativePaths, p => p.Contains("root.txt"));
            Assert.Contains(relativePaths, p => p.Contains("sub") && p.Contains("sub.txt"));
        }
        finally
        {
            try
            {
                foreach (var f in Directory.GetFiles(subDir))
                {
                    File.SetAttributes(f, FileAttributes.Normal);
                    File.Delete(f);
                }
                Directory.Delete(subDir, false);
                foreach (var f in Directory.GetFiles(rootPath))
                {
                    File.SetAttributes(f, FileAttributes.Normal);
                    File.Delete(f);
                }
                Directory.Delete(rootPath, false);
            }
            catch { }
        }
    }

    #endregion

    #region FileEntry struct

    [Fact]
    public void FileEntry_ValueEquality_SamePropsEqual()
    {
        var time = DateTime.UtcNow;
        var a = new FileEntry("path/file.txt", 100, time);
        var b = new FileEntry("path/file.txt", 100, time);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void FileEntry_ValueEquality_DifferentSizeNotEqual()
    {
        var time = DateTime.UtcNow;
        var a = new FileEntry("f.txt", 100, time);
        var b = new FileEntry("f.txt", 200, time);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void FileEntry_ToString_ContainsPath()
    {
        var entry = new FileEntry("sub/myfile.dll", 2048, DateTime.UtcNow);
        var str = entry.ToString();

        Assert.Contains("sub/myfile.dll", str);
    }

    #endregion
}
