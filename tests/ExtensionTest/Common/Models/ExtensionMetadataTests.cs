/// <summary>
/// 测试覆盖点：
/// - 默认值: Id=null, SupportedPlatforms=All, IsPreRelease=false
/// - DependencyList 属性：Dependencies 为 null/空/空白/单值/多值/含空格/含空段/仅逗号
/// - DependencyList 缓存机制
/// - 所有可空属性赋值和读取
/// </summary>
using Xunit;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Common.Models.Tests;

public class ExtensionMetadataTests
{
    [Fact]
    public void 默认构造_Id为null() => Assert.Null(new ExtensionMetadata().Id);

    [Fact]
    public void 默认构造_SupportedPlatforms_应为All()
        => Assert.Equal(TargetPlatform.All, new ExtensionMetadata().SupportedPlatforms);

    [Fact]
    public void 默认构造_IsPreRelease_应为false()
        => Assert.False(new ExtensionMetadata().IsPreRelease);

    [Fact]
    public void DependencyList_Dependencies为null_返回空列表()
        => Assert.Empty(new ExtensionMetadata { Dependencies = null }.DependencyList);

    [Fact]
    public void DependencyList_Dependencies为空字符串_返回空列表()
        => Assert.Empty(new ExtensionMetadata { Dependencies = "" }.DependencyList);

    [Fact]
    public void DependencyList_Dependencies为空白字符串_返回空列表()
        => Assert.Empty(new ExtensionMetadata { Dependencies = "   " }.DependencyList);

    [Fact]
    public void DependencyList_单个依赖()
    {
        var meta = new ExtensionMetadata { Dependencies = "dep1" };
        Assert.Single(meta.DependencyList);
        Assert.Equal("dep1", meta.DependencyList[0]);
    }

    [Fact]
    public void DependencyList_多个依赖_逗号分隔()
    {
        var meta = new ExtensionMetadata { Dependencies = "dep1,dep2,dep3" };
        Assert.Equal(3, meta.DependencyList.Count);
        Assert.Equal("dep1", meta.DependencyList[0]);
        Assert.Equal("dep2", meta.DependencyList[1]);
        Assert.Equal("dep3", meta.DependencyList[2]);
    }

    [Fact]
    public void DependencyList_含空格trim处理()
    {
        var meta = new ExtensionMetadata { Dependencies = " dep1 , dep2 , dep3 " };
        Assert.Equal(3, meta.DependencyList.Count);
        Assert.Equal("dep1", meta.DependencyList[0]);
    }

    [Fact]
    public void DependencyList_含空段_过滤空段()
    {
        var meta = new ExtensionMetadata { Dependencies = "dep1,,dep2" };
        Assert.Equal(2, meta.DependencyList.Count);
    }

    [Fact]
    public void DependencyList_仅逗号_返回空列表()
        => Assert.Empty(new ExtensionMetadata { Dependencies = "," }.DependencyList);

    [Fact]
    public void DependencyList_缓存机制_相同实例返回同一引用()
    {
        var meta = new ExtensionMetadata { Dependencies = "a,b" };
        Assert.Same(meta.DependencyList, meta.DependencyList);
    }

    [Fact]
    public void DependencyList_修改Dependencies后重新读取_仍返回旧缓存()
    {
        var meta = new ExtensionMetadata { Dependencies = "a,b" };
        var list1 = meta.DependencyList;
        meta.Dependencies = "c,d";
        Assert.Same(list1, meta.DependencyList);
    }

    [Fact]
    public void Name_DisplayName_Version_可赋值()
    {
        var meta = new ExtensionMetadata { Name = "my-ext", DisplayName = "My Ext", Version = "1.2.3" };
        Assert.Equal("my-ext", meta.Name);
        Assert.Equal("My Ext", meta.DisplayName);
        Assert.Equal("1.2.3", meta.Version);
    }

    [Fact]
    public void FileSize_可赋值为0和long最大值()
    {
        Assert.Equal(0, new ExtensionMetadata { FileSize = 0 }.FileSize);
        Assert.Equal(long.MaxValue, new ExtensionMetadata { FileSize = long.MaxValue }.FileSize);
    }

    [Fact]
    public void Status_三态赋值()
    {
        var meta = new ExtensionMetadata();
        Assert.Null(meta.Status);
        meta.Status = true;
        Assert.True(meta.Status);
        meta.Status = false;
        Assert.False(meta.Status);
    }

    [Fact]
    public void Hash_Format_Publisher_License_DownloadUrl_CustomProperties_可赋值()
    {
        var meta = new ExtensionMetadata
        {
            Hash = "abcdef",
            Format = ".zip",
            Publisher = "pub",
            License = "MIT",
            DownloadUrl = "https://example.com",
            CustomProperties = @"{""k"":""v""}"
        };
        Assert.Equal("abcdef", meta.Hash);
        Assert.Equal(".zip", meta.Format);
        Assert.Equal("pub", meta.Publisher);
        Assert.Equal("MIT", meta.License);
        Assert.Equal("https://example.com", meta.DownloadUrl);
        Assert.Equal(@"{""k"":""v""}", meta.CustomProperties);
    }
}
