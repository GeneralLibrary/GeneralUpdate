using GeneralUpdate.Drivelution.Core.Utilities;

namespace DrivelutionTest.Utilities;

/// <summary>
/// HashValidator 测试
/// 分支覆盖点:
/// - ComputeHashAsync: 文件存在且有效 -> 返回hash; 文件不存在 -> FileNotFoundException
/// - 算法: SHA256 (默认), MD5
/// - 不支持的算法 -> ArgumentException
/// - ValidateHashAsync: null/空 expectedHash -> ArgumentException
/// - ValidateHashAsync: 匹配 -> true, 不匹配 -> false
/// - ComputeStringHash: 空/空字符串 input -> ArgumentException
/// - ComputeStringHash: SHA256, MD5, 不支持的算法
/// - ByteArrayToHexString 内部实现
/// 触发条件：创建临时文件或字符串来测试
/// 预期结果：哈希正确，异常正确
/// </summary>
public class HashValidatorTests : IDisposable
{
    private string _tempFilePath;

    public HashValidatorTests()
    {
        _tempFilePath = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
            File.Delete(_tempFilePath);
    }

    [Fact(DisplayName = "HashValidator_ComputeHashAsync_SHA256返回64字符hex")]
    public async Task ComputeHashAsync_SHA256_Returns64CharHex()
    {
        await File.WriteAllTextAsync(_tempFilePath, "test data");

        var hash = await HashValidator.ComputeHashAsync(_tempFilePath);

        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact(DisplayName = "HashValidator_ComputeHashAsync_MD5返回32字符hex")]
    public async Task ComputeHashAsync_MD5_Returns32CharHex()
    {
        await File.WriteAllTextAsync(_tempFilePath, "test data");

        var hash = await HashValidator.ComputeHashAsync(_tempFilePath, "MD5");

        Assert.NotNull(hash);
        Assert.Equal(32, hash.Length);
    }

    [Fact(DisplayName = "HashValidator_ComputeHashAsync_文件不存在抛出FileNotFoundException")]
    public async Task ComputeHashAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            HashValidator.ComputeHashAsync("nonexistent_file.xyz"));
    }

    [Fact(DisplayName = "HashValidator_ComputeHashAsync_不支持的算法抛出ArgumentException")]
    public async Task ComputeHashAsync_UnsupportedAlgorithm_ThrowsArgumentException()
    {
        await File.WriteAllTextAsync(_tempFilePath, "data");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            HashValidator.ComputeHashAsync(_tempFilePath, "SHA1"));
    }

    [Fact(DisplayName = "HashValidator_ComputeHashAsync_大小写不敏感算法名")]
    public async Task ComputeHashAsync_CaseInsensitiveAlgorithm_Works()
    {
        await File.WriteAllTextAsync(_tempFilePath, "data");

        var hash = await HashValidator.ComputeHashAsync(_tempFilePath, "sha256");

        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact(DisplayName = "HashValidator_ComputeHashAsync_相同内容生成相同哈希")]
    public async Task ComputeHashAsync_SameContent_SameHash()
    {
        await File.WriteAllTextAsync(_tempFilePath, "identical content");

        var hash1 = await HashValidator.ComputeHashAsync(_tempFilePath);
        var hash2 = await HashValidator.ComputeHashAsync(_tempFilePath);

        Assert.Equal(hash1, hash2);
    }

    [Fact(DisplayName = "HashValidator_ComputeHashAsync_不同内容生成不同哈希")]
    public async Task ComputeHashAsync_DifferentContent_DifferentHash()
    {
        await File.WriteAllTextAsync(_tempFilePath, "content A");
        var hash1 = await HashValidator.ComputeHashAsync(_tempFilePath);

        await File.WriteAllTextAsync(_tempFilePath, "content B");
        var hash2 = await HashValidator.ComputeHashAsync(_tempFilePath);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact(DisplayName = "HashValidator_ValidateHashAsync_匹配返回true")]
    public async Task ValidateHashAsync_MatchingHash_ReturnsTrue()
    {
        await File.WriteAllTextAsync(_tempFilePath, "hello world");

        var hash = await HashValidator.ComputeHashAsync(_tempFilePath);

        Assert.True(await HashValidator.ValidateHashAsync(_tempFilePath, hash));
    }

    [Fact(DisplayName = "HashValidator_ValidateHashAsync_不匹配返回false")]
    public async Task ValidateHashAsync_MismatchingHash_ReturnsFalse()
    {
        await File.WriteAllTextAsync(_tempFilePath, "hello world");

        Assert.False(await HashValidator.ValidateHashAsync(_tempFilePath,
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"));
    }

    [Theory(DisplayName = "HashValidator_ValidateHashAsync_null或空expectedHash抛出ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ValidateHashAsync_NullOrEmptyExpectedHash_ThrowsArgumentException(string? expectedHash)
    {
        await File.WriteAllTextAsync(_tempFilePath, "data");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            HashValidator.ValidateHashAsync(_tempFilePath, expectedHash!));
    }

    [Fact(DisplayName = "HashValidator_ComputeStringHash_SHA256返回结果")]
    public void ComputeStringHash_SHA256_ReturnsHash()
    {
        var hash = HashValidator.ComputeStringHash("test input");

        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact(DisplayName = "HashValidator_ComputeStringHash_MD5返回结果")]
    public void ComputeStringHash_MD5_ReturnsHash()
    {
        var hash = HashValidator.ComputeStringHash("test input", "MD5");

        Assert.NotNull(hash);
        Assert.Equal(32, hash.Length);
    }

    [Theory(DisplayName = "HashValidator_ComputeStringHash_null或空输入抛出ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    public void ComputeStringHash_NullOrEmptyInput_ThrowsArgumentException(string? input)
    {
        Assert.Throws<ArgumentException>(() => HashValidator.ComputeStringHash(input!));
    }

    [Fact(DisplayName = "HashValidator_ComputeStringHash_不支持的算法抛出ArgumentException")]
    public void ComputeStringHash_UnsupportedAlgorithm_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => HashValidator.ComputeStringHash("data", "SHA1"));
    }

    [Fact(DisplayName = "HashValidator_ComputeStringHash_相同输入产生相同哈希")]
    public void ComputeStringHash_SameInput_SameHash()
    {
        var h1 = HashValidator.ComputeStringHash("hello");
        var h2 = HashValidator.ComputeStringHash("hello");

        Assert.Equal(h1, h2);
    }

    [Fact(DisplayName = "HashValidator_ComputeStringHash_不同输入产生不同哈希")]
    public void ComputeStringHash_DifferentInput_DifferentHash()
    {
        var h1 = HashValidator.ComputeStringHash("hello");
        var h2 = HashValidator.ComputeStringHash("world");

        Assert.NotEqual(h1, h2);
    }

    [Fact(DisplayName = "HashValidator_ComputeHashAsync_空文件仍返回有效哈希")]
    public async Task ComputeHashAsync_EmptyFile_ReturnsValidHash()
    {
        await File.WriteAllTextAsync(_tempFilePath, "");

        var hash = await HashValidator.ComputeHashAsync(_tempFilePath);

        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact(DisplayName = "HashValidator_ValidateHashAsync_大小写不敏感比较")]
    public async Task ValidateHashAsync_CaseInsensitiveComparison_ReturnsTrue()
    {
        await File.WriteAllTextAsync(_tempFilePath, "data");

        var hash = await HashValidator.ComputeHashAsync(_tempFilePath);
        var upperHash = hash.ToUpper();
        var lowerHash = hash.ToLower();

        Assert.True(await HashValidator.ValidateHashAsync(_tempFilePath, upperHash));
        Assert.True(await HashValidator.ValidateHashAsync(_tempFilePath, lowerHash));
    }
}
