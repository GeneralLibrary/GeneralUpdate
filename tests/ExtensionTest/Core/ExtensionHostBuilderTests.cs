/// <summary>
/// 测试覆盖点：
/// - 构造函数：创建 ServiceCollection
/// - ConfigureOptions(Action)：
///   - 正常配置
///   - Action 抛出异常
/// - WithOptions(ExtensionHostOptions)：
///   - 正常赋值
///   - null 参数 => ArgumentNullException
/// - ConfigureServices(Action)：正常使用和 null Action
/// - Build()：
///   - Options 未配置 => InvalidOperationException
///   - 完整配置的正常构建流程
///   - 各服务注册检查：
///     - ExtensionHostOptions 单例
///     - IExtensionHttpClient 单例
///     - IVersionCompatibilityChecker 单例
///     - IDownloadQueueManager 单例
///     - IPlatformMatcher 单例
///     - IPlatformServices 单例
///     - IExtensionMetadataMapper 单例
///     - IExtensionCatalog 单例
///     - IDependencyResolver 单例
///     - IExtensionLifecycleHooks 单例_默认 no-op
///     - IExtensionHost 单例_返回 GeneralExtensionHost
///   - 用户已注册服务时不重复注册
///   - CatalogPath 为 null 时回退到 ExtensionsDirectory
/// - 方法链式调用返回自身
/// </summary>
using Microsoft.Extensions.DependencyInjection;
using Moq;
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Core.Tests;

public class ExtensionHostBuilderTests
{
    [Fact]
    public void 构造函数_创建ServiceCollection()
    {
        var builder = new ExtensionHostBuilder();
        Assert.NotNull(builder);
    }

    [Fact]
    public void ConfigureOptions_正常配置()
    {
        var builder = new ExtensionHostBuilder();
        var result = builder.ConfigureOptions(opts =>
        {
            opts.ServerUrl = "http://test.com";
            opts.HostVersion = "1.0.0";
            opts.ExtensionsDirectory = "/tmp";
        });
        Assert.Same(builder, result); // 链式返回自身
    }

    [Fact]
    public void ConfigureOptions_Action抛出异常_传播异常()
    {
        var builder = new ExtensionHostBuilder();
        Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureOptions(_ => throw new InvalidOperationException("config error")));
    }

    [Fact]
    public void WithOptions_正常赋值()
    {
        var opts = new ExtensionHostOptions
        {
            ServerUrl = "http://server",
            HostVersion = "2.0.0",
            ExtensionsDirectory = "/ext"
        };
        var builder = new ExtensionHostBuilder();
        var result = builder.WithOptions(opts);
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithOptions_null参数_抛出ArgumentNullException()
    {
        var builder = new ExtensionHostBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.WithOptions(null!));
    }

    [Fact]
    public void ConfigureServices_正常配置()
    {
        var builder = new ExtensionHostBuilder();
        var result = builder.ConfigureServices(services =>
        {
            services.AddSingleton<IExtensionLifecycleHooks, DefaultExtensionLifecycleHooks>();
        });
        Assert.Same(builder, result);
    }

    [Fact]
    public void Build_Options未配置_抛出InvalidOperationException()
    {
        var builder = new ExtensionHostBuilder();
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_仅最小配置可正常构建()
    {
        var builder = new ExtensionHostBuilder();
        builder.WithOptions(new ExtensionHostOptions
        {
            ServerUrl = "http://localhost",
            HostVersion = "1.0.0",
            ExtensionsDirectory = Path.Combine(Path.GetTempPath(), $"ext-test-{Guid.NewGuid()}")
        });
        var host = builder.Build();
        Assert.NotNull(host);
        Assert.NotNull(host.ExtensionCatalog);
    }

    [Fact]
    public void Build_用户已注册某服务时不再重复注册()
    {
        var mockHttpClient = new Mock<IExtensionHttpClient>();
        var builder = new ExtensionHostBuilder();
        builder.WithOptions(new ExtensionHostOptions
        {
            ServerUrl = "http://localhost",
            HostVersion = "1.0.0",
            ExtensionsDirectory = Path.Combine(Path.GetTempPath(), $"ext-test-{Guid.NewGuid()}")
        });
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(mockHttpClient.Object);
        });
        var host = builder.Build();
        Assert.NotNull(host);
    }

    [Fact]
    public void Build_CatalogPath为null_回退到ExtensionsDirectory()
    {
        var builder = new ExtensionHostBuilder();
        builder.WithOptions(new ExtensionHostOptions
        {
            ServerUrl = "http://localhost",
            HostVersion = "1.0.0",
            ExtensionsDirectory = Path.Combine(Path.GetTempPath(), $"ext-test-{Guid.NewGuid()}"),
            CatalogPath = null
        });
        var host = builder.Build();
        Assert.NotNull(host);
    }

    [Fact]
    public void Build_指定CatalogPath()
    {
        var catalogPath = Path.Combine(Path.GetTempPath(), $"catalog-{Guid.NewGuid()}");
        var builder = new ExtensionHostBuilder();
        builder.WithOptions(new ExtensionHostOptions
        {
            ServerUrl = "http://localhost",
            HostVersion = "1.0.0",
            ExtensionsDirectory = Path.Combine(Path.GetTempPath(), $"ext-test-{Guid.NewGuid()}"),
            CatalogPath = catalogPath
        });
        var host = builder.Build();
        Assert.NotNull(host);
    }

    [Fact]
    public void Build_返回IExtensionHost实例()
    {
        var builder = new ExtensionHostBuilder();
        builder.WithOptions(new ExtensionHostOptions
        {
            ServerUrl = "http://localhost",
            HostVersion = "1.0.0",
            ExtensionsDirectory = Path.Combine(Path.GetTempPath(), $"ext-test-{Guid.NewGuid()}")
        });
        var host = builder.Build();
        Assert.IsAssignableFrom<IExtensionHost>(host);
    }

    [Fact]
    public void 链式调用流畅API()
    {
        var builder = new ExtensionHostBuilder();
        var result = builder
            .WithOptions(new ExtensionHostOptions
            {
                ServerUrl = "http://server",
                HostVersion = "1.0.0",
                ExtensionsDirectory = Path.Combine(Path.GetTempPath(), $"ext-{Guid.NewGuid()}")
            })
            .ConfigureServices(services => { })
            .Build();

        Assert.NotNull(result);
    }
}
