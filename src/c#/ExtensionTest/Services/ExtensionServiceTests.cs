using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Extension.DTOs;
using GeneralUpdate.Extension.Metadata;
using GeneralUpdate.Extension.Services;
using Moq;
using Xunit;

namespace ExtensionTest.Services
{
    /// <summary>
    /// Contains test cases for ExtensionService
    /// </summary>
    public class ExtensionServiceTests
    {
        private List<AvailableExtension> CreateTestExtensions()
        {
            return new List<AvailableExtension>
            {
                new AvailableExtension
                {
                    Descriptor = new ExtensionDescriptor
                    {
                        Name = "test-extension-1",
                        DisplayName = "Test Extension 1",
                        Version = "1.0.0",
                        Description = "First test extension",
                        Publisher = "TestPublisher",
                        Categories = new List<string> { "Testing", "Development" },
                        SupportedPlatforms = TargetPlatform.All,
                        PackageSize = 1024,
                        PackageHash = "hash1",
                        DownloadUrl = "https://example.com/ext1.zip",
                        Compatibility = new VersionCompatibility
                        {
                            MinHostVersion = new Version(1, 0, 0),
                            MaxHostVersion = new Version(2, 0, 0)
                        }
                    },
                    IsPreRelease = false
                },
                new AvailableExtension
                {
                    Descriptor = new ExtensionDescriptor
                    {
                        Name = "test-extension-2",
                        DisplayName = "Test Extension 2",
                        Version = "2.0.0",
                        Description = "Second test extension",
                        Publisher = "AnotherPublisher",
                        Categories = new List<string> { "Utilities" },
                        SupportedPlatforms = TargetPlatform.Windows | TargetPlatform.Linux,
                        PackageSize = 2048,
                        PackageHash = "hash2",
                        DownloadUrl = "https://example.com/ext2.zip"
                    },
                    IsPreRelease = true
                },
                new AvailableExtension
                {
                    Descriptor = new ExtensionDescriptor
                    {
                        Name = "test-extension-3",
                        DisplayName = "Test Extension 3",
                        Version = "1.5.0",
                        Description = "Third test extension",
                        Publisher = "TestPublisher",
                        Categories = new List<string> { "Testing" },
                        SupportedPlatforms = TargetPlatform.MacOS,
                        PackageSize = 512,
                        PackageHash = "hash3",
                        DownloadUrl = "https://example.com/ext3.zip"
                    },
                    IsPreRelease = false
                }
            };
        }

        private ExtensionService CreateExtensionService(List<AvailableExtension> extensions)
        {
            var updateQueue = new GeneralUpdate.Extension.Download.UpdateQueue();
            return new ExtensionService(extensions, "/tmp/test-downloads", updateQueue);
        }

        [Fact]
        public async Task Query_WithValidQuery_ShouldReturnPagedResults()
        {
            // Arrange
            var extensions = CreateTestExtensions();
            var service = CreateExtensionService(extensions);
            var query = new ExtensionQueryDTO
            {
                PageNumber = 1,
                PageSize = 10,
                IncludePreRelease = true // Include pre-release to get all 3 extensions
            };

            // Act
            var result = await service.Query(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Code);
            Assert.NotNull(result.Body);
            Assert.Equal(3, result.Body.TotalCount);
            Assert.Equal(1, result.Body.TotalPages);
            Assert.Equal(3, result.Body.Items.Count());
        }

        [Fact]
        public async Task Query_WithPagination_ShouldReturnCorrectPage()
        {
            // Arrange
            var extensions = CreateTestExtensions();
            var service = CreateExtensionService(extensions);
            var query = new ExtensionQueryDTO
            {
                PageNumber = 1,
                PageSize = 2,
                IncludePreRelease = true // Include pre-release to get all 3 extensions
            };

            // Act
            var result = await service.Query(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Code);
            Assert.NotNull(result.Body);
            Assert.Equal(3, result.Body.TotalCount);
            Assert.Equal(2, result.Body.TotalPages);
            Assert.Equal(2, result.Body.Items.Count());
            Assert.True(result.Body.HasNext);
            Assert.False(result.Body.HasPrevious);
        }

        [Fact]
        public async Task Query_WithNameFilter_ShouldReturnMatchingExtensions()
        {
            // Arrange
            var extensions = CreateTestExtensions();
            var service = CreateExtensionService(extensions);
            var query = new ExtensionQueryDTO
            {
                PageNumber = 1,
                PageSize = 10,
                Name = "extension-1"
            };

            // Act
            var result = await service.Query(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Code);
            Assert.NotNull(result.Body);
            Assert.Equal(1, result.Body.TotalCount);
            Assert.Single(result.Body.Items);
            Assert.Equal("test-extension-1", result.Body.Items.First().Name);
        }

        [Fact]
        public async Task Query_WithPublisherFilter_ShouldReturnMatchingExtensions()
        {
            // Arrange
            var extensions = CreateTestExtensions();
            var service = CreateExtensionService(extensions);
            var query = new ExtensionQueryDTO
            {
                PageNumber = 1,
                PageSize = 10,
                Publisher = "TestPublisher"
            };

            // Act
            var result = await service.Query(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Code);
            Assert.NotNull(result.Body);
            Assert.Equal(2, result.Body.TotalCount);
        }

        [Fact]
        public async Task Query_WithCategoryFilter_ShouldReturnMatchingExtensions()
        {
            // Arrange
            var extensions = CreateTestExtensions();
            var service = CreateExtensionService(extensions);
            var query = new ExtensionQueryDTO
            {
                PageNumber = 1,
                PageSize = 10,
                Category = "Testing"
            };

            // Act
            var result = await service.Query(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Code);
            Assert.NotNull(result.Body);
            Assert.Equal(2, result.Body.TotalCount);
        }

        [Fact]
        public async Task Query_WithPlatformFilter_ShouldReturnMatchingExtensions()
        {
            // Arrange
            var extensions = CreateTestExtensions();
            var service = CreateExtensionService(extensions);
            var query = new ExtensionQueryDTO
            {
                PageNumber = 1,
                PageSize = 10,
                TargetPlatform = TargetPlatform.Windows,
                IncludePreRelease = true // Include pre-release to get all matching extensions
            };

            // Act
            var result = await service.Query(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Code);
            Assert.NotNull(result.Body);
            // Should return extensions that support Windows (extension-1 with All, and extension-2 with Windows|Linux)
            Assert.Equal(2, result.Body.TotalCount);
        }

        [Fact]
        public async Task Query_ExcludePreRelease_ShouldNotReturnPreReleaseExtensions()
        {
            // Arrange
            var extensions = CreateTestExtensions();
            var service = CreateExtensionService(extensions);
            var query = new ExtensionQueryDTO
            {
                PageNumber = 1,
                PageSize = 10,
                IncludePreRelease = false
            };

            // Act
            var result = await service.Query(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Code);
            Assert.NotNull(result.Body);
            Assert.Equal(2, result.Body.TotalCount);
            Assert.All(result.Body.Items, item => Assert.False(item.IsPreRelease));
        }

        [Fact]
        public async Task Query_WithSearchTerm_ShouldReturnMatchingExtensions()
        {
            // Arrange
            var extensions = CreateTestExtensions();
            var service = CreateExtensionService(extensions);
            var query = new ExtensionQueryDTO
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = "Second",
                IncludePreRelease = true // Include pre-release to find the matching extension
            };

            // Act
            var result = await service.Query(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Code);
            Assert.NotNull(result.Body);
            Assert.Equal(1, result.Body.TotalCount);
            Assert.Equal("test-extension-2", result.Body.Items.First().Name);
        }

        [Fact]
        public async Task Query_WithInvalidPageNumber_ShouldReturnFailure()
        {
            // Arrange
            var extensions = CreateTestExtensions();
            var service = CreateExtensionService(extensions);
            var query = new ExtensionQueryDTO
            {
                PageNumber = 0,
                PageSize = 10
            };

            // Act
            var result = await service.Query(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Code);
            Assert.Contains("PageNumber", result.Message);
        }

        [Fact]
        public async Task Query_WithInvalidPageSize_ShouldReturnFailure()
        {
            // Arrange
            var extensions = CreateTestExtensions();
            var service = CreateExtensionService(extensions);
            var query = new ExtensionQueryDTO
            {
                PageNumber = 1,
                PageSize = 0
            };

            // Act
            var result = await service.Query(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Code);
            Assert.Contains("PageSize", result.Message);
        }

        [Fact]
        public async Task Query_WithNullQuery_ShouldReturnFailure()
        {
            // Arrange
            var extensions = CreateTestExtensions();
            var service = CreateExtensionService(extensions);

            // Act
            var result = await service.Query(null!);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Code);
            Assert.Contains("null", result.Message);
        }

        [Fact]
        public void UpdateAvailableExtensions_ShouldUpdateExtensionsList()
        {
            // Arrange
            var initialExtensions = CreateTestExtensions();
            var service = CreateExtensionService(initialExtensions);
            var newExtensions = new List<AvailableExtension>
            {
                new AvailableExtension
                {
                    Descriptor = new ExtensionDescriptor
                    {
                        Name = "new-extension",
                        DisplayName = "New Extension",
                        Version = "1.0.0"
                    }
                }
            };

            // Act
            service.UpdateAvailableExtensions(newExtensions);

            // Assert
            var query = new ExtensionQueryDTO { PageNumber = 1, PageSize = 10 };
            var result = service.Query(query).Result;
            Assert.Equal(1, result.Body.TotalCount);
            Assert.Equal("new-extension", result.Body.Items.First().Name);
        }

        [Fact]
        public async Task Download_WithInvalidId_ShouldReturnFailure()
        {
            // Arrange
            var extensions = CreateTestExtensions();
            var service = CreateExtensionService(extensions);

            // Act
            var result = await service.Download("non-existent-extension");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Code);
            // Should fail because extension is not found
            Assert.Contains("not found", result.Message);
        }

        [Fact]
        public async Task Download_WithNullOrEmptyId_ShouldReturnFailure()
        {
            // Arrange
            var extensions = CreateTestExtensions();
            var service = CreateExtensionService(extensions);

            // Act
            var result1 = await service.Download(null!);
            var result2 = await service.Download(string.Empty);

            // Assert
            Assert.Equal(400, result1.Code);
            Assert.Equal(400, result2.Code);
            // Should fail because ID is null or empty
            Assert.Contains("null or empty", result1.Message);
            Assert.Contains("null or empty", result2.Message);
        }
    }
}
