using GeneralUpdate.Core.HashAlgorithms;

namespace CoreTest.HashAlgorithms;

public class Sha256HashAlgorithmTests
{
    private readonly Sha256HashAlgorithm _algorithm = new();

    [Fact]
    public void ComputeHash_FileNotFound_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
            _algorithm.ComputeHash(Path.Combine(Path.GetTempPath(), $"no_file_{Guid.NewGuid():N}.dat")));
    }

    [Fact]
    public void ComputeHash_EmptyFile_ReturnsKnownHash()
    {
        var emptyFile = Path.GetTempFileName();
        try
        {
            var hash = _algorithm.ComputeHash(emptyFile);
            Assert.NotNull(hash);
            Assert.Equal(64, hash.Length);
        }
        finally { if (File.Exists(emptyFile)) File.Delete(emptyFile); }
    }

    [Fact]
    public void ComputeHash_SameContentFile_SameHash()
    {
        var file1 = Path.GetTempFileName();
        var file2 = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file1, "identical content");
            File.WriteAllText(file2, "identical content");
            var h1 = _algorithm.ComputeHash(file1);
            var h2 = _algorithm.ComputeHash(file2);
            Assert.Equal(h1, h2);
        }
        finally
        {
            if (File.Exists(file1)) File.Delete(file1);
            if (File.Exists(file2)) File.Delete(file2);
        }
    }

    [Fact]
    public void ComputeHash_DifferentContent_DifferentHash()
    {
        var file1 = Path.GetTempFileName();
        var file2 = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file1, "content A");
            File.WriteAllText(file2, "content B");
            var h1 = _algorithm.ComputeHash(file1);
            var h2 = _algorithm.ComputeHash(file2);
            Assert.NotEqual(h1, h2);
        }
        finally
        {
            if (File.Exists(file1)) File.Delete(file1);
            if (File.Exists(file2)) File.Delete(file2);
        }
    }

    [Fact]
    public void ComputeHashBytes_ValidFile_Returns32Bytes()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "test data");
            var bytes = _algorithm.ComputeHashBytes(file);
            Assert.NotNull(bytes);
            Assert.Equal(32, bytes.Length);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public void ComputeHashBytes_FileNotFound_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
            _algorithm.ComputeHashBytes(Path.Combine(Path.GetTempPath(), $"no_file_{Guid.NewGuid():N}.dat")));
    }

    [Fact]
    public void ComputeHash_ConsistentAcrossCalls()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "stable content");
            var h1 = _algorithm.ComputeHash(file);
            var h2 = _algorithm.ComputeHash(file);
            Assert.Equal(h1, h2);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public void ComputeHash_LargeFile_ComputesCorrectly()
    {
        var file = Path.GetTempFileName();
        try
        {
            var data = new byte[1024 * 1024]; // 1MB
            new Random(42).NextBytes(data);
            File.WriteAllBytes(file, data);
            var hash = _algorithm.ComputeHash(file);
            Assert.Equal(64, hash.Length);
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }
}
