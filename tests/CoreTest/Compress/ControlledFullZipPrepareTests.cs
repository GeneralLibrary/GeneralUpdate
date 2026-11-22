using System.IO.Compression;
using System.Security.Cryptography;
using GeneralUpdate.Core.Compress;

namespace CoreTest.Compress;

public sealed class ControlledFullZipPrepareTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gu-prepare-" + Guid.NewGuid().ToString("N"));
    private string Target => Path.Combine(_root, "target");
    private string Backup => Path.Combine(_root, "backup");
    private string State => Path.Combine(_root, "state");
    private string Stage => Path.Combine(_root, "stage");

    public ControlledFullZipPrepareTests()
    {
        foreach (var path in new[] { Target, Backup, State, Stage }) Directory.CreateDirectory(path);
    }

    [WindowsFact]
    public void SnapshotsCompleteWriteSetBeforeApplying()
    {
        Directory.CreateDirectory(Path.Combine(Target, "nested"));
        File.WriteAllText(Path.Combine(Target, "nested", "old"), "original");
        File.WriteAllText(Path.Combine(Target, "unknown"), "keep");
        using var staged = Zip(("nested/old", "updated"), ("new", "added"));
        var plan = ControlledFullZipPrepare.Prepare(staged, Target, State, Backup, ["data"]);
        Assert.Equal("original", File.ReadAllText(Path.Combine(Backup, "nested", "old")));
        Assert.Equal("original", File.ReadAllText(Path.Combine(Target, "nested", "old")));
        Assert.Null(plan.Entries[1].SnapshotPath);
        Assert.Empty(Directory.GetFiles(State));
        ControlledFullZipApply.Apply(plan);
        Assert.Equal("updated", File.ReadAllText(Path.Combine(Target, "nested", "old")));
        Assert.Equal("added", File.ReadAllText(Path.Combine(Target, "new")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(Target, "unknown")));
    }

    [WindowsFact]
    public void ProtectedTargetIsRejectedBeforeAnySnapshot()
    {
        File.WriteAllText(Path.Combine(Target, "old"), "original");
        Directory.CreateDirectory(Path.Combine(Target, "data"));
        using var staged = Zip(("old", "updated"), ("data/private", "forbidden"));
        Assert.Throws<InvalidDataException>(() =>
            ControlledFullZipPrepare.Prepare(staged, Target, State, Backup, ["data"]));
        Assert.Empty(Directory.GetFileSystemEntries(Backup));
        Assert.Equal("original", File.ReadAllText(Path.Combine(Target, "old")));
    }

    [WindowsFact]
    public void OccupiedBackupAndOverlappingRootsAreRejected()
    {
        using var staged = Zip(("new", "added"));
        File.WriteAllText(Path.Combine(Backup, "unknown"), "keep");
        Assert.Throws<IOException>(() => ControlledFullZipPrepare.Prepare(staged, Target, State, Backup, []));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(Backup, "unknown")));
        Assert.Throws<InvalidDataException>(() => ControlledFullZipPrepare.Prepare(staged, Target, State, Target, []));
    }

    [WindowsFact]
    public void ChangedTargetAfterSnapshotStopsBeforeApplying()
    {
        File.WriteAllText(Path.Combine(Target, "old"), "original");
        using var staged = Zip(("old", "updated"));
        var plan = ControlledFullZipPrepare.Prepare(staged, Target, State, Backup, []);
        File.WriteAllText(Path.Combine(Target, "old"), "user change");
        Assert.ThrowsAny<IOException>(() => ControlledFullZipApply.Apply(plan));
        Assert.Equal("user change", File.ReadAllText(Path.Combine(Target, "old")));
        Assert.Equal("original", File.ReadAllText(Path.Combine(Backup, "old")));
        Assert.Empty(Directory.GetFiles(State));
    }

    [WindowsFact]
    public void DirectoryTargetIsRejectedBeforeAnySnapshot()
    {
        File.WriteAllText(Path.Combine(Target, "old"), "original");
        Directory.CreateDirectory(Path.Combine(Target, "directory"));
        using var staged = Zip(("old", "updated"), ("directory", "not a directory"));
        Assert.Throws<InvalidDataException>(() =>
            ControlledFullZipPrepare.Prepare(staged, Target, State, Backup, []));
        Assert.Empty(Directory.GetFileSystemEntries(Backup));
        Assert.Equal("original", File.ReadAllText(Path.Combine(Target, "old")));
    }

    [WindowsFact]
    public void IntentRetainsStagedFilesAfterApplyFailure()
    {
        var staged = Zip(("new", "added"));
        var stagedFile = staged.Entries[0].StagedPath;
        try
        {
            var plan = ControlledFullZipPrepare.Prepare(staged, Target, State, Backup, []);
            Assert.Throws<InvalidOperationException>(() => ControlledFullZipApply.Apply(plan, () =>
            {
                staged.RetainForRecovery();
                throw new InvalidOperationException("interrupted");
            }));
        }
        finally { staged.Dispose(); }
        Assert.True(File.Exists(stagedFile));
        Assert.Single(Directory.GetFiles(State, "*.json"));
        Assert.False(File.Exists(Path.Combine(Target, "new")));
    }

    private StagedZip Zip(params (string Name, string Content)[] entries)
    {
        string path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".zip");
        using (var file = File.Create(path))
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            foreach (var entry in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(entry.Name).Open());
                writer.Write(entry.Content);
            }
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return FullZipStager.Stage(path, Stage, Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant(),
            10, 1024 * 1024, 2 * 1024 * 1024);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class WindowsFactAttribute : FactAttribute
    {
        public WindowsFactAttribute()
        {
            if (!OperatingSystem.IsWindows()) Skip = "The controlled file implementation requires Windows.";
        }
    }
}
