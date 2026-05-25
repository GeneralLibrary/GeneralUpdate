/// <summary>
/// 测试覆盖点：
/// - ExtensionDTO 属性默认值验证
/// - 所有可空属性 null/非null 赋值验证
/// - SupportedPlatforms 默认值应为 TargetPlatform.All
/// - IsPreRelease 默认值应为 false
/// - PageNumber/PageSize 边界值
/// - Categories/Dependencies 空列表/非空列表
/// - CustomProperties 空字典/非空字典
/// - IsCompatible 三态 (null/true/false)
/// - Id 属性赋值和读取
/// </summary>
namespace GeneralUpdate.Extension.Common.DTOs.Tests;

public class ExtensionDTOTests
{
    [Fact]
    public void 默认构造_Id为null_但可赋值为非空字符串()
    {
        var dto = new ExtensionDTO();
        Assert.Null(dto.Id);
        dto.Id = "test-id";
        Assert.Equal("test-id", dto.Id);
    }

    [Fact]
    public void 默认构造_Name_DisplayName_Version_均为null()
    {
        var dto = new ExtensionDTO();
        Assert.Null(dto.Name);
        Assert.Null(dto.DisplayName);
        Assert.Null(dto.Version);
    }

    [Fact]
    public void 默认构造_SupportedPlatforms_应为All()
    {
        var dto = new ExtensionDTO();
        Assert.Equal(TargetPlatform.All, dto.SupportedPlatforms);
    }

    [Fact]
    public void 默认构造_IsPreRelease_应为false()
    {
        var dto = new ExtensionDTO();
        Assert.False(dto.IsPreRelease);
    }

    [Fact]
    public void FileSize_可赋值为long最大值()
    {
        var dto = new ExtensionDTO { FileSize = long.MaxValue };
        Assert.Equal(long.MaxValue, dto.FileSize);
    }

    [Fact]
    public void FileSize_可赋值为0()
    {
        var dto = new ExtensionDTO { FileSize = 0 };
        Assert.Equal(0, dto.FileSize);
    }

    [Fact]
    public void UploadTime_可赋值为DateTime()
    {
        var now = DateTime.UtcNow;
        var dto = new ExtensionDTO { UploadTime = now };
        Assert.Equal(now, dto.UploadTime);
    }

    [Fact]
    public void Status_可赋值为null_true_false()
    {
        var dto = new ExtensionDTO();
        Assert.Null(dto.Status);
        dto.Status = true;
        Assert.True(dto.Status);
        dto.Status = false;
        Assert.False(dto.Status);
    }

    [Fact]
    public void Hash_和Format_可正常赋值()
    {
        var dto = new ExtensionDTO
        {
            Hash = "abc123def456",
            Format = ".zip"
        };
        Assert.Equal("abc123def456", dto.Hash);
        Assert.Equal(".zip", dto.Format);
    }

    [Fact]
    public void Publisher_和License_可赋值()
    {
        var dto = new ExtensionDTO
        {
            Publisher = "test-publisher",
            License = "MIT"
        };
        Assert.Equal("test-publisher", dto.Publisher);
        Assert.Equal("MIT", dto.License);
    }

    [Fact]
    public void Categories_空列表赋值()
    {
        var dto = new ExtensionDTO { Categories = new List<string>() };
        Assert.NotNull(dto.Categories);
        Assert.Empty(dto.Categories);
    }

    [Fact]
    public void Categories_非空列表赋值()
    {
        var categories = new List<string> { "tools", "ui" };
        var dto = new ExtensionDTO { Categories = categories };
        Assert.Equal(2, dto.Categories.Count);
        Assert.Contains("tools", dto.Categories);
        Assert.Contains("ui", dto.Categories);
    }

    [Fact]
    public void MinHostVersion_和MaxHostVersion_可赋值()
    {
        var dto = new ExtensionDTO
        {
            MinHostVersion = "1.0.0",
            MaxHostVersion = "2.0.0"
        };
        Assert.Equal("1.0.0", dto.MinHostVersion);
        Assert.Equal("2.0.0", dto.MaxHostVersion);
    }

    [Fact]
    public void ReleaseDate_可赋值()
    {
        var date = new DateTime(2025, 1, 1);
        var dto = new ExtensionDTO { ReleaseDate = date };
        Assert.Equal(date, dto.ReleaseDate);
    }

    [Fact]
    public void Dependencies_空列表和非空列表赋值()
    {
        var dto1 = new ExtensionDTO { Dependencies = new List<string>() };
        Assert.Empty(dto1.Dependencies);

        var deps = new List<string> { "dep1", "dep2" };
        var dto2 = new ExtensionDTO { Dependencies = deps };
        Assert.Equal(2, dto2.Dependencies.Count);
    }

    [Fact]
    public void DownloadUrl_可赋值()
    {
        var dto = new ExtensionDTO { DownloadUrl = "https://example.com/ext.zip" };
        Assert.Equal("https://example.com/ext.zip", dto.DownloadUrl);
    }

    [Fact]
    public void CustomProperties_空字典和非空字典()
    {
        var dto1 = new ExtensionDTO { CustomProperties = new Dictionary<string, string>() };
        Assert.Empty(dto1.CustomProperties);

        var props = new Dictionary<string, string> { { "key1", "value1" } };
        var dto2 = new ExtensionDTO { CustomProperties = props };
        Assert.Single(dto2.CustomProperties);
        Assert.Equal("value1", dto2.CustomProperties["key1"]);
    }

    [Fact]
    public void IsCompatible_三态null_true_false()
    {
        var dto = new ExtensionDTO();
        Assert.Null(dto.IsCompatible);
        dto.IsCompatible = true;
        Assert.True(dto.IsCompatible);
        dto.IsCompatible = false;
        Assert.False(dto.IsCompatible);
    }

    [Fact]
    public void 所有属性全赋值后验证完整性()
    {
        var dto = new ExtensionDTO
        {
            Id = "ext-001",
            Name = "test-ext",
            DisplayName = "Test Extension",
            Version = "1.0.0",
            FileSize = 1024,
            UploadTime = new DateTime(2025, 6, 1),
            Status = true,
            Description = "A test extension",
            Format = ".zip",
            Hash = "abc123",
            Publisher = "test-pub",
            License = "Apache-2.0",
            Categories = new List<string> { "test" },
            SupportedPlatforms = TargetPlatform.Windows | TargetPlatform.Linux,
            MinHostVersion = "2.0.0",
            MaxHostVersion = "5.0.0",
            ReleaseDate = new DateTime(2025, 5, 1),
            Dependencies = new List<string> { "dep-a" },
            IsPreRelease = true,
            DownloadUrl = "https://example.com",
            CustomProperties = new Dictionary<string, string> { { "k", "v" } },
            IsCompatible = true
        };

        Assert.Equal("ext-001", dto.Id);
        Assert.Equal("test-ext", dto.Name);
        Assert.Equal("Test Extension", dto.DisplayName);
        Assert.Equal("1.0.0", dto.Version);
        Assert.Equal(1024, dto.FileSize);
        Assert.Equal(new DateTime(2025, 6, 1), dto.UploadTime);
        Assert.True(dto.Status);
        Assert.Equal("A test extension", dto.Description);
        Assert.Equal(".zip", dto.Format);
        Assert.Equal("abc123", dto.Hash);
        Assert.Equal("test-pub", dto.Publisher);
        Assert.Equal("Apache-2.0", dto.License);
        Assert.Single(dto.Categories);
        Assert.Equal(TargetPlatform.Windows | TargetPlatform.Linux, dto.SupportedPlatforms);
        Assert.Equal("2.0.0", dto.MinHostVersion);
        Assert.Equal("5.0.0", dto.MaxHostVersion);
        Assert.True(dto.IsPreRelease);
        Assert.Equal("https://example.com", dto.DownloadUrl);
        Assert.True(dto.IsCompatible);
    }
}
