using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GeneralUpdate.Core.Compress;

namespace CoreTest.Compress;

public sealed class ControlledFullZipApplyTests : IDisposable
{
    private readonly string _root = Path.Combine(Environment.CurrentDirectory, "controlled-apply-" + Guid.NewGuid().ToString("N"));
    private string Target => Path.Combine(_root, "target");
    private string State => Path.Combine(_root, "state");
    private string Input => Path.Combine(_root, "input");

    public ControlledFullZipApplyTests()
    {
        Directory.CreateDirectory(Target);
        Directory.CreateDirectory(State);
        Directory.CreateDirectory(Input);
    }

    [Fact]
    public void WritesExistingAndNewFilesAfterPersistingCompleteIntent()
    {
        File.WriteAllText(Path.Combine(Target, "old"), "old");
        var old = Entry("old", "new", "old");
        var added = Entry("added", "added content");
        string intent = ControlledFullZipApply.Apply(Plan(old, added), () =>
        {
            string path = Assert.Single(Directory.GetFiles(State, "*.json"));
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(new[] { "version", "targetRoot", "stateDirectory", "protectedPaths", "entries" },
                json.RootElement.EnumerateObject().Select(p => p.Name));
            Assert.Equal(1, json.RootElement.GetProperty("version").GetInt32());
            Assert.Equal(Target, json.RootElement.GetProperty("targetRoot").GetString());
            Assert.Equal(State, json.RootElement.GetProperty("stateDirectory").GetString());
            Assert.Equal(".backups", Assert.Single(json.RootElement.GetProperty("protectedPaths").EnumerateArray()).GetString());
            var entries = json.RootElement.GetProperty("entries");
            Assert.Equal(2, entries.GetArrayLength());
            Assert.Equal(new[] { "path", "targetPath", "stagedPath", "updatedSha256", "snapshotPath", "originalSha256", "temporaryPath" },
                entries[0].EnumerateObject().Select(p => p.Name));
            Assert.Equal("old", entries[0].GetProperty("path").GetString());
            Assert.Equal(Path.Combine(Target, "old"), entries[0].GetProperty("targetPath").GetString());
            Assert.Equal(old.StagedPath, entries[0].GetProperty("stagedPath").GetString());
            Assert.Equal(old.SnapshotPath, entries[0].GetProperty("snapshotPath").GetString());
            Assert.Equal(Hash("old"), entries[0].GetProperty("originalSha256").GetString());
            Assert.Equal(Hash("new"), entries[0].GetProperty("updatedSha256").GetString());
            Assert.StartsWith(Path.Combine(Target, ".gu-apply-"), entries[0].GetProperty("temporaryPath").GetString());
            Assert.Equal(JsonValueKind.Null, entries[1].GetProperty("originalSha256").ValueKind);
            Assert.Equal(JsonValueKind.Null, entries[1].GetProperty("snapshotPath").ValueKind);
            Assert.Equal("old", File.ReadAllText(Path.Combine(Target, "old")));
            Assert.False(File.Exists(Path.Combine(Target, "added")));
        });
        Assert.True(File.Exists(intent));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Target, "old")));
        Assert.Equal("added content", File.ReadAllText(Path.Combine(Target, "added")));
        Assert.Equal("old", File.ReadAllText(old.SnapshotPath!));
    }

    [Fact]
    public void ModifiedTargetOrMissingSnapshotStopsBeforeAnyWrite()
    {
        File.WriteAllText(Path.Combine(Target, "old"), "modified");
        var added = Entry("added", "new");
        var old = Entry("old", "new old", "original");
        Assert.ThrowsAny<IOException>(() => ControlledFullZipApply.Apply(Plan(added, old)));
        Assert.False(File.Exists(Path.Combine(Target, "added")));
        Assert.Empty(Directory.GetFiles(State));
        File.WriteAllText(Path.Combine(Target, "old"), "original");
        File.Delete(old.SnapshotPath!);
        Assert.ThrowsAny<IOException>(() => ControlledFullZipApply.Apply(Plan(added, old)));
        Assert.False(File.Exists(Path.Combine(Target, "added")));
        Assert.Empty(Directory.GetFiles(State));
    }

    [Fact]
    public void RejectsWrongSnapshotAndPayloadHashesBeforeWrites()
    {
        File.WriteAllText(Path.Combine(Target, "old"), "old");
        var old = Entry("old", "new", "old");
        File.WriteAllText(old.SnapshotPath!, "corrupt");
        Assert.ThrowsAny<IOException>(() => ControlledFullZipApply.Apply(Plan(Entry("added", "new"), old)));
        File.WriteAllText(old.SnapshotPath!, "old");
        File.WriteAllText(old.StagedPath, "corrupt");
        Assert.ThrowsAny<IOException>(() => ControlledFullZipApply.Apply(Plan(Entry("other", "new"), old)));
        Assert.Empty(Directory.GetFiles(State));
        Assert.Equal("old", File.ReadAllText(Path.Combine(Target, "old")));
    }

    [Theory]
    [InlineData("protected\\file", "protected")]
    [InlineData("protected", "protected\\file")]
    [InlineData(".backups\\file", "unrelated")]
    [InlineData("a\\..\\file", "unrelated")]
    [InlineData("file:stream", "unrelated")]
    [InlineData("CON.txt", "unrelated")]
    [InlineData("folder/file", "unrelated")]
    public void RejectsProtectedAndUnsafePaths(string name, string protect)
    {
        var plan = new ControlledFullZipPlan(Target, State, new[] { Entry(name, "new") }, new[] { protect });
        Assert.Throws<InvalidDataException>(() => ControlledFullZipApply.Apply(plan));
        Assert.Empty(Directory.GetFiles(State));
    }

    [Fact]
    public void InterruptionRetainsIntentAndSnapshotsWithoutTouchingOtherTargets()
    {
        File.WriteAllText(Path.Combine(Target, "old"), "old");
        var old = Entry("old", "new", "old");
        var other = Entry("other", "other");
        Assert.Throws<InvalidOperationException>(() => ControlledFullZipApply.Apply(Plan(old, other),
            afterWrite: _ => throw new InvalidOperationException("interrupted")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(Target, "old")));
        Assert.False(File.Exists(Path.Combine(Target, "other")));
        Assert.Equal("old", File.ReadAllText(old.SnapshotPath!));
        Assert.Single(Directory.GetFiles(State, "*.json"));
    }

    [Fact]
    public void ConflictAfterIntentIsRetainedAndDoesNotOverwrite()
    {
        var entry = Entry("new", "payload");
        Assert.ThrowsAny<IOException>(() => ControlledFullZipApply.Apply(Plan(entry),
            () => File.WriteAllText(Path.Combine(Target, "new"), "unexpected")));
        Assert.Equal("unexpected", File.ReadAllText(Path.Combine(Target, "new")));
        Assert.Single(Directory.GetFiles(State, "*.json"));
    }

    [Fact]
    public void FailureImmediatelyAfterIntentLeavesEveryTargetUnchanged()
    {
        File.WriteAllText(Path.Combine(Target, "old"), "old");
        var old = Entry("old", "new", "old");
        Assert.Throws<InvalidOperationException>(() => ControlledFullZipApply.Apply(
            Plan(old, Entry("added", "new")), () => throw new InvalidOperationException("stop")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(Target, "old")));
        Assert.False(File.Exists(Path.Combine(Target, "added")));
        Assert.Equal("old", File.ReadAllText(old.SnapshotPath!));
        Assert.Single(Directory.GetFiles(State, "*.json"));
    }

    [Fact]
    public void PreexistingNewTargetAndDuplicatePathsFailDuringPreflight()
    {
        File.WriteAllText(Path.Combine(Target, "new"), "keep");
        Assert.ThrowsAny<IOException>(() => ControlledFullZipApply.Apply(Plan(Entry("new", "payload"))));
        Assert.Throws<InvalidDataException>(() => ControlledFullZipApply.Apply(
            Plan(Entry("another", "one"), Entry("ANOTHER", "two"))));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(Target, "new")));
        Assert.Empty(Directory.GetFiles(State));
    }

    [Fact]
    public void OccupiedTemporaryPathIsRetainedAndNotOverwritten()
    {
        var entry = Entry("new", "payload");
        string? temporary = null;
        Assert.ThrowsAny<IOException>(() => ControlledFullZipApply.Apply(Plan(entry), () =>
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Assert.Single(Directory.GetFiles(State, "*.json"))));
            temporary = json.RootElement.GetProperty("entries")[0].GetProperty("temporaryPath").GetString()!;
            File.WriteAllText(temporary, "unknown");
        }));
        Assert.Equal("unknown", File.ReadAllText(temporary!));
        Assert.False(File.Exists(Path.Combine(Target, "new")));
        Assert.Single(Directory.GetFiles(State, "*.json"));
    }

    [Fact]
    public void RejectsHardLinkedTargetAndAlternateStreams()
    {
        var target = Path.Combine(Target, "old");
        File.WriteAllText(target, "old");
        string link = Path.Combine(Input, "hardlink");
        Assert.True(CreateHardLink(link, target, IntPtr.Zero));
        var entry = Entry("old", "new", "old");
        Assert.ThrowsAny<IOException>(() => ControlledFullZipApply.Apply(Plan(entry)));
        File.Delete(link);
        File.WriteAllText(target + ":extra", "extra");
        Assert.Throws<InvalidDataException>(() => ControlledFullZipApply.Apply(Plan(entry)));
        Assert.Equal("old", File.ReadAllText(target));
        Assert.Empty(Directory.GetFiles(State));
    }

    [Fact]
    public void RejectsReparsePointInTargetPath()
    {
        Directory.CreateDirectory(Path.Combine(Input, "elsewhere"));
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec")!,
            Arguments = $"/c mklink /J \"{Path.Combine(Target, "link")}\" \"{Path.Combine(Input, "elsewhere")}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        try
        {
            Assert.Throws<InvalidDataException>(() => ControlledFullZipApply.Apply(Plan(Entry("link\\file", "new"))));
            Assert.Empty(Directory.GetFiles(State));
        }
        finally { Directory.Delete(Path.Combine(Target, "link")); }
    }

    private ControlledFullZipPlan Plan(params ControlledFullZipEntry[] entries) =>
        new(Target, State, entries, Array.Empty<string>());

    private ControlledFullZipEntry Entry(string name, string payload, string? original = null)
    {
        string stage = Path.Combine(Input, Guid.NewGuid().ToString("N"));
        File.WriteAllText(stage, payload);
        string? snapshot = null;
        if (original != null)
        {
            snapshot = Path.Combine(Input, Guid.NewGuid().ToString("N"));
            File.WriteAllText(snapshot, original);
        }
        return new ControlledFullZipEntry(name, stage, Hash(payload), snapshot, original == null ? null : Hash(original));
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string file, string existingFile, IntPtr securityAttributes);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
