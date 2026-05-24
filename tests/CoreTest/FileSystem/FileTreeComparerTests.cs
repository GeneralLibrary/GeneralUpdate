using System;
using System.Collections.Generic;
using System.IO;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.FileSystem;
using Xunit;

namespace CoreTest.FileSystem;

public class FileTreeComparerTests
{
    [Fact]
    public void Compare_TwoIdenticalSnapshots_ReturnsEmptyDiff()
    {
        var entries = new[] { new FileEntry("a.txt", 100, DateTime.UtcNow) };
        var old = new FileTreeSnapshot("/root", entries);
        var updated = new FileTreeSnapshot("/root", entries);

        var diff = FileTreeComparer.Compare(old, updated);

        Assert.False(diff.HasChanges);
        Assert.Equal(0, diff.TotalChanges);
    }

    [Fact]
    public void Compare_NewFile_DetectsAddition()
    {
        var old = new FileTreeSnapshot("/root", Array.Empty<FileEntry>());
        var entry = new FileEntry("new.txt", 50, DateTime.UtcNow);
        var updated = new FileTreeSnapshot("/root", new[] { entry });

        var diff = FileTreeComparer.Compare(old, updated);

        Assert.True(diff.HasChanges);
        Assert.Single(diff.Added);
        Assert.Equal("new.txt", diff.Added[0].RelativePath);
        Assert.Empty(diff.Modified);
        Assert.Empty(diff.Deleted);
    }

    [Fact]
    public void Compare_DeletedFile_DetectsDeletion()
    {
        var entry = new FileEntry("old.txt", 50, DateTime.UtcNow);
        var old = new FileTreeSnapshot("/root", new[] { entry });
        var updated = new FileTreeSnapshot("/root", Array.Empty<FileEntry>());

        var diff = FileTreeComparer.Compare(old, updated);

        Assert.True(diff.HasChanges);
        Assert.Empty(diff.Added);
        Assert.Empty(diff.Modified);
        Assert.Single(diff.Deleted);
        Assert.Equal("old.txt", diff.Deleted[0]);
    }

    [Fact]
    public void Compare_ModifiedFile_DetectsModification()
    {
        var oldEntry = new FileEntry("mod.txt", 100, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var newEntry = new FileEntry("mod.txt", 200, new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        var old = new FileTreeSnapshot("/root", new[] { oldEntry });
        var updated = new FileTreeSnapshot("/root", new[] { newEntry });

        var diff = FileTreeComparer.Compare(old, updated);

        Assert.True(diff.HasChanges);
        Assert.Empty(diff.Added);
        Assert.Single(diff.Modified);
        Assert.Equal("mod.txt", diff.Modified[0].RelativePath);
        Assert.Equal(200, diff.Modified[0].Size);
        Assert.Empty(diff.Deleted);
    }

    [Fact]
    public void Compare_MixedChanges_AllDetected()
    {
        var now = DateTime.UtcNow;
        var old = new FileTreeSnapshot("/root", new[]
        {
            new FileEntry("keep.txt", 100, now),
            new FileEntry("remove.txt", 200, now),
            new FileEntry("change.txt", 50, now.AddDays(-1)),
        });
        var updated = new FileTreeSnapshot("/root", new[]
        {
            new FileEntry("keep.txt", 100, now),
            new FileEntry("change.txt", 75, now),
            new FileEntry("create.txt", 150, now),
        });

        var diff = FileTreeComparer.Compare(old, updated);

        Assert.True(diff.HasChanges);
        Assert.Single(diff.Added);
        Assert.Equal("create.txt", diff.Added[0].RelativePath);
        Assert.Single(diff.Modified);
        Assert.Equal("change.txt", diff.Modified[0].RelativePath);
        Assert.Single(diff.Deleted);
        Assert.Equal("remove.txt", diff.Deleted[0]);
    }

    [Fact]
    public void HasChanges_QuickCheck_ShortCircuits()
    {
        var now = DateTime.UtcNow;
        var old = new FileTreeSnapshot("/root", new[] { new FileEntry("a.txt", 100, now) });
        var updated = new FileTreeSnapshot("/root", new[] { new FileEntry("b.txt", 100, now) });

        bool changed = FileTreeComparer.HasChanges(old, updated);
        Assert.True(changed);

        var same = FileTreeComparer.HasChanges(old, old);
        Assert.False(same);
    }

    [Fact]
    public void FileTreeDiffer_ShouldUseDelta_SmallChange()
    {
        var now = DateTime.UtcNow;
        var diff = new FileTreeDiff(
            new[] { new FileEntry("a.txt", 100, now) },
            Array.Empty<FileEntry>(),
            Array.Empty<string>()
        );

        bool useDelta = FileTreeDiffer.ShouldUseDeltaPatching(diff, totalFileCount: 100);
        Assert.True(useDelta); // 1/100 = 1% < 50%
    }

    [Fact]
    public void FileTreeDiffer_ShouldUseFull_WhenLargeChange()
    {
        var added = new List<FileEntry>();
        for (int i = 0; i < 60; i++)
            added.Add(new FileEntry($"file_{i}.txt", 100, DateTime.UtcNow));

        var diff = new FileTreeDiff(added.AsReadOnly(), Array.Empty<FileEntry>(), Array.Empty<string>());

        bool useDelta = FileTreeDiffer.ShouldUseDeltaPatching(diff, totalFileCount: 100);
        Assert.False(useDelta); // 60/100 = 60% > 50%
    }

    [Fact]
    public void FileTreeDiffer_ProduceDeltaPaths()
    {
        var now = DateTime.UtcNow;
        var diff = new FileTreeDiff(
            new[] { new FileEntry("new.txt", 100, now) },
            new[] { new FileEntry("mod.txt", 200, now) },
            new[] { "del.txt" }
        );

        // ProduceDeltaPaths only returns files that exist on disk.
        // Non-existent paths are skipped — this tests the logic, not disk state.
        var paths = FileTreeDiffer.ProduceDeltaPaths(diff, "/nonexistent-root");
        Assert.Empty(paths); // files don't exist on disk

        var deletes = FileTreeDiffer.ProduceDeletes(diff);
        Assert.Single(deletes);
        Assert.Equal("del.txt", deletes[0]);

        // Verify ShouldUseDeltaPatching logic separately
        Assert.True(FileTreeDiffer.ShouldUseDeltaPatching(diff, totalFileCount: 100));
    }

    [Fact]
    public void FileTreeSnapshot_FromEnumerator()
    {
        var safeDir = "GenUpdSnap_" + System.IO.Path.GetRandomFileName();
        var rootPath = Path.Combine(Path.GetTempPath(), safeDir);
        Directory.CreateDirectory(rootPath);
        try
        {
            var filePath = Path.Combine(rootPath, "test.txt");
            File.WriteAllText(filePath, "data");

            var config = BlackListConfig.Empty;
            var enumerator = FileTreeEnumerator.FromConfig(config);

            var snapshot = FileTreeSnapshot.FromEnumerator(rootPath, enumerator);
            Assert.Equal(rootPath, snapshot.RootPath);
            Assert.NotNull(snapshot.Entries);
            Assert.NotEmpty(snapshot.Entries);
            Assert.Contains(snapshot.Entries, e => e.RelativePath.EndsWith("test.txt"));
            Assert.True(snapshot.CreatedAt <= DateTime.UtcNow);
        }
        finally
        {
            try
            {
                if (Directory.Exists(rootPath))
                {
                    foreach (var f in Directory.GetFiles(rootPath))
                    {
                        File.SetAttributes(f, FileAttributes.Normal);
                        File.Delete(f);
                    }
                    Directory.Delete(rootPath, false);
                }
            }
            catch { }
        }
    }

    [Fact]
    public void FileTreeSnapshot_Empty()
    {
        var snapshot = FileTreeSnapshot.Empty("/root");
        Assert.Empty(snapshot.Entries);
        Assert.Equal("/root", snapshot.RootPath);
    }
}
