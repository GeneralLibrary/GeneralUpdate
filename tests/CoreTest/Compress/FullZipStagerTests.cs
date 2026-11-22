using System.IO.Compression;
using System.Security.Cryptography;
using GeneralUpdate.Core.Compress;

namespace CoreTest.Compress;

public sealed class FullZipStagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Environment.CurrentDirectory, "zip-stage-tests-" + Guid.NewGuid().ToString("N"));
    private string ZipPath => Path.Combine(_root, "package.zip");
    private string Parent => Path.Combine(_root, "staging");

    public FullZipStagerTests()
    {
        Directory.CreateDirectory(Parent);
    }

    [Fact]
    public void StagesFilesAndHashesAndDisposesOnlyItsOwnDirectory()
    {
        CreateZip(("folder/", "", 0), ("folder/hello.txt", "hello", 0), ("other.txt", "world", 0));
        var untouched = Path.Combine(Parent, "untouched");
        Directory.CreateDirectory(untouched);
        File.WriteAllText(Path.Combine(untouched, "keep"), "keep");

        string stagedDirectory;
        using (var result = Stage())
        {
            stagedDirectory = result.DirectoryPath;
            Assert.Equal(2, result.Entries.Count);
            Assert.Equal("folder/hello.txt", result.Entries[0].RelativePath);
            Assert.Equal("hello", File.ReadAllText(result.Entries[0].StagedPath));
            Assert.Equal(Hex(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("hello"))), result.Entries[0].Sha256);
            Assert.Equal("world", File.ReadAllText(result.Entries[1].StagedPath));
            Assert.NotEqual(result.Entries[0].StagedPath, result.Entries[1].StagedPath);
            Assert.All(result.Entries, x => Assert.Equal(stagedDirectory, Path.GetDirectoryName(x.StagedPath)));
        }
        Assert.False(Directory.Exists(stagedDirectory));
        Assert.True(File.Exists(Path.Combine(untouched, "keep")));
    }

    [Theory]
    [InlineData("/root")]
    [InlineData("C:/drive")]
    [InlineData("file:stream")]
    [InlineData("a\\b")]
    [InlineData("../evil")]
    [InlineData("a/./b")]
    [InlineData("a//b")]
    [InlineData("name.")]
    [InlineData("name ")]
    [InlineData("CON.txt")]
    [InlineData("aux .txt")]
    [InlineData("COM¹.bin")]
    [InlineData("LPT9")]
    [InlineData(".BACKUPS/file")]
    [InlineData("a/\u0001b")]
    [InlineData("a?/b")]
    [InlineData("a//")]
    public void RejectsUnsafeNames(string name)
    {
        CreateZip((name, "", 0));
        Assert.Throws<InvalidDataException>(() => Stage());
        Assert.Empty(Directory.GetFileSystemEntries(Parent));
    }

    [Theory]
    [InlineData("a.txt", "A.TXT")]
    [InlineData("a", "a/b")]
    [InlineData("a/b", "A")]
    [InlineData("a/", "A/")]
    [InlineData("a/", "a")]
    public void RejectsDuplicateAndConflictingPaths(string first, string second)
    {
        CreateZip((first, "", 0), (second, "", 0));
        Assert.Throws<InvalidDataException>(() => Stage());
        Assert.Empty(Directory.GetFileSystemEntries(Parent));
    }

    [Theory]
    [InlineData(0xA0000000u)] // Unix symbolic link
    [InlineData(0x10000000u)] // Unix FIFO
    [InlineData(0x60000000u)] // Unix block device
    [InlineData(0x00000400u)] // Windows reparse point
    [InlineData(0x00000040u)] // Windows device
    [InlineData(0x04000000u)] // Windows reparse point in the upper attributes
    [InlineData(0x00400000u)] // Windows device in the upper attributes
    [InlineData(0x40000000u)] // Unix directory without a directory path
    [InlineData(0x84000000u)] // Unix regular file with special mode bits
    public void RejectsSpecialEntries(uint attributes)
    {
        CreateZip(("unsafe", "", unchecked((int)attributes)));
        Assert.Throws<InvalidDataException>(() => Stage());
        Assert.Empty(Directory.GetFileSystemEntries(Parent));
    }

    [Fact]
    public void AllowsExplicitDirectoryAfterAnImplicitDirectory()
    {
        CreateZip(("Dir/child", "content", 0), ("dir/", "", 0));
        using var result = Stage();
        Assert.Single(result.Entries);
    }

    [Fact]
    public void AllowsUnixRegularFileWithExecutablePermissions()
    {
        CreateZip(("executable", "content", unchecked((int)0x81ed0000u)));
        using var result = Stage();
        Assert.Single(result.Entries);
    }

    [Fact]
    public void EnforcesEntryAndExpandedSizeLimits()
    {
        CreateZip(("one", "123", 0), ("two", "456", 0));
        Assert.Throws<InvalidDataException>(() => Stage(maxEntries: 1));
        Assert.Throws<InvalidDataException>(() => Stage(maxFile: 2));
        Assert.Throws<InvalidDataException>(() => Stage(maxTotal: 5));
        Assert.Empty(Directory.GetFileSystemEntries(Parent));
        using var result = Stage(maxEntries: 2, maxFile: 3, maxTotal: 6);
        Assert.Equal(2, result.Entries.Count);
    }

    [Fact]
    public void RejectsMissingInvalidAndWrongHash()
    {
        CreateZip(("one", "data", 0));
        Assert.Throws<ArgumentException>(() => FullZipStager.Stage(ZipPath, Parent, "", 10, 100, 100));
        Assert.Throws<ArgumentException>(() => FullZipStager.Stage(ZipPath, Parent, new string('z', 64), 10, 100, 100));
        Assert.Throws<InvalidDataException>(() => FullZipStager.Stage(ZipPath, Parent, new string('0', 64), 10, 100, 100));
        Assert.Empty(Directory.GetFileSystemEntries(Parent));
    }

    [Fact]
    public void RejectsDirectoryContent()
    {
        CreateZip(("dir/", "not empty", 0));
        Assert.Throws<InvalidDataException>(() => Stage());
        Assert.Empty(Directory.GetFileSystemEntries(Parent));
    }

    [Fact]
    public void ReportsCleanupFailureWithoutDeletingUnknownFiles()
    {
        CreateZip(("one", "data", 0));
        var result = Stage();
        string unexpected = Path.Combine(result.DirectoryPath, "unexpected");
        File.WriteAllText(unexpected, "keep");
        Assert.Throws<AggregateException>(() => result.Dispose());
        Assert.True(File.Exists(unexpected));
        File.Delete(unexpected);
        result.Dispose();
        Assert.False(Directory.Exists(result.DirectoryPath));
    }

    [Fact]
    public void CleansUpPartialStagingAfterCorruptCompressedData()
    {
        CreateZip(("first", "valid", 0), ("second", new string('x', 500), 0));
        byte[] zip = File.ReadAllBytes(ZipPath);
        // Locate the second local header and invalidate the deflate stream.
        int firstNameLength = BitConverter.ToUInt16(zip, 26);
        int firstExtraLength = BitConverter.ToUInt16(zip, 28);
        int firstCompressedLength = BitConverter.ToInt32(zip, 18);
        int secondHeader = 30 + firstNameLength + firstExtraLength + firstCompressedLength;
        Assert.Equal(0x04034b50, BitConverter.ToInt32(zip, secondHeader));
        int secondData = secondHeader + 30 + BitConverter.ToUInt16(zip, secondHeader + 26) + BitConverter.ToUInt16(zip, secondHeader + 28);
        zip[secondData] = 0xff;
        File.WriteAllBytes(ZipPath, zip);
        Assert.ThrowsAny<Exception>(() => Stage());
        Assert.Empty(Directory.GetFileSystemEntries(Parent));
    }

    [Fact]
    public void RejectsActualContentExceedingDeclaredLengthAndCleansUp()
    {
        CreateZip(("first", "valid", 0), ("second", new string('x', 500), 0));
        byte[] zip = File.ReadAllBytes(ZipPath);
        int end = zip.Length - 22;
        Assert.Equal(0x06054b50, BitConverter.ToInt32(zip, end));
        int central = BitConverter.ToInt32(zip, end + 16);
        int second = central + 46 + BitConverter.ToUInt16(zip, central + 28) +
            BitConverter.ToUInt16(zip, central + 30) + BitConverter.ToUInt16(zip, central + 32);
        Assert.Equal(0x02014b50, BitConverter.ToInt32(zip, second));
        int local = BitConverter.ToInt32(zip, second + 42);
        Assert.Equal(0x04034b50, BitConverter.ToInt32(zip, local));
        // Make both headers and CRC agree with the truncated prefix. The compressed
        // stream still expands to 500 bytes and must be read to completion.
        Array.Copy(BitConverter.GetBytes(0x8cdc1683u), 0, zip, second + 16, 4);
        Array.Copy(BitConverter.GetBytes(0x8cdc1683u), 0, zip, local + 14, 4);
        Array.Copy(BitConverter.GetBytes(1), 0, zip, second + 24, 4);
        Array.Copy(BitConverter.GetBytes(1), 0, zip, local + 22, 4);
        File.WriteAllBytes(ZipPath, zip);
        Assert.ThrowsAny<Exception>(() => Stage());
        Assert.Empty(Directory.GetFileSystemEntries(Parent));
    }

    private StagedZip Stage(int maxEntries = 100, long maxFile = 10000, long maxTotal = 10000)
        => FullZipStager.Stage(ZipPath, Parent, Hex(SHA256.HashData(File.ReadAllBytes(ZipPath))), maxEntries, maxFile, maxTotal);

    private static string Hex(byte[] value) => Convert.ToHexString(value).ToLowerInvariant();

    private void CreateZip(params (string Name, string Content, int Attributes)[] entries)
    {
        using var archive = ZipFile.Open(ZipPath, ZipArchiveMode.Create);
        foreach (var item in entries)
        {
            var entry = archive.CreateEntry(item.Name);
            entry.ExternalAttributes = item.Attributes;
            if (item.Content.Length != 0)
            {
                using var writer = new StreamWriter(entry.Open());
                writer.Write(item.Content);
            }
        }
    }

    public void Dispose() => Directory.Delete(_root, true);
}
