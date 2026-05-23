using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Common.Models;

namespace ExtensionTest;

public class VersionCompatibilityCheckerTests
{
    private readonly VersionCompatibilityChecker _checker;

    public VersionCompatibilityCheckerTests()
    {
        _checker = new VersionCompatibilityChecker();
    }

    [Fact]
    public void IsCompatible_ShouldReturnTrue_WhenNoVersionConstraints()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            Name = "TestExtension"
        };

        // Act
        var result = _checker.IsCompatible(extension, "1.0.0");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCompatible_ShouldReturnTrue_WhenHostVersionEmpty()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MinHostVersion = "1.0.0",
            MaxHostVersion = "2.0.0"
        };

        // Act
        var result = _checker.IsCompatible(extension, "");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCompatible_ShouldReturnTrue_WhenHostVersionMeetsMinVersion()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MinHostVersion = "1.0.0"
        };

        // Act
        var result = _checker.IsCompatible(extension, "1.5.0");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCompatible_ShouldReturnTrue_WhenHostVersionEqualsMinVersion()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MinHostVersion = "1.0.0"
        };

        // Act
        var result = _checker.IsCompatible(extension, "1.0.0");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCompatible_ShouldReturnFalse_WhenHostVersionBelowMinVersion()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MinHostVersion = "2.0.0"
        };

        // Act
        var result = _checker.IsCompatible(extension, "1.0.0");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsCompatible_ShouldReturnTrue_WhenHostVersionUnderMaxVersion()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MaxHostVersion = "2.0.0"
        };

        // Act
        var result = _checker.IsCompatible(extension, "1.5.0");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCompatible_ShouldReturnTrue_WhenHostVersionEqualsMaxVersion()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MaxHostVersion = "2.0.0"
        };

        // Act
        var result = _checker.IsCompatible(extension, "2.0.0");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCompatible_ShouldReturnFalse_WhenHostVersionAboveMaxVersion()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MaxHostVersion = "2.0.0"
        };

        // Act
        var result = _checker.IsCompatible(extension, "3.0.0");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsCompatible_ShouldReturnTrue_WhenHostVersionInRange()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MinHostVersion = "1.0.0",
            MaxHostVersion = "3.0.0"
        };

        // Act
        var result = _checker.IsCompatible(extension, "2.0.0");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsCompatible_ShouldReturnFalse_WhenHostVersionOutOfRange()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MinHostVersion = "1.0.0",
            MaxHostVersion = "2.0.0"
        };

        // Act
        var result1 = _checker.IsCompatible(extension, "0.5.0");
        var result2 = _checker.IsCompatible(extension, "3.0.0");

        // Assert
        Assert.False(result1);
        Assert.False(result2);
    }

    [Fact]
    public void IsCompatible_ShouldReturnFalse_WhenHostVersionInvalid()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MinHostVersion = "1.0.0"
        };

        // Act
        var result = _checker.IsCompatible(extension, "invalid-version");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsCompatible_ShouldReturnFalse_WhenMinVersionInvalid()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MinHostVersion = "invalid"
        };

        // Act
        var result = _checker.IsCompatible(extension, "1.0.0");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsCompatible_ShouldReturnFalse_WhenMaxVersionInvalid()
    {
        // Arrange
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            MaxHostVersion = "invalid"
        };

        // Act
        var result = _checker.IsCompatible(extension, "1.0.0");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void FindLatestCompatibleVersion_ShouldReturnLatestVersion()
    {
        // Arrange
        var extensions = new List<ExtensionMetadata>
        {
            new ExtensionMetadata { Id = "ext1", Version = "1.0.0", MinHostVersion = "1.0.0" },
            new ExtensionMetadata { Id = "ext1", Version = "1.5.0", MinHostVersion = "1.0.0" },
            new ExtensionMetadata { Id = "ext1", Version = "2.0.0", MinHostVersion = "1.0.0" }
        };

        // Act
        var result = _checker.FindLatestCompatibleVersion(extensions, "1.5.0");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("2.0.0", result.Version);
    }

    [Fact]
    public void FindLatestCompatibleVersion_ShouldReturnNullWhenNoneCompatible()
    {
        // Arrange
        var extensions = new List<ExtensionMetadata>
        {
            new ExtensionMetadata { Id = "ext1", Version = "1.0.0", MinHostVersion = "2.0.0" },
            new ExtensionMetadata { Id = "ext1", Version = "1.5.0", MinHostVersion = "2.0.0" }
        };

        // Act
        var result = _checker.FindLatestCompatibleVersion(extensions, "1.0.0");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FindLatestCompatibleVersion_ShouldHandleMaxVersion()
    {
        // Arrange
        var extensions = new List<ExtensionMetadata>
        {
            new ExtensionMetadata { Id = "ext1", Version = "1.0.0", MaxHostVersion = "2.0.0" },
            new ExtensionMetadata { Id = "ext1", Version = "1.5.0", MaxHostVersion = "1.5.0" },
            new ExtensionMetadata { Id = "ext1", Version = "2.0.0", MaxHostVersion = "3.0.0" }
        };

        // Act
        var result = _checker.FindLatestCompatibleVersion(extensions, "2.5.0");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("2.0.0", result.Version);
    }

    [Fact]
    public void FindLatestCompatibleVersion_ShouldReturnNullForEmptyList()
    {
        // Arrange
        var extensions = new List<ExtensionMetadata>();

        // Act
        var result = _checker.FindLatestCompatibleVersion(extensions, "1.0.0");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FindLatestCompatibleVersion_ShouldHandleVersionParsing()
    {
        // Arrange
        var extensions = new List<ExtensionMetadata>
        {
            new ExtensionMetadata { Id = "ext1", Version = "1.0.0.0" },
            new ExtensionMetadata { Id = "ext1", Version = "1.10.0" },
            new ExtensionMetadata { Id = "ext1", Version = "1.2.0" }
        };

        // Act
        var result = _checker.FindLatestCompatibleVersion(extensions, "1.0.0");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("1.10.0", result.Version);
    }
}
