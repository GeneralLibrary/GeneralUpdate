/// <summary>
/// 测试覆盖点：
/// - IsCompatible(extension, hostVersion)
///   - hostVersion 为 null/空/空白 => 返回 true
///   - hostVersion 为无效版本字符串 => 返回 false
///   - MinHostVersion: null/空=跳过, 无效=返回false, host<Min=false, host>=Min=下一检查
///   - MaxHostVersion: null/空=跳过, 无效=返回false, host>Max=false, host<=Max=true
///   - 同时有Min和Max约束的组合
/// - FindLatestCompatibleVersion(extensions, hostVersion)
///   - 空列表/null => null, 全部不兼容 => null
///   - 多个兼容版本 => 返回版本号最大者
///   - 含无效版本号 => 有效版本优先
/// </summary>
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Compatibility.Tests;

public class VersionCompatibilityCheckerTests
{
    private readonly VersionCompatibilityChecker _checker = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsCompatible_hostVersion为null或空白_返回true(string hostVersion)
    {
        var ext = new ExtensionMetadata();
        Assert.True(_checker.IsCompatible(ext, hostVersion));
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("1.x.0")]
    [InlineData("abc")]
    public void IsCompatible_hostVersion无效_返回false(string hostVersion)
    {
        var ext = new ExtensionMetadata();
        Assert.False(_checker.IsCompatible(ext, hostVersion));
    }

    [Fact]
    public void IsCompatible_无Min和Max约束_返回true()
    {
        var ext = new ExtensionMetadata();
        Assert.True(_checker.IsCompatible(ext, "1.0.0"));
    }

    [Fact]
    public void IsCompatible_MinHostVersion为null_跳过Min检查()
    {
        var ext = new ExtensionMetadata { MinHostVersion = null, MaxHostVersion = null };
        Assert.True(_checker.IsCompatible(ext, "1.0.0"));
    }

    [Fact]
    public void IsCompatible_MinHostVersion为空字符串_跳过Min检查()
    {
        var ext = new ExtensionMetadata { MinHostVersion = "", MaxHostVersion = null };
        Assert.True(_checker.IsCompatible(ext, "1.0.0"));
    }

    [Fact]
    public void IsCompatible_MinHostVersion无效格式_返回false()
    {
        var ext = new ExtensionMetadata { MinHostVersion = "invalid" };
        Assert.False(_checker.IsCompatible(ext, "1.0.0"));
    }

    [Fact]
    public void IsCompatible_host低于MinHostVersion_返回false()
    {
        var ext = new ExtensionMetadata { MinHostVersion = "2.0.0" };
        Assert.False(_checker.IsCompatible(ext, "1.0.0"));
    }

    [Fact]
    public void IsCompatible_host等于MinHostVersion_返回true()
    {
        var ext = new ExtensionMetadata { MinHostVersion = "2.0.0" };
        Assert.True(_checker.IsCompatible(ext, "2.0.0"));
    }

    [Fact]
    public void IsCompatible_host高于MinHostVersion_无Max约束_返回true()
    {
        var ext = new ExtensionMetadata { MinHostVersion = "2.0.0" };
        Assert.True(_checker.IsCompatible(ext, "3.0.0"));
    }

    [Fact]
    public void IsCompatible_MaxHostVersion为null_跳过Max检查()
    {
        var ext = new ExtensionMetadata { MaxHostVersion = null };
        Assert.True(_checker.IsCompatible(ext, "10.0.0"));
    }

    [Fact]
    public void IsCompatible_MaxHostVersion为空_跳过Max检查()
    {
        var ext = new ExtensionMetadata { MaxHostVersion = "" };
        Assert.True(_checker.IsCompatible(ext, "10.0.0"));
    }

    [Fact]
    public void IsCompatible_MaxHostVersion无效格式_返回false()
    {
        var ext = new ExtensionMetadata { MaxHostVersion = "invalid" };
        Assert.False(_checker.IsCompatible(ext, "2.0.0"));
    }

    [Fact]
    public void IsCompatible_host高于MaxHostVersion_返回false()
    {
        var ext = new ExtensionMetadata { MaxHostVersion = "3.0.0" };
        Assert.False(_checker.IsCompatible(ext, "4.0.0"));
    }

    [Fact]
    public void IsCompatible_host等于MaxHostVersion_返回true()
    {
        var ext = new ExtensionMetadata { MaxHostVersion = "3.0.0" };
        Assert.True(_checker.IsCompatible(ext, "3.0.0"));
    }

    [Fact]
    public void IsCompatible_host在Min与Max之间_返回true()
    {
        var ext = new ExtensionMetadata { MinHostVersion = "1.0.0", MaxHostVersion = "5.0.0" };
        Assert.True(_checker.IsCompatible(ext, "3.0.0"));
    }

    [Fact]
    public void IsCompatible_host在MinMax范围内_返回true()
    {
        var ext = new ExtensionMetadata { MinHostVersion = "2.0.0", MaxHostVersion = "4.0.0" };
        Assert.True(_checker.IsCompatible(ext, "3.0.0"));
    }

    [Fact]
    public void IsCompatible_host低于Min但在Max内_返回false()
    {
        var ext = new ExtensionMetadata { MinHostVersion = "2.0.0", MaxHostVersion = "4.0.0" };
        Assert.False(_checker.IsCompatible(ext, "1.0.0"));
    }

    [Fact]
    public void IsCompatible_host高于Max也高于Min_返回false()
    {
        var ext = new ExtensionMetadata { MinHostVersion = "2.0.0", MaxHostVersion = "4.0.0" };
        Assert.False(_checker.IsCompatible(ext, "5.0.0"));
    }

    [Fact]
    public void FindLatestCompatibleVersion_列表为null_返回null()
    {
        Assert.Null(_checker.FindLatestCompatibleVersion(null!, "1.0.0"));
    }

    [Fact]
    public void FindLatestCompatibleVersion_列表为空_返回null()
    {
        Assert.Null(_checker.FindLatestCompatibleVersion(new List<ExtensionMetadata>(), "1.0.0"));
    }

    [Fact]
    public void FindLatestCompatibleVersion_全部不兼容_返回null()
    {
        var list = new List<ExtensionMetadata>
        {
            new() { Version = "1.0.0", MinHostVersion = "5.0.0" },
            new() { Version = "2.0.0", MinHostVersion = "5.0.0" }
        };
        Assert.Null(_checker.FindLatestCompatibleVersion(list, "3.0.0"));
    }

    [Fact]
    public void FindLatestCompatibleVersion_单个兼容_返回该扩展()
    {
        var list = new List<ExtensionMetadata>
        {
            new() { Id = "ext-1", Version = "1.0.0" }
        };
        var result = _checker.FindLatestCompatibleVersion(list, "2.0.0");
        Assert.NotNull(result);
        Assert.Equal("ext-1", result.Id);
    }

    [Fact]
    public void FindLatestCompatibleVersion_多个兼容_返回版本号最大()
    {
        var list = new List<ExtensionMetadata>
        {
            new() { Id = "v1", Version = "1.0.0" },
            new() { Id = "v3", Version = "3.0.0" },
            new() { Id = "v2", Version = "2.0.0" }
        };
        var result = _checker.FindLatestCompatibleVersion(list, "4.0.0");
        Assert.NotNull(result);
        Assert.Equal("v3", result.Id);
    }

    [Fact]
    public void FindLatestCompatibleVersion_含无效版本号_有效版本优先()
    {
        var list = new List<ExtensionMetadata>
        {
            new() { Id = "bad-ver", Version = "not-a-version" },
            new() { Id = "good-v1", Version = "1.0.0" }
        };
        var result = _checker.FindLatestCompatibleVersion(list, "2.0.0");
        Assert.NotNull(result);
        Assert.Equal("good-v1", result.Id);
    }

    [Fact]
    public void FindLatestCompatibleVersion_部分不兼容_兼容中取最大版本()
    {
        var list = new List<ExtensionMetadata>
        {
            new() { Id = "c1", Version = "1.0.0" },
            new() { Id = "c3", Version = "3.0.0" },
            new() { Id = "inc", Version = "5.0.0", MinHostVersion = "10.0.0" }
        };
        var result = _checker.FindLatestCompatibleVersion(list, "5.0.0");
        Assert.NotNull(result);
        Assert.Equal("c3", result.Id);
    }

    [Fact]
    public void FindLatestCompatibleVersion_所有有效但不兼容_返回null()
    {
        var list = new List<ExtensionMetadata>
        {
            new() { Version = "1.0.0", MaxHostVersion = "1.0.0" },
            new() { Version = "2.0.0", MaxHostVersion = "2.0.0" }
        };
        Assert.Null(_checker.FindLatestCompatibleVersion(list, "5.0.0"));
    }
}
