using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Common.Models;
using GeneralUpdate.Extension.Dependencies;
using Moq;

namespace ExtensionTest;

public class DependencyResolverTests
{
    [Fact]
    public void ResolveDependencies_ShouldReturnEmpty_WhenNoDependencies()
    {
        // Arrange
        var catalogMock = new Mock<IExtensionCatalog>();
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            Dependencies = null
        };
        catalogMock.Setup(c => c.GetInstalledExtensionById("ext1"))
            .Returns(extension);

        var resolver = new DependencyResolver(catalogMock.Object);

        // Act
        var result = resolver.ResolveDependencies(extension);

        // Assert
        Assert.Single(result);
        Assert.Equal("ext1", result[0]);
    }

    [Fact]
    public void ResolveDependencies_ShouldReturnEmpty_WhenDependenciesEmpty()
    {
        // Arrange
        var catalogMock = new Mock<IExtensionCatalog>();
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            Dependencies = ""
        };
        catalogMock.Setup(c => c.GetInstalledExtensionById("ext1"))
            .Returns(extension);

        var resolver = new DependencyResolver(catalogMock.Object);

        // Act
        var result = resolver.ResolveDependencies(extension);

        // Assert
        Assert.Single(result);
        Assert.Equal("ext1", result[0]);
    }

    [Fact]
    public void ResolveDependencies_ShouldReturnSingleDependency()
    {
        // Arrange
        var catalogMock = new Mock<IExtensionCatalog>();
        var dep1 = new ExtensionMetadata
        {
            Id = "dep1",
            Dependencies = null
        };
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            Dependencies = "dep1"
        };

        catalogMock.Setup(c => c.GetInstalledExtensionById("ext1"))
            .Returns(extension);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep1"))
            .Returns(dep1);

        var resolver = new DependencyResolver(catalogMock.Object);

        // Act
        var result = resolver.ResolveDependencies(extension);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("dep1", result[0]);
        Assert.Equal("ext1", result[1]);
    }

    [Fact]
    public void ResolveDependencies_ShouldReturnMultipleDependencies()
    {
        // Arrange
        var catalogMock = new Mock<IExtensionCatalog>();
        var dep1 = new ExtensionMetadata
        {
            Id = "dep1",
            Dependencies = null
        };
        var dep2 = new ExtensionMetadata
        {
            Id = "dep2",
            Dependencies = null
        };
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            Dependencies = "dep1,dep2"
        };

        catalogMock.Setup(c => c.GetInstalledExtensionById("ext1"))
            .Returns(extension);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep1"))
            .Returns(dep1);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep2"))
            .Returns(dep2);

        var resolver = new DependencyResolver(catalogMock.Object);

        // Act
        var result = resolver.ResolveDependencies(extension);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("dep1", result);
        Assert.Contains("dep2", result);
        Assert.Equal("ext1", result[2]);
    }

    [Fact]
    public void ResolveDependencies_ShouldHandleNestedDependencies()
    {
        // Arrange
        var catalogMock = new Mock<IExtensionCatalog>();
        var dep2 = new ExtensionMetadata
        {
            Id = "dep2",
            Dependencies = null
        };
        var dep1 = new ExtensionMetadata
        {
            Id = "dep1",
            Dependencies = "dep2"
        };
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            Dependencies = "dep1"
        };

        catalogMock.Setup(c => c.GetInstalledExtensionById("ext1"))
            .Returns(extension);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep1"))
            .Returns(dep1);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep2"))
            .Returns(dep2);

        var resolver = new DependencyResolver(catalogMock.Object);

        // Act
        var result = resolver.ResolveDependencies(extension);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("dep2", result[0]);
        Assert.Equal("dep1", result[1]);
        Assert.Equal("ext1", result[2]);
    }

    [Fact]
    public void ResolveDependencies_ShouldHandleSharedDependencies()
    {
        // Arrange
        var catalogMock = new Mock<IExtensionCatalog>();
        var depShared = new ExtensionMetadata
        {
            Id = "shared",
            Dependencies = null
        };
        var dep1 = new ExtensionMetadata
        {
            Id = "dep1",
            Dependencies = "shared"
        };
        var dep2 = new ExtensionMetadata
        {
            Id = "dep2",
            Dependencies = "shared"
        };
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            Dependencies = "dep1,dep2"
        };

        catalogMock.Setup(c => c.GetInstalledExtensionById("ext1"))
            .Returns(extension);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep1"))
            .Returns(dep1);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep2"))
            .Returns(dep2);
        catalogMock.Setup(c => c.GetInstalledExtensionById("shared"))
            .Returns(depShared);

        var resolver = new DependencyResolver(catalogMock.Object);

        // Act
        var result = resolver.ResolveDependencies(extension);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal("shared", result[0]);
        Assert.Contains("dep1", result);
        Assert.Contains("dep2", result);
        Assert.Equal("ext1", result[3]);
    }

    [Fact]
    public void ResolveDependencies_ShouldThrow_WhenCircularDependency()
    {
        // Arrange
        var catalogMock = new Mock<IExtensionCatalog>();
        var dep1 = new ExtensionMetadata
        {
            Id = "dep1",
            Dependencies = "dep2"
        };
        var dep2 = new ExtensionMetadata
        {
            Id = "dep2",
            Dependencies = "dep1"
        };

        catalogMock.Setup(c => c.GetInstalledExtensionById("dep1"))
            .Returns(dep1);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep2"))
            .Returns(dep2);

        var resolver = new DependencyResolver(catalogMock.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => resolver.ResolveDependencies(dep1));
    }

    [Fact]
    public void ResolveDependencies_ShouldHandleWhitespaceInDependencies()
    {
        // Arrange
        var catalogMock = new Mock<IExtensionCatalog>();
        var dep1 = new ExtensionMetadata
        {
            Id = "dep1",
            Dependencies = null
        };
        var dep2 = new ExtensionMetadata
        {
            Id = "dep2",
            Dependencies = null
        };
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            Dependencies = " dep1 , dep2 "
        };

        catalogMock.Setup(c => c.GetInstalledExtensionById("ext1"))
            .Returns(extension);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep1"))
            .Returns(dep1);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep2"))
            .Returns(dep2);

        var resolver = new DependencyResolver(catalogMock.Object);

        // Act
        var result = resolver.ResolveDependencies(extension);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("dep1", result);
        Assert.Contains("dep2", result);
    }

    [Fact]
    public void GetMissingDependencies_ShouldReturnEmpty_WhenAllInstalled()
    {
        // Arrange
        var catalogMock = new Mock<IExtensionCatalog>();
        var dep1 = new ExtensionMetadata
        {
            Id = "dep1",
            Dependencies = null
        };
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            Dependencies = "dep1"
        };

        catalogMock.Setup(c => c.GetInstalledExtensionById("ext1"))
            .Returns(extension);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep1"))
            .Returns(dep1);

        var resolver = new DependencyResolver(catalogMock.Object);

        // Act
        var result = resolver.GetMissingDependencies(extension);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetMissingDependencies_ShouldReturnMissing_WhenNotInstalled()
    {
        // Arrange
        var catalogMock = new Mock<IExtensionCatalog>();
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            Dependencies = "dep1"
        };

        catalogMock.Setup(c => c.GetInstalledExtensionById("ext1"))
            .Returns(extension);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep1"))
            .Returns((ExtensionMetadata?)null);

        var resolver = new DependencyResolver(catalogMock.Object);

        // Act
        var result = resolver.GetMissingDependencies(extension);

        // Assert
        Assert.Single(result);
        Assert.Equal("dep1", result[0]);
    }

    [Fact]
    public void GetMissingDependencies_ShouldReturnAllMissing_WhenMultipleMissing()
    {
        // Arrange
        var catalogMock = new Mock<IExtensionCatalog>();
        var dep1 = new ExtensionMetadata
        {
            Id = "dep1",
            Dependencies = null
        };
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            Dependencies = "dep1,dep2,dep3"
        };

        catalogMock.Setup(c => c.GetInstalledExtensionById("ext1"))
            .Returns(extension);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep1"))
            .Returns(dep1);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep2"))
            .Returns((ExtensionMetadata?)null);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep3"))
            .Returns((ExtensionMetadata?)null);

        var resolver = new DependencyResolver(catalogMock.Object);

        // Act
        var result = resolver.GetMissingDependencies(extension);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("dep2", result);
        Assert.Contains("dep3", result);
    }

    [Fact]
    public void GetMissingDependencies_ShouldHandleNestedMissing()
    {
        // Arrange
        var catalogMock = new Mock<IExtensionCatalog>();
        var dep1 = new ExtensionMetadata
        {
            Id = "dep1",
            Dependencies = "dep2"
        };
        var extension = new ExtensionMetadata
        {
            Id = "ext1",
            Dependencies = "dep1"
        };

        catalogMock.Setup(c => c.GetInstalledExtensionById("ext1"))
            .Returns(extension);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep1"))
            .Returns(dep1);
        catalogMock.Setup(c => c.GetInstalledExtensionById("dep2"))
            .Returns((ExtensionMetadata?)null);

        var resolver = new DependencyResolver(catalogMock.Object);

        // Act
        var result = resolver.GetMissingDependencies(extension);

        // Assert
        Assert.Single(result);
        Assert.Equal("dep2", result[0]);
    }
}
