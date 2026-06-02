/// <summary>
/// 测试覆盖点：
/// - 默认值: ServerUrl="", Scheme="", Token="", HostVersion="", ExtensionsDirectory=""
/// - CatalogPath 默认为 null
/// - 所有属性赋值和读取
/// </summary>
namespace GeneralUpdate.Extension.Common.Models.Tests;

public class ExtensionHostOptionsTests
{
    [Fact]
    public void 默认构造_ServerUrl为空字符串()
    {
        var opts = new ExtensionHostOptions();
        Assert.Equal(string.Empty, opts.ServerUrl);
    }

    [Fact]
    public void 默认构造_Scheme_Token_HostVersion_ExtensionsDirectory_为空字符串()
    {
        var opts = new ExtensionHostOptions();
        Assert.Equal(string.Empty, opts.Scheme);
        Assert.Equal(string.Empty, opts.Token);
        Assert.Equal(string.Empty, opts.HostVersion);
        Assert.Equal(string.Empty, opts.ExtensionsDirectory);
    }

    [Fact]
    public void 默认构造_CatalogPath为null()
    {
        var opts = new ExtensionHostOptions();
        Assert.Null(opts.CatalogPath);
    }

    [Fact]
    public void 所有属性赋值后可正确读取()
    {
        var opts = new ExtensionHostOptions
        {
            ServerUrl = "https://api.example.com",
            Scheme = "Bearer",
            Token = "abc123",
            HostVersion = "2.0.0",
            ExtensionsDirectory = @"C:\extensions",
            CatalogPath = @"C:\catalog"
        };
        Assert.Equal("https://api.example.com", opts.ServerUrl);
        Assert.Equal("Bearer", opts.Scheme);
        Assert.Equal("abc123", opts.Token);
        Assert.Equal("2.0.0", opts.HostVersion);
        Assert.Equal(@"C:\extensions", opts.ExtensionsDirectory);
        Assert.Equal(@"C:\catalog", opts.CatalogPath);
    }

    [Fact]
    public void CatalogPath_可保持null()
    {
        var opts = new ExtensionHostOptions
        {
            ServerUrl = "http://localhost",
            HostVersion = "1.0.0",
            ExtensionsDirectory = "/tmp"
        };
        Assert.Null(opts.CatalogPath);
    }
}
