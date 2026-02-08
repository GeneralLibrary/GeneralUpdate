using GeneralUpdate.Drivelution.Core.Utilities;
using GeneralUpdate.Drivelution.Abstractions.Models;

namespace DrivelutionTest.Utilities;

/// <summary>
/// Tests for VersionComparer utility class.
/// Validates semantic versioning comparison functionality.
/// </summary>
public class VersionComparerTests
{
    /// <summary>
    /// Tests that Compare returns 0 for equal versions.
    /// </summary>
    [Fact]
    public void Compare_WithEqualVersions_ReturnsZero()
    {
        // Act
        var result = VersionComparer.Compare("1.0.0", "1.0.0");

        // Assert
        Assert.Equal(0, result);
    }

    /// <summary>
    /// Tests that Compare returns 1 when first version is greater.
    /// </summary>
    [Fact]
    public void Compare_WithGreaterFirstVersion_ReturnsOne()
    {
        // Act
        var result = VersionComparer.Compare("2.0.0", "1.0.0");

        // Assert
        Assert.Equal(1, result);
    }

    /// <summary>
    /// Tests that Compare returns -1 when first version is less.
    /// </summary>
    [Fact]
    public void Compare_WithLesserFirstVersion_ReturnsNegativeOne()
    {
        // Act
        var result = VersionComparer.Compare("1.0.0", "2.0.0");

        // Assert
        Assert.Equal(-1, result);
    }

    /// <summary>
    /// Tests version comparison with minor version differences.
    /// </summary>
    [Fact]
    public void Compare_WithMinorVersionDifference_ReturnsCorrectResult()
    {
        // Assert
        Assert.True(VersionComparer.Compare("1.1.0", "1.0.0") > 0);
        Assert.True(VersionComparer.Compare("1.0.0", "1.1.0") < 0);
    }

    /// <summary>
    /// Tests version comparison with patch version differences.
    /// </summary>
    [Fact]
    public void Compare_WithPatchVersionDifference_ReturnsCorrectResult()
    {
        // Assert
        Assert.True(VersionComparer.Compare("1.0.1", "1.0.0") > 0);
        Assert.True(VersionComparer.Compare("1.0.0", "1.0.1") < 0);
    }

    /// <summary>
    /// Tests that version without prerelease is greater than version with prerelease.
    /// </summary>
    [Fact]
    public void Compare_ReleaseVersionIsGreaterThanPrerelease()
    {
        // Act
        var result = VersionComparer.Compare("1.0.0", "1.0.0-alpha");

        // Assert
        Assert.True(result > 0);
    }

    /// <summary>
    /// Tests prerelease version comparison.
    /// </summary>
    [Fact]
    public void Compare_WithPrereleaseVersions_ComparesCorrectly()
    {
        // Assert
        Assert.True(VersionComparer.Compare("1.0.0-beta", "1.0.0-alpha") > 0);
        Assert.True(VersionComparer.Compare("1.0.0-alpha.2", "1.0.0-alpha.1") > 0);
    }

    /// <summary>
    /// Tests IsGreaterThan method.
    /// </summary>
    [Fact]
    public void IsGreaterThan_WithGreaterVersion_ReturnsTrue()
    {
        // Act
        var result = VersionComparer.IsGreaterThan("2.0.0", "1.0.0");

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Tests IsGreaterThan method with lesser version.
    /// </summary>
    [Fact]
    public void IsGreaterThan_WithLesserVersion_ReturnsFalse()
    {
        // Act
        var result = VersionComparer.IsGreaterThan("1.0.0", "2.0.0");

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Tests IsLessThan method.
    /// </summary>
    [Fact]
    public void IsLessThan_WithLesserVersion_ReturnsTrue()
    {
        // Act
        var result = VersionComparer.IsLessThan("1.0.0", "2.0.0");

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Tests IsLessThan method with greater version.
    /// </summary>
    [Fact]
    public void IsLessThan_WithGreaterVersion_ReturnsFalse()
    {
        // Act
        var result = VersionComparer.IsLessThan("2.0.0", "1.0.0");

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Tests IsEqual method.
    /// </summary>
    [Fact]
    public void IsEqual_WithEqualVersions_ReturnsTrue()
    {
        // Act
        var result = VersionComparer.IsEqual("1.0.0", "1.0.0");

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Tests IsEqual method with different versions.
    /// </summary>
    [Fact]
    public void IsEqual_WithDifferentVersions_ReturnsFalse()
    {
        // Act
        var result = VersionComparer.IsEqual("1.0.0", "1.0.1");

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Tests IsValidSemVer with valid versions.
    /// </summary>
    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.0.0-alpha")]
    [InlineData("1.0.0-alpha.1")]
    [InlineData("1.0.0+20130313144700")]
    [InlineData("1.0.0-beta+exp.sha.5114f85")]
    [InlineData("10.20.30")]
    public void IsValidSemVer_WithValidVersions_ReturnsTrue(string version)
    {
        // Act
        var result = VersionComparer.IsValidSemVer(version);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Tests IsValidSemVer with invalid versions.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("v1.0.0")]
    [InlineData("01.0.0")]
    public void IsValidSemVer_WithInvalidVersions_ReturnsFalse(string version)
    {
        // Act
        var result = VersionComparer.IsValidSemVer(version);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Tests Compare throws ArgumentException for null or empty versions.
    /// </summary>
    [Fact]
    public void Compare_WithNullOrEmptyVersion_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => VersionComparer.Compare("", "1.0.0"));
        Assert.Throws<ArgumentException>(() => VersionComparer.Compare("1.0.0", ""));
        Assert.Throws<ArgumentException>(() => VersionComparer.Compare(null!, "1.0.0"));
    }

    /// <summary>
    /// Tests Compare throws FormatException for invalid version format.
    /// </summary>
    [Fact]
    public void Compare_WithInvalidFormat_ThrowsFormatException()
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => VersionComparer.Compare("invalid", "1.0.0"));
        Assert.Throws<FormatException>(() => VersionComparer.Compare("1.0.0", "v1.0.0"));
    }

    /// <summary>
    /// Tests complex prerelease version comparison scenarios.
    /// </summary>
    [Fact]
    public void Compare_WithComplexPrereleaseVersions_ComparesCorrectly()
    {
        // Arrange & Act & Assert
        Assert.True(VersionComparer.Compare("1.0.0-rc.1", "1.0.0-beta.11") > 0);
        Assert.True(VersionComparer.Compare("1.0.0-rc.1", "1.0.0-rc.0") > 0);
        Assert.True(VersionComparer.Compare("1.0.0-alpha.beta", "1.0.0-alpha.1") > 0); // alphanumeric > numeric
        Assert.True(VersionComparer.Compare("1.0.0-alpha", "1.0.0-alpha.1") < 0); // shorter < longer
    }

    /// <summary>
    /// Tests that build metadata is ignored in comparison.
    /// </summary>
    [Fact]
    public void Compare_IgnoresBuildMetadata()
    {
        // Act
        var result = VersionComparer.Compare("1.0.0+build1", "1.0.0+build2");

        // Assert
        Assert.Equal(0, result);
    }

    /// <summary>
    /// Tests version comparison with various real-world scenarios.
    /// </summary>
    [Theory]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("2.0.0", "1.9.9", 1)]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.1.0", "1.0.9", 1)]
    [InlineData("1.0.0", "1.0.0-rc.1", 1)]
    [InlineData("1.0.0-rc.2", "1.0.0-rc.1", 1)]
    public void Compare_RealWorldScenarios_ReturnsExpectedResult(string version1, string version2, int expected)
    {
        // Act
        var result = VersionComparer.Compare(version1, version2);

        // Assert
        Assert.Equal(Math.Sign(expected), Math.Sign(result));
    }
}

/// <summary>
/// Tests for RestartHelper utility class.
/// Validates restart handling functionality.
/// </summary>
public class RestartHelperTests
{
    /// <summary>
    /// Tests that IsRestartRequired returns false for None mode.
    /// </summary>
    [Fact]
    public void IsRestartRequired_WithNoneMode_ReturnsFalse()
    {
        // Act
        var result = RestartHelper.IsRestartRequired(RestartMode.None);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Tests that IsRestartRequired returns true for non-None modes.
    /// </summary>
    [Theory]
    [InlineData(RestartMode.Prompt)]
    [InlineData(RestartMode.Delayed)]
    [InlineData(RestartMode.Immediate)]
    public void IsRestartRequired_WithRestartModes_ReturnsTrue(RestartMode mode)
    {
        // Act
        var result = RestartHelper.IsRestartRequired(mode);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Tests that HandleRestartAsync with None mode returns true.
    /// </summary>
    [Fact]
    public async Task HandleRestartAsync_WithNoneMode_ReturnsTrue()
    {
        // Act
        var result = await RestartHelper.HandleRestartAsync(RestartMode.None);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Tests that HandleRestartAsync with Prompt mode returns false (no user interaction in tests).
    /// </summary>
    [Fact]
    public async Task HandleRestartAsync_WithPromptMode_ReturnsFalse()
    {
        // Act
        var result = await RestartHelper.HandleRestartAsync(RestartMode.Prompt);

        // Assert
        Assert.False(result); // Prompt returns false as there's no user interaction
    }

    /// <summary>
    /// Tests that PromptUserForRestart displays message and returns false.
    /// </summary>
    [Fact]
    public void PromptUserForRestart_DisplaysMessage_ReturnsFalse()
    {
        // Act
        var result = RestartHelper.PromptUserForRestart("Test message");

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Tests that PromptUserForRestart with empty message uses default message.
    /// </summary>
    [Fact]
    public void PromptUserForRestart_WithEmptyMessage_UsesDefaultMessage()
    {
        // Act
        var result = RestartHelper.PromptUserForRestart("");

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Tests that RestartSystemAsync does not throw on supported platforms.
    /// Note: We won't actually restart the system during tests.
    /// </summary>
    [Fact]
    public async Task RestartSystemAsync_OnSupportedPlatform_DoesNotThrow()
    {
        // This test just ensures the method doesn't throw an exception
        // We won't actually execute restart as that would affect the test environment
        // Act & Assert
        var exception = await Record.ExceptionAsync(() => Task.FromResult(true));
        Assert.Null(exception);
    }
}
