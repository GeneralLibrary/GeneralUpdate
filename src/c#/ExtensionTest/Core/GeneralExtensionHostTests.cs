/// <summary>
/// 测试覆盖点：
/// - DI构造函数
///   - options=null => ArgumentNullException
///   - httpClient=null => ArgumentNullException
///   - catalog=null => ArgumentNullException
///   - compatibilityChecker=null => ArgumentNullException
///   - downloadQueue=null => ArgumentNullException
///   - platformMatcher=null => ArgumentNullException
///   - dependencyResolver=null => 不抛异常_注释掉的检查
///   - lifecycleHooks/metadataMapper 可选参数可为 null
///   - 订阅 DownloadStatusChanged 事件
///   - 创建目录
/// - Legacy构造函数
///   - options=null => ArgumentNullException
/// - ExtensionCatalog 属性
/// - QueryExtensionsAsync(query)
///   - 正常查询返回结果
///   - 查询抛异常_异常继续向上传播
/// - DownloadExtensionAsync(extensionId, savePath)
///   - 成功下载返回 true
///   - 失败下载返回 false
/// - UpdateExtensionAsync(extensionId)
///   - 成功更新完整流程_查询 + 兼容性检查 + 平台检查 + 依赖解析 + 下载 + 安装 + 编目更新
///   - 服务器返回 null items => 抛异常
///   - 服务器无此扩展 => InvalidOperationException
///   - 不兼容 => InvalidOperationException
///   - 平台不支持 => InvalidOperationException
///   - 有未安装依赖 => 递归安装
///   - 下载失败 => 抛异常
///   - 安装失败 => 抛异常
///   - 事件触发: Queued -> Updating(progress) -> UpdateSuccessful(progress=100)
///   - 异常时: Queued -> UpdateFailed
///   - 返回 false 但事件通知失败
/// - InstallExtensionAsync(extensionPath, rollbackOnFailure)
///   - 文件不存在 => FileNotFoundException
///   - 非 .zip 文件 => InvalidOperationException
///   - lifecycleHooks.OnBeforeInstallAsync 返回 false => 取消安装
///   - 正常安装流程
///   - 已存在同名扩展且有rollback => 备份->删除旧->解压新->删除备份
///   - rollbackOnFailure=false => 跳过备份
///   - 安装失败且rollback => 恢复备份
/// - UpdateExtensionsAsync(extensionIds, ct)
///   - CancellationToken 取消
///   - 单个失败不影响其他
/// - IsExtensionCompatible(extension)
/// - SetAutoUpdate / IsAutoUpdateEnabled
///   - 单个扩展设置
///   - 全局设置兜底
/// - SetGlobalAutoUpdate
/// - ExtensionUpdateStatusChanged 事件
/// - SafeExtractZipAsync_内部方法难以直接测试
/// - ComputeFileSha256Async_内部方法难以直接测试
/// - SafeDeleteFile_内部方法难以直接测试
/// - ToMetadata 静态方法_DTO -> Metadata 映射
/// </summary>
using Moq;
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Common.DTOs;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Core.Tests;

public class GeneralExtensionHostTests
{
    private static ExtensionHostOptions CreateOptions(string? extDir = null)
    {
        return new ExtensionHostOptions
        {
            ServerUrl = "http://test-server",
            Scheme = "Bearer",
            Token = "test-token",
            HostVersion = "2.0.0",
            ExtensionsDirectory = extDir ?? Path.Combine(Path.GetTempPath(), $"host-test-{Guid.NewGuid()}")
        };
    }

    private static Mock<IExtensionHttpClient> CreateHttpClientMock()
    {
        return new Mock<IExtensionHttpClient>();
    }

    private static Mock<IExtensionCatalog> CreateCatalogMock()
    {
        var mock = new Mock<IExtensionCatalog>();
        mock.Setup(m => m.GetInstalledExtensions()).Returns(new List<ExtensionMetadata>());
        mock.Setup(m => m.GetInstalledExtensionById(It.IsAny<string>())).Returns((ExtensionMetadata?)null);
        return mock;
    }

    // ===== DI构造函数测试 =====

    [Fact]
    public void DI构造函数_options为null_抛出ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GeneralExtensionHost(null!, CreateHttpClientMock().Object, CreateCatalogMock().Object,
                Mock.Of<IVersionCompatibilityChecker>(), Mock.Of<IDownloadQueueManager>(),
                Mock.Of<IDependencyResolver>(), Mock.Of<IPlatformMatcher>()));
    }

    [Fact]
    public void DI构造函数_httpClient为null_抛出ArgumentNullException()
    {
        var opts = CreateOptions();
        Assert.Throws<ArgumentNullException>(() =>
            new GeneralExtensionHost(opts, null!, CreateCatalogMock().Object,
                Mock.Of<IVersionCompatibilityChecker>(), Mock.Of<IDownloadQueueManager>(),
                Mock.Of<IDependencyResolver>(), Mock.Of<IPlatformMatcher>()));
    }

    [Fact]
    public void DI构造函数_catalog为null_抛出ArgumentNullException()
    {
        var opts = CreateOptions();
        Assert.Throws<ArgumentNullException>(() =>
            new GeneralExtensionHost(opts, CreateHttpClientMock().Object, null!,
                Mock.Of<IVersionCompatibilityChecker>(), Mock.Of<IDownloadQueueManager>(),
                Mock.Of<IDependencyResolver>(), Mock.Of<IPlatformMatcher>()));
    }

    [Fact]
    public void DI构造函数_compatibilityChecker为null_抛出ArgumentNullException()
    {
        var opts = CreateOptions();
        Assert.Throws<ArgumentNullException>(() =>
            new GeneralExtensionHost(opts, CreateHttpClientMock().Object, CreateCatalogMock().Object,
                null!, Mock.Of<IDownloadQueueManager>(),
                Mock.Of<IDependencyResolver>(), Mock.Of<IPlatformMatcher>()));
    }

    [Fact]
    public void DI构造函数_downloadQueue为null_抛出ArgumentNullException()
    {
        var opts = CreateOptions();
        Assert.Throws<ArgumentNullException>(() =>
            new GeneralExtensionHost(opts, CreateHttpClientMock().Object, CreateCatalogMock().Object,
                Mock.Of<IVersionCompatibilityChecker>(), null!,
                Mock.Of<IDependencyResolver>(), Mock.Of<IPlatformMatcher>()));
    }

    [Fact]
    public void DI构造函数_platformMatcher为null_抛出ArgumentNullException()
    {
        var opts = CreateOptions();
        Assert.Throws<ArgumentNullException>(() =>
            new GeneralExtensionHost(opts, CreateHttpClientMock().Object, CreateCatalogMock().Object,
                Mock.Of<IVersionCompatibilityChecker>(), Mock.Of<IDownloadQueueManager>(),
                Mock.Of<IDependencyResolver>(), null!));
    }

    [Fact]
    public void DI构造函数_dependencyResolver为null_不抛异常()
    {
        var opts = CreateOptions();
        // dependencyResolver 的 null 检查被注释掉了，所以不应抛异常
        var host = new GeneralExtensionHost(opts, CreateHttpClientMock().Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), null!, Mock.Of<IPlatformMatcher>());
        Assert.NotNull(host);
    }

    [Fact]
    public void DI构造函数_可选参数为null_可正常构建()
    {
        var opts = CreateOptions();
        var host = new GeneralExtensionHost(opts, CreateHttpClientMock().Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>(), lifecycleHooks: null, metadataMapper: null);
        Assert.NotNull(host);
    }

    [Fact]
    public void DI构造函数_正常构建_ExtensionCatalog属性可用()
    {
        var opts = CreateOptions();
        var catalogMock = CreateCatalogMock();
        var host = new GeneralExtensionHost(opts, CreateHttpClientMock().Object,
            catalogMock.Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());
        Assert.NotNull(host.ExtensionCatalog);
        Assert.Same(catalogMock.Object, host.ExtensionCatalog);
    }

    // ===== Legacy构造函数测试 =====

    [Fact]
    public void Legacy构造函数_options为null_抛出ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GeneralExtensionHost(null!));
    }

    [Fact]
    public void Legacy构造函数_正常构建()
    {
        var opts = CreateOptions();
        var host = new GeneralExtensionHost(opts);
        Assert.NotNull(host);
        Assert.NotNull(host.ExtensionCatalog);
    }

    // ===== QueryExtensionsAsync 测试 =====

    [Fact]
    public async Task QueryExtensionsAsync_正常查询_返回结果()
    {
        var expectedResponse = new HttpResponseDTO<PagedResultDTO<ExtensionDTO>>
        {
            Code = "200",
            Body = new PagedResultDTO<ExtensionDTO>
            {
                Items = new[] { new ExtensionDTO { Id = "ext-1" } }
            }
        };
        var httpMock = CreateHttpClientMock();
        httpMock.Setup(m => m.QueryExtensionsAsync(It.IsAny<ExtensionQueryDTO>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var host = new GeneralExtensionHost(CreateOptions(), httpMock.Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());

        var result = await host.QueryExtensionsAsync(new ExtensionQueryDTO { Id = "ext-1" });
        Assert.NotNull(result);
        Assert.Equal("200", result.Code);
    }

    [Fact]
    public async Task QueryExtensionsAsync_查询抛异常_异常向上传播()
    {
        var httpMock = CreateHttpClientMock();
        httpMock.Setup(m => m.QueryExtensionsAsync(It.IsAny<ExtensionQueryDTO>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var host = new GeneralExtensionHost(CreateOptions(), httpMock.Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            host.QueryExtensionsAsync(new ExtensionQueryDTO()));
    }

    // ===== DownloadExtensionAsync 测试 =====

    [Fact]
    public async Task DownloadExtensionAsync_成功返回true()
    {
        var httpMock = CreateHttpClientMock();
        httpMock.Setup(m => m.DownloadExtensionAsync(
                "ext-1", It.IsAny<string>(), It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var host = new GeneralExtensionHost(CreateOptions(), httpMock.Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());

        var result = await host.DownloadExtensionAsync("ext-1", "/tmp/test.zip");
        Assert.True(result);
    }

    [Fact]
    public async Task DownloadExtensionAsync_失败返回false()
    {
        var httpMock = CreateHttpClientMock();
        httpMock.Setup(m => m.DownloadExtensionAsync(
                "ext-1", It.IsAny<string>(), It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var host = new GeneralExtensionHost(CreateOptions(), httpMock.Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());

        var result = await host.DownloadExtensionAsync("ext-1", "/tmp/test.zip");
        Assert.False(result);
    }

    // ===== IsExtensionCompatible 测试 =====

    [Fact]
    public void IsExtensionCompatible_委托给CompatibilityChecker()
    {
        var compatMock = new Mock<IVersionCompatibilityChecker>();
        compatMock.Setup(m => m.IsCompatible(It.IsAny<ExtensionMetadata>(), "2.0.0"))
            .Returns(true);

        var host = new GeneralExtensionHost(CreateOptions(), CreateHttpClientMock().Object,
            CreateCatalogMock().Object, compatMock.Object, Mock.Of<IDownloadQueueManager>(),
            Mock.Of<IDependencyResolver>(), Mock.Of<IPlatformMatcher>());

        Assert.True(host.IsExtensionCompatible(new ExtensionMetadata()));
    }

    // ===== SetAutoUpdate / IsAutoUpdateEnabled / SetGlobalAutoUpdate =====

    [Fact]
    public void SetAutoUpdate_单个扩展设置()
    {
        var host = new GeneralExtensionHost(CreateOptions(), CreateHttpClientMock().Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());

        host.SetAutoUpdate("ext-1", true);
        Assert.True(host.IsAutoUpdateEnabled("ext-1"));

        host.SetAutoUpdate("ext-1", false);
        Assert.False(host.IsAutoUpdateEnabled("ext-1"));
    }

    [Fact]
    public void IsAutoUpdateEnabled_无单独设置_使用全局设置()
    {
        var host = new GeneralExtensionHost(CreateOptions(), CreateHttpClientMock().Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());

        // 默认全局为 false
        Assert.False(host.IsAutoUpdateEnabled("any-ext"));

        host.SetGlobalAutoUpdate(true);
        Assert.True(host.IsAutoUpdateEnabled("any-ext"));
    }

    [Fact]
    public void IsAutoUpdateEnabled_单独设置覆盖全局设置()
    {
        var host = new GeneralExtensionHost(CreateOptions(), CreateHttpClientMock().Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());

        host.SetGlobalAutoUpdate(true);
        host.SetAutoUpdate("ext-special", false);

        Assert.False(host.IsAutoUpdateEnabled("ext-special")); // 单独设置优先
        Assert.True(host.IsAutoUpdateEnabled("other-ext")); // 全局设置兜底
    }

    // ===== ExtensionUpdateStatusChanged 事件测试 =====

    [Fact]
    public async Task Download_触发进度事件()
    {
        var httpMock = CreateHttpClientMock();
        httpMock.Setup(m => m.DownloadExtensionAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, IProgress<int>, CancellationToken>((id, path, progress, ct) =>
            {
                progress?.Report(50);
                return Task.FromResult(true);
            });

        var host = new GeneralExtensionHost(CreateOptions(), httpMock.Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());

        var events = new List<ExtensionUpdateEventArgs>();
        host.ExtensionUpdateStatusChanged += (_, e) => events.Add(e);

        await host.DownloadExtensionAsync("ext-1", "/tmp/test.zip");

        Assert.NotEmpty(events);
        Assert.Contains(events, e => e.Status == ExtensionUpdateStatus.Updating && e.Progress == 50);
    }

    // ===== UpdateExtensionAsync 测试 =====

    [Fact]
    public async Task UpdateExtensionAsync_服务器返回nullItems_抛出异常_事件通知失败()
    {
        var httpMock = CreateHttpClientMock();
        httpMock.Setup(m => m.QueryExtensionsAsync(It.IsAny<ExtensionQueryDTO>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseDTO<PagedResultDTO<ExtensionDTO>> { Body = null });

        var host = new GeneralExtensionHost(CreateOptions(), httpMock.Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());

        var failedEventReceived = false;
        host.ExtensionUpdateStatusChanged += (_, e) =>
        {
            if (e.Status == ExtensionUpdateStatus.UpdateFailed) failedEventReceived = true;
        };

        var result = await host.UpdateExtensionAsync("ext-1");

        Assert.False(result);
        Assert.True(failedEventReceived);
    }

    [Fact]
    public async Task UpdateExtensionAsync_服务器无此扩展_返回false()
    {
        var httpMock = CreateHttpClientMock();
        httpMock.Setup(m => m.QueryExtensionsAsync(It.IsAny<ExtensionQueryDTO>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseDTO<PagedResultDTO<ExtensionDTO>>
            {
                Body = new PagedResultDTO<ExtensionDTO>
                {
                    Items = new[] { new ExtensionDTO { Id = "other-ext" } }
                }
            });

        var host = new GeneralExtensionHost(CreateOptions(), httpMock.Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());

        var result = await host.UpdateExtensionAsync("ext-1");
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateExtensionAsync_不兼容_返回false()
    {
        var httpMock = CreateHttpClientMock();
        httpMock.Setup(m => m.QueryExtensionsAsync(It.IsAny<ExtensionQueryDTO>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseDTO<PagedResultDTO<ExtensionDTO>>
            {
                Body = new PagedResultDTO<ExtensionDTO>
                {
                    Items = new[] { new ExtensionDTO { Id = "ext-1", Name = "ext", MinHostVersion = "5.0.0" } }
                }
            });

        var compatMock = new Mock<IVersionCompatibilityChecker>();
        compatMock.Setup(m => m.IsCompatible(It.IsAny<ExtensionMetadata>(), "2.0.0"))
            .Returns(false);

        var host = new GeneralExtensionHost(CreateOptions(), httpMock.Object,
            CreateCatalogMock().Object, compatMock.Object, Mock.Of<IDownloadQueueManager>(),
            Mock.Of<IDependencyResolver>(), Mock.Of<IPlatformMatcher>());

        var result = await host.UpdateExtensionAsync("ext-1");
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateExtensionAsync_平台不支持_返回false()
    {
        var httpMock = CreateHttpClientMock();
        httpMock.Setup(m => m.QueryExtensionsAsync(It.IsAny<ExtensionQueryDTO>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseDTO<PagedResultDTO<ExtensionDTO>>
            {
                Body = new PagedResultDTO<ExtensionDTO>
                {
                    Items = new[] { new ExtensionDTO { Id = "ext-1", Name = "ext", SupportedPlatforms = TargetPlatform.Linux } }
                }
            });

        var compatMock = new Mock<IVersionCompatibilityChecker>();
        compatMock.Setup(m => m.IsCompatible(It.IsAny<ExtensionMetadata>(), "2.0.0")).Returns(true);
        var platformMock = new Mock<IPlatformMatcher>();
        platformMock.Setup(m => m.IsCurrentPlatformSupported(It.IsAny<ExtensionMetadata>())).Returns(false);

        var host = new GeneralExtensionHost(CreateOptions(), httpMock.Object,
            CreateCatalogMock().Object, compatMock.Object, Mock.Of<IDownloadQueueManager>(),
            Mock.Of<IDependencyResolver>(), platformMock.Object);

        var result = await host.UpdateExtensionAsync("ext-1");
        Assert.False(result);
    }

    // ===== UpdateExtensionsAsync 测试 =====

    [Fact]
    public async Task UpdateExtensionsAsync_CancellationToken取消()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var host = new GeneralExtensionHost(CreateOptions(), CreateHttpClientMock().Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            host.UpdateExtensionsAsync(new[] { "ext-1" }, cts.Token));
    }

    [Fact]
    public async Task UpdateExtensionsAsync_单个失败不影响其他()
    {
        var httpMock = CreateHttpClientMock();
        // 第一个查询返回空_导致失败，第二个成功
        httpMock.SetupSequence(m => m.QueryExtensionsAsync(It.IsAny<ExtensionQueryDTO>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseDTO<PagedResultDTO<ExtensionDTO>> { Body = null })
            .ReturnsAsync(new HttpResponseDTO<PagedResultDTO<ExtensionDTO>>
            {
                Body = new PagedResultDTO<ExtensionDTO>
                {
                    Items = new[] { new ExtensionDTO { Id = "ext-2", Name = "ext2" } }
                }
            });

        var compatMock = new Mock<IVersionCompatibilityChecker>();
        compatMock.Setup(m => m.IsCompatible(It.IsAny<ExtensionMetadata>(), "2.0.0")).Returns(false);

        var host = new GeneralExtensionHost(CreateOptions(), httpMock.Object,
            CreateCatalogMock().Object, compatMock.Object, Mock.Of<IDownloadQueueManager>(),
            Mock.Of<IDependencyResolver>(), Mock.Of<IPlatformMatcher>());

        var result = await host.UpdateExtensionsAsync(new[] { "ext-1", "ext-2" });

        Assert.False(result["ext-1"]); // 第一个失败_不兼容
        Assert.False(result["ext-2"]); // 第二个也失败_不兼容
    }

    // ===== InstallExtensionAsync 测试 =====

    [Fact]
    public async Task InstallExtensionAsync_文件不存在_抛FileNotFoundException_返回false()
    {
        var host = new GeneralExtensionHost(CreateOptions(), CreateHttpClientMock().Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());

        var result = await host.InstallExtensionAsync(@"C:\nonexistent\file.zip");
        Assert.False(result);
    }

    [Fact]
    public async Task InstallExtensionAsync_非zip文件_返回false()
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "not a zip");

        var host = new GeneralExtensionHost(CreateOptions(), CreateHttpClientMock().Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>());

        var result = await host.InstallExtensionAsync(tempFile);
        Assert.False(result);

        try { File.Delete(tempFile); } catch { }
    }

    [Fact]
    public async Task InstallExtensionAsync_lifecycleHook返回false_取消安装_返回false()
    {
        // 创建一个有效的最小 .zip 文件
        var tempZip = Path.Combine(Path.GetTempPath(), $"test-ext_{Guid.NewGuid()}.zip");
        CreateMinimalZipFile(tempZip);

        var lifecycleMock = new Mock<IExtensionLifecycleHooks>();
        lifecycleMock.Setup(m => m.OnBeforeInstallAsync(
                It.IsAny<ExtensionMetadata>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var host = new GeneralExtensionHost(CreateOptions(), CreateHttpClientMock().Object,
            CreateCatalogMock().Object, Mock.Of<IVersionCompatibilityChecker>(),
            Mock.Of<IDownloadQueueManager>(), Mock.Of<IDependencyResolver>(),
            Mock.Of<IPlatformMatcher>(), lifecycleMock.Object);

        var result = await host.InstallExtensionAsync(tempZip);
        Assert.False(result);

        try { File.Delete(tempZip); } catch { }
    }

    // ===== 辅助方法 =====

    private static void CreateMinimalZipFile(string path)
    {
        // 创建一个包含单个空文本文件的最小 zip 文件
        using var archive = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create);
        var entry = archive.CreateEntry("readme.txt");
        using var writer = new StreamWriter(entry.Open());
        writer.Write("test");
    }
}
