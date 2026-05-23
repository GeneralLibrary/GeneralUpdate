using GeneralUpdate.Drivelution.Core.Utilities;

namespace DrivelutionTest.Utilities;

/// <summary>
/// Tests for HashValidator utility class.
/// Validates file hash computation and validation functionality.
/// </summary>
public class HashValidatorTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly string _testContent = "Hello, World! This is test content for hash validation.";

    public HashValidatorTests()
    {
        // Create a temporary test file
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
        File.WriteAllText(_testFilePath, _testContent);
    }

    /// <summary>
    /// Cleanup temporary test files.
    /// </summary>
    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            try
            {
                File.Delete(_testFilePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Tests that ComputeHashAsync returns a valid SHA256 hash.
    /// </summary>
    [Fact]
    public async Task ComputeHashAsync_WithSHA256_ReturnsValidHash()
    {
        // Act
        var hash = await HashValidator.ComputeHashAsync(_testFilePath, "SHA256");

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        Assert.Equal(64, hash.Length); // SHA256 hash is 64 hex characters
        Assert.Matches("^[a-f0-9]+$", hash); // Only lowercase hex characters
    }

    /// <summary>
    /// Tests that ComputeHashAsync returns a valid MD5 hash.
    /// </summary>
    [Fact]
    public async Task ComputeHashAsync_WithMD5_ReturnsValidHash()
    {
        // Act
        var hash = await HashValidator.ComputeHashAsync(_testFilePath, "MD5");

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        Assert.Equal(32, hash.Length); // MD5 hash is 32 hex characters
        Assert.Matches("^[a-f0-9]+$", hash);
    }

    /// <summary>
    /// Tests that ComputeHashAsync throws FileNotFoundException for non-existent file.
    /// </summary>
    [Fact]
    public async Task ComputeHashAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.txt");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => HashValidator.ComputeHashAsync(nonExistentFile));
    }

    /// <summary>
    /// Tests that ComputeHashAsync throws ArgumentException for unsupported algorithm.
    /// </summary>
    [Fact]
    public async Task ComputeHashAsync_WithUnsupportedAlgorithm_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => HashValidator.ComputeHashAsync(_testFilePath, "UNSUPPORTED"));
    }

    /// <summary>
    /// Tests that ValidateHashAsync returns true for matching hash.
    /// </summary>
    [Fact]
    public async Task ValidateHashAsync_WithMatchingHash_ReturnsTrue()
    {
        // Arrange
        var expectedHash = await HashValidator.ComputeHashAsync(_testFilePath, "SHA256");

        // Act
        var isValid = await HashValidator.ValidateHashAsync(_testFilePath, expectedHash, "SHA256");

        // Assert
        Assert.True(isValid);
    }

    /// <summary>
    /// Tests that ValidateHashAsync returns false for non-matching hash.
    /// </summary>
    [Fact]
    public async Task ValidateHashAsync_WithNonMatchingHash_ReturnsFalse()
    {
        // Arrange
        var wrongHash = "0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        var isValid = await HashValidator.ValidateHashAsync(_testFilePath, wrongHash, "SHA256");

        // Assert
        Assert.False(isValid);
    }

    /// <summary>
    /// Tests that ValidateHashAsync is case-insensitive.
    /// </summary>
    [Fact]
    public async Task ValidateHashAsync_IsCaseInsensitive()
    {
        // Arrange
        var expectedHash = await HashValidator.ComputeHashAsync(_testFilePath, "SHA256");
        var upperCaseHash = expectedHash.ToUpperInvariant();

        // Act
        var isValid = await HashValidator.ValidateHashAsync(_testFilePath, upperCaseHash, "SHA256");

        // Assert
        Assert.True(isValid);
    }

    /// <summary>
    /// Tests that ValidateHashAsync throws ArgumentException for null or empty hash.
    /// </summary>
    [Fact]
    public async Task ValidateHashAsync_WithNullOrEmptyHash_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => HashValidator.ValidateHashAsync(_testFilePath, "", "SHA256"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => HashValidator.ValidateHashAsync(_testFilePath, null!, "SHA256"));
    }

    /// <summary>
    /// Tests that ComputeStringHash returns a valid hash for string input.
    /// </summary>
    [Fact]
    public void ComputeStringHash_WithValidString_ReturnsValidHash()
    {
        // Arrange
        var input = "Test String";

        // Act
        var hash = HashValidator.ComputeStringHash(input, "SHA256");

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        Assert.Equal(64, hash.Length);
    }

    /// <summary>
    /// Tests that ComputeStringHash returns consistent results.
    /// </summary>
    [Fact]
    public void ComputeStringHash_WithSameInput_ReturnsConsistentHash()
    {
        // Arrange
        var input = "Consistent Test";

        // Act
        var hash1 = HashValidator.ComputeStringHash(input, "SHA256");
        var hash2 = HashValidator.ComputeStringHash(input, "SHA256");

        // Assert
        Assert.Equal(hash1, hash2);
    }

    /// <summary>
    /// Tests that ComputeStringHash returns different hashes for different inputs.
    /// </summary>
    [Fact]
    public void ComputeStringHash_WithDifferentInputs_ReturnsDifferentHashes()
    {
        // Arrange
        var input1 = "Test 1";
        var input2 = "Test 2";

        // Act
        var hash1 = HashValidator.ComputeStringHash(input1, "SHA256");
        var hash2 = HashValidator.ComputeStringHash(input2, "SHA256");

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    /// <summary>
    /// Tests that ComputeStringHash throws ArgumentException for null or empty input.
    /// </summary>
    [Fact]
    public void ComputeStringHash_WithNullOrEmptyInput_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => HashValidator.ComputeStringHash(""));
        Assert.Throws<ArgumentException>(() => HashValidator.ComputeStringHash(null!));
    }

    /// <summary>
    /// Tests that hash computation can be cancelled.
    /// </summary>
    [Fact]
    public async Task ComputeHashAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => HashValidator.ComputeHashAsync(_testFilePath, "SHA256", cts.Token));
    }
}
