/// <summary>
/// 测试覆盖点：
/// - ToMetadata 方法将 ExtensionDTO 完整映射到 ExtensionMetadata
///   - 所有标量字段的正确映射
///   - Categories: List<string> -> 逗号分隔 string
///   - Dependencies: List<string> -> 逗号分隔 string
///   - CustomProperties: Dictionary -> JSON 字符串
///   - Categories/Dependencies/CustomProperties 为 null 时映射为 null
///   - 空 Categories/Dependencies/CustomProperties 映射
/// </summary>
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Common.DTOs;
using GeneralUpdate.Extension.Common.Enums;

namespace GeneralUpdate.Extension.Core.Tests;

public class DefaultExtensionMetadataMapperTests
{
    private readonly DefaultExtensionMetadataMapper _mapper = new();

    [Fact]
    public void ToMetadata_标量字段正确映射()
    {
        var dto = new ExtensionDTO
        {
            Id = "ext-001",
            Name = "my-ext",
            DisplayName = "My Extension",
            Version = "1.0.0",
            FileSize = 1024,
            UploadTime = new DateTime(2025, 1, 1),
            Status = true,
            Description = "A test extension",
            Format = ".zip",
            Hash = "abc123",
            Publisher = "pub",
            License = "MIT",
            SupportedPlatforms = TargetPlatform.Windows | TargetPlatform.Linux,
            MinHostVersion = "2.0.0",
            MaxHostVersion = "5.0.0",
            ReleaseDate = new DateTime(2024, 12, 1),
            IsPreRelease = true,
            DownloadUrl = "https://dl.example.com"
        };

        var meta = _mapper.ToMetadata(dto);

        Assert.Equal("ext-001", meta.Id);
        Assert.Equal("my-ext", meta.Name);
        Assert.Equal("My Extension", meta.DisplayName);
        Assert.Equal("1.0.0", meta.Version);
        Assert.Equal(1024, meta.FileSize);
        Assert.Equal(new DateTime(2025, 1, 1), meta.UploadTime);
        Assert.True(meta.Status);
        Assert.Equal("A test extension", meta.Description);
        Assert.Equal(".zip", meta.Format);
        Assert.Equal("abc123", meta.Hash);
        Assert.Equal("pub", meta.Publisher);
        Assert.Equal("MIT", meta.License);
        Assert.Equal(TargetPlatform.Windows | TargetPlatform.Linux, meta.SupportedPlatforms);
        Assert.Equal("2.0.0", meta.MinHostVersion);
        Assert.Equal("5.0.0", meta.MaxHostVersion);
        Assert.Equal(new DateTime(2024, 12, 1), meta.ReleaseDate);
        Assert.True(meta.IsPreRelease);
        Assert.Equal("https://dl.example.com", meta.DownloadUrl);
    }

    [Fact]
    public void ToMetadata_Categories_List转为逗号分隔字符串()
    {
        var dto = new ExtensionDTO
        {
            Id = "ext-1",
            Categories = new List<string> { "tools", "ui", "debug" }
        };
        var meta = _mapper.ToMetadata(dto);
        Assert.Equal("tools,ui,debug", meta.Categories);
    }

    [Fact]
    public void ToMetadata_Categories为null_映射为null()
    {
        var dto = new ExtensionDTO { Id = "ext-1", Categories = null };
        var meta = _mapper.ToMetadata(dto);
        Assert.Null(meta.Categories);
    }

    [Fact]
    public void ToMetadata_Categories为空列表_映射为空字符串()
    {
        var dto = new ExtensionDTO { Id = "ext-1", Categories = new List<string>() };
        var meta = _mapper.ToMetadata(dto);
        Assert.Equal("", meta.Categories);
    }

    [Fact]
    public void ToMetadata_Dependencies_List转为逗号分隔字符串()
    {
        var dto = new ExtensionDTO
        {
            Id = "ext-1",
            Dependencies = new List<string> { "dep-a", "dep-b" }
        };
        var meta = _mapper.ToMetadata(dto);
        Assert.Equal("dep-a,dep-b", meta.Dependencies);
    }

    [Fact]
    public void ToMetadata_Dependencies为null_映射为null()
    {
        var dto = new ExtensionDTO { Id = "ext-1", Dependencies = null };
        var meta = _mapper.ToMetadata(dto);
        Assert.Null(meta.Dependencies);
    }

    [Fact]
    public void ToMetadata_CustomProperties_转为JSON字符串()
    {
        var dto = new ExtensionDTO
        {
            Id = "ext-1",
            CustomProperties = new Dictionary<string, string>
            {
                { "env", "prod" },
                { "debug", "false" }
            }
        };
        var meta = _mapper.ToMetadata(dto);
        Assert.NotNull(meta.CustomProperties);
        Assert.Contains("env", meta.CustomProperties);
        Assert.Contains("prod", meta.CustomProperties);
    }

    [Fact]
    public void ToMetadata_CustomProperties为null_映射为null()
    {
        var dto = new ExtensionDTO { Id = "ext-1", CustomProperties = null };
        var meta = _mapper.ToMetadata(dto);
        Assert.Null(meta.CustomProperties);
    }

    [Fact]
    public void ToMetadata_CustomProperties为空字典_映射为JSON空对象()
    {
        var dto = new ExtensionDTO
        {
            Id = "ext-1",
            CustomProperties = new Dictionary<string, string>()
        };
        var meta = _mapper.ToMetadata(dto);
        Assert.Equal("{}", meta.CustomProperties);
    }
}
