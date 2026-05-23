using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;
using System.Runtime.InteropServices;

namespace ExtensionTest;

public class PlatformMatcherTests
{
    private readonly PlatformMatcher _matcher;

    public PlatformMatcherTests()
    {
        _matcher = new PlatformMatcher();
    }

    [Fact]
    public void GetCurrentPlatform_ShouldReturnValidPlatform()
    {
        // Act
        var platform = _matcher.GetCurrentPlatform();

        // Assert
        Assert.NotEqual(TargetPlatform.None, platform);
        Assert.True(
            platform == TargetPlatform.Windows ||
            platform == TargetPlatform.Linux ||
            platform == TargetPlatform.MacOS
        );
    }

    [Fact]
    public void GetCurrentPlatform_ShouldMatchRuntimeInformation()
    {
        // Act
        var platform = _matcher.GetCurrentPlatform();

        // Assert
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal(TargetPlatform.Windows, platform);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.Equal(TargetPlatform.Linux, platform);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.Equal(TargetPlatform.MacOS, platform);
        }
    }

    [Fact]
    public void IsCurrentPlatformSupported_ShouldReturnTrue_WhenAllPlatformsSupported()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            SupportedPlatforms = TargetPlatform.All
        };

        // Act
        var result = _matcher.IsCurrentPlatformSupported(extension);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCurrentPlatformSupported_ShouldReturnTrue_WhenCurrentPlatformSupported()
    {
        // Arrange
        var currentPlatform = _matcher.GetCurrentPlatform();
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            SupportedPlatforms = currentPlatform
        };

        // Act
        var result = _matcher.IsCurrentPlatformSupported(extension);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCurrentPlatformSupported_ShouldReturnFalse_WhenCurrentPlatformNotSupported()
    {
        // Arrange
        var currentPlatform = _matcher.GetCurrentPlatform();
        var otherPlatform = GetOtherPlatform(currentPlatform);
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            SupportedPlatforms = otherPlatform
        };

        // Act
        var result = _matcher.IsCurrentPlatformSupported(extension);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsCurrentPlatformSupported_ShouldReturnFalse_WhenNoPlatformSupported()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            SupportedPlatforms = TargetPlatform.None
        };

        // Act
        var result = _matcher.IsCurrentPlatformSupported(extension);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPlatformSupported_ShouldReturnTrue_WhenWindowsSupported()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            SupportedPlatforms = TargetPlatform.Windows
        };

        // Act
        var result = _matcher.IsPlatformSupported(extension, TargetPlatform.Windows);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPlatformSupported_ShouldReturnTrue_WhenLinuxSupported()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            SupportedPlatforms = TargetPlatform.Linux
        };

        // Act
        var result = _matcher.IsPlatformSupported(extension, TargetPlatform.Linux);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPlatformSupported_ShouldReturnTrue_WhenMacOSSupported()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            SupportedPlatforms = TargetPlatform.MacOS
        };

        // Act
        var result = _matcher.IsPlatformSupported(extension, TargetPlatform.MacOS);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPlatformSupported_ShouldReturnTrue_WhenMultiplePlatformsSupported()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            SupportedPlatforms = TargetPlatform.Windows | TargetPlatform.Linux
        };

        // Act
        var result1 = _matcher.IsPlatformSupported(extension, TargetPlatform.Windows);
        var result2 = _matcher.IsPlatformSupported(extension, TargetPlatform.Linux);
        var result3 = _matcher.IsPlatformSupported(extension, TargetPlatform.MacOS);

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.False(result3);
    }

    [Fact]
    public void IsPlatformSupported_ShouldReturnTrue_WhenAllPlatformsSupported()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            SupportedPlatforms = TargetPlatform.All
        };

        // Act
        var result1 = _matcher.IsPlatformSupported(extension, TargetPlatform.Windows);
        var result2 = _matcher.IsPlatformSupported(extension, TargetPlatform.Linux);
        var result3 = _matcher.IsPlatformSupported(extension, TargetPlatform.MacOS);

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
    }

    [Fact]
    public void IsPlatformSupported_ShouldReturnFalse_WhenPlatformNotSupported()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            SupportedPlatforms = TargetPlatform.Windows
        };

        // Act
        var result1 = _matcher.IsPlatformSupported(extension, TargetPlatform.Linux);
        var result2 = _matcher.IsPlatformSupported(extension, TargetPlatform.MacOS);

        // Assert
        Assert.False(result1);
        Assert.False(result2);
    }

    [Fact]
    public void IsPlatformSupported_ShouldReturnFalse_WhenNoPlatformSupported()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            SupportedPlatforms = TargetPlatform.None
        };

        // Act
        var result1 = _matcher.IsPlatformSupported(extension, TargetPlatform.Windows);
        var result2 = _matcher.IsPlatformSupported(extension, TargetPlatform.Linux);
        var result3 = _matcher.IsPlatformSupported(extension, TargetPlatform.MacOS);

        // Assert
        Assert.False(result1);
        Assert.False(result2);
        Assert.False(result3);
    }

    private TargetPlatform GetOtherPlatform(TargetPlatform current)
    {
        return current switch
        {
            TargetPlatform.Windows => TargetPlatform.Linux,
            TargetPlatform.Linux => TargetPlatform.MacOS,
            TargetPlatform.MacOS => TargetPlatform.Windows,
            _ => TargetPlatform.Windows
        };
    }
}
