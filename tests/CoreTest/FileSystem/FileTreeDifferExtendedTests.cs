namespace CoreTest.FileSystem;

using GeneralUpdate.Core.FileSystem;

/// <summary>
/// AAAT unit tests for <see cref="FileTreeDiffer"/> — exhaustive branch coverage.
/// Covers: ProduceDeltaPaths (added, modified, deleted, non-existent, mixed), ProduceDeletes,
/// ShouldUseDeltaPatching (zero total, threshold boundary, exact threshold, large diff, empty diff).
/// </summary>
public class FileTreeDifferExtendedTests
{
    private static FileEntry Entry(string path, long size = 100)
        => new(path, size, DateTime.UtcNow);

    private static FileTreeDiff Diff(
        FileEntry[]? added = null,
        FileEntry[]? modified = null,
        string[]? deleted = null)
        => new(
            added ?? Array.Empty<FileEntry>(),
            modified ?? Array.Empty<FileEntry>(),
            deleted ?? Array.Empty<string>());

    #region ProduceDeltaPaths — Added Files

    [Fact]
    public void ProduceDeltaPaths_AddedFileExists_ReturnsIt()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            var fileName = Path.GetFileName(filePath);
            var root = Path.GetDirectoryName(filePath)!;
            var diff = Diff(added: new[] { Entry(fileName) });

            var paths = FileTreeDiffer.ProduceDeltaPaths(diff, root);

            Assert.Single(paths);
            Assert.Equal(fileName, paths[0].RelativePath);
        }
        finally { TryDelete(filePath); }
    }

    [Fact]
    public void ProduceDeltaPaths_AddedFileDoesNotExist_Skipped()
    {
        var diff = Diff(added: new[] { Entry("nonexistent_added.txt") });

        var paths = FileTreeDiffer.ProduceDeltaPaths(diff, Path.GetTempPath());

        Assert.Empty(paths);
    }

    [Fact]
    public void ProduceDeltaPaths_MultipleAddedFiles_ExistingOnesReturned()
    {
        var file1 = Path.GetTempFileName();
        var file2 = Path.GetTempFileName();
        try
        {
            var root = Path.GetDirectoryName(file1)!;
            var diff = Diff(added: new[]
            {
                Entry(Path.GetFileName(file1)),
                Entry(Path.GetFileName(file2)),
                Entry("missing_added.txt")
            });

            var paths = FileTreeDiffer.ProduceDeltaPaths(diff, root);

            Assert.Equal(2, paths.Count);
        }
        finally { TryDelete(file1); TryDelete(file2); }
    }

    #endregion

    #region ProduceDeltaPaths — Modified Files

    [Fact]
    public void ProduceDeltaPaths_ModifiedFileExists_ReturnsIt()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            var fileName = Path.GetFileName(filePath);
            var root = Path.GetDirectoryName(filePath)!;
            var diff = Diff(modified: new[] { Entry(fileName, 200) });

            var paths = FileTreeDiffer.ProduceDeltaPaths(diff, root);

            Assert.Single(paths);
            Assert.Equal(fileName, paths[0].RelativePath);
        }
        finally { TryDelete(filePath); }
    }

    [Fact]
    public void ProduceDeltaPaths_ModifiedFileDoesNotExist_Skipped()
    {
        var diff = Diff(modified: new[] { Entry("nonexistent_modified.txt") });

        var paths = FileTreeDiffer.ProduceDeltaPaths(diff, Path.GetTempPath());

        Assert.Empty(paths);
    }

    #endregion

    #region ProduceDeltaPaths — Mixed (Added + Modified + Deleted)

    [Fact]
    public void ProduceDeltaPaths_MixedChanged_FiltersDeleted()
    {
        var file1 = Path.GetTempFileName();
        try
        {
            var root = Path.GetDirectoryName(file1)!;
            var diff = Diff(
                added: new[] { Entry(Path.GetFileName(file1)) },
                modified: new[] { Entry("missing_mod.txt") },
                deleted: new[] { "deleted_file.txt" }
            );

            var paths = FileTreeDiffer.ProduceDeltaPaths(diff, root);

            // Only the existing added file — missing modified + deleted excluded
            Assert.Single(paths);
            Assert.Equal(Path.GetFileName(file1), paths[0].RelativePath);
        }
        finally { TryDelete(file1); }
    }

    #endregion

    #region ProduceDeltaPaths — Empty diff

    [Fact]
    public void ProduceDeltaPaths_AllEmptyArrays_ReturnsEmpty()
    {
        var diff = Diff();

        var paths = FileTreeDiffer.ProduceDeltaPaths(diff, Path.GetTempPath());

        Assert.Empty(paths);
    }

    [Fact]
    public void ProduceDeltaPaths_OnlyDeletes_ReturnsEmpty()
    {
        var diff = Diff(deleted: new[] { "a.txt", "b.txt" });

        var paths = FileTreeDiffer.ProduceDeltaPaths(diff, Path.GetTempPath());

        Assert.Empty(paths);
    }

    #endregion

    #region ProduceDeletes

    [Fact]
    public void ProduceDeletes_WithDeletes_ReturnsThem()
    {
        var diff = Diff(deleted: new[] { "remove/a.txt", "remove/b.dll" });

        var deletes = FileTreeDiffer.ProduceDeletes(diff);

        Assert.Equal(2, deletes.Count);
        Assert.Contains("remove/a.txt", deletes);
        Assert.Contains("remove/b.dll", deletes);
    }

    [Fact]
    public void ProduceDeletes_EmptyDeletes_ReturnsEmpty()
    {
        var diff = Diff();

        var deletes = FileTreeDiffer.ProduceDeletes(diff);

        Assert.Empty(deletes);
    }

    [Fact]
    public void ProduceDeletes_ReturnsDeletedItems()
    {
        var deleted = new[] { "x.txt" };
        var diff = Diff(deleted: deleted);

        var result = FileTreeDiffer.ProduceDeletes(diff);

        Assert.Single(result);
        Assert.Equal("x.txt", result[0]);
    }

    #endregion

    #region ShouldUseDeltaPatching

    [Fact]
    public void ShouldUseDeltaPatching_ZeroTotalFiles_ReturnsFalse()
    {
        var diff = Diff(added: new[] { Entry("a.txt") });
        Assert.False(FileTreeDiffer.ShouldUseDeltaPatching(diff, 0));
    }

    [Fact]
    public void ShouldUseDeltaPatching_BelowThreshold_ReturnsTrue()
    {
        var diff = Diff(added: new[] { Entry("a.txt") }); // 1 change

        Assert.True(FileTreeDiffer.ShouldUseDeltaPatching(diff, totalFileCount: 100));
    }

    [Fact]
    public void ShouldUseDeltaPatching_ExactlyAtDefaultThreshold_ReturnsTrue()
    {
        // 50% threshold: 50 changes / 100 files = 50% <= 50% => true
        var added = Enumerable.Range(0, 50).Select(i => Entry($"file_{i}.txt")).ToArray();
        var diff = Diff(added: added);

        Assert.True(FileTreeDiffer.ShouldUseDeltaPatching(diff, 100));
    }

    [Fact]
    public void ShouldUseDeltaPatching_JustAboveDefaultThreshold_ReturnsFalse()
    {
        // 51/100 > 50% => false
        var added = Enumerable.Range(0, 51).Select(i => Entry($"file_{i}.txt")).ToArray();
        var diff = Diff(added: added);

        Assert.False(FileTreeDiffer.ShouldUseDeltaPatching(diff, 100));
    }

    [Fact]
    public void ShouldUseDeltaPatching_CustomThreshold_Below_ReturnsTrue()
    {
        var diff = Diff(added: new[] { Entry("a.txt") }); // 1/10 = 10%

        Assert.True(FileTreeDiffer.ShouldUseDeltaPatching(diff, 10, 0.3));
    }

    [Fact]
    public void ShouldUseDeltaPatching_CustomThreshold_Above_ReturnsFalse()
    {
        // 4/10 = 40% > 30%
        var added = Enumerable.Range(0, 4).Select(i => Entry($"f{i}.txt")).ToArray();
        var diff = Diff(added: added);

        Assert.False(FileTreeDiffer.ShouldUseDeltaPatching(diff, 10, 0.3));
    }

    [Fact]
    public void ShouldUseDeltaPatching_AllFilesChanged_ReturnsFalse()
    {
        // 100/100 = 100%
        var added = Enumerable.Range(0, 100).Select(i => Entry($"f{i}.txt")).ToArray();
        var diff = Diff(added: added);

        Assert.False(FileTreeDiffer.ShouldUseDeltaPatching(diff, 100));
    }

    [Fact]
    public void ShouldUseDeltaPatching_ZeroChanges_ReturnsTrue()
    {
        var diff = Diff();
        Assert.True(FileTreeDiffer.ShouldUseDeltaPatching(diff, 100));
    }

    [Fact]
    public void ShouldUseDeltaPatching_ZeroChangesZeroFiles_ReturnsFalse()
    {
        var diff = Diff();
        Assert.False(FileTreeDiffer.ShouldUseDeltaPatching(diff, 0));
    }

    [Fact]
    public void ShouldUseDeltaPatching_ThresholdZero_Below_ReturnsFalse()
    {
        // 0% threshold — any change should return false
        var diff = Diff(added: new[] { Entry("a.txt") });
        Assert.False(FileTreeDiffer.ShouldUseDeltaPatching(diff, 100, 0.0));
    }

    [Fact]
    public void ShouldUseDeltaPatching_ThresholdOne_AlwaysTrue()
    {
        var added = Enumerable.Range(0, 999).Select(i => Entry($"f{i}.txt")).ToArray();
        var diff = Diff(added: added);

        Assert.True(FileTreeDiffer.ShouldUseDeltaPatching(diff, 1000, 1.0));
    }

    #endregion

    #region Helpers

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    #endregion
}
