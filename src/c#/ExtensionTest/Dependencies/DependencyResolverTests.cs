/// <summary>
/// 测试覆盖点：
/// - ResolveDependencies(extension)
///   - extension.Id 不在已安装列表中 => 返回仅含 [extension.Id]
///   - 无依赖 => 返回 [extension.Id]
///   - 单层依赖 => 返回 [dep, extension.Id]_拓扑序：先依赖后自身
///   - 多层依赖链 => 正确的拓扑序
///   - 循环依赖检测 => 抛出 InvalidOperationException
///   - 多子依赖共享依赖 => 依赖只出现一次
/// - GetMissingDependencies(extension)
///   - 全部依赖已安装 => 返回空列表
///   - 部分依赖缺失 => 返回缺失ID列表
///   - 全部依赖缺失 => 返回全部依赖ID列表
///   - 无依赖 => 返回空列表
/// </summary>
using Moq;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Dependencies.Tests;

public class DependencyResolverTests
{
    private Mock<IExtensionCatalog> CreateCatalogMock(Dictionary<string, ExtensionMetadata> installed)
    {
        var mock = new Mock<IExtensionCatalog>();
        mock.Setup(m => m.GetInstalledExtensionById(It.IsAny<string>()))
            .Returns<string>(id => installed.TryGetValue(id, out var ext) ? ext : null);
        return mock;
    }

    // ===== ResolveDependencies 测试 =====

    [Fact]
    public void ResolveDependencies_扩展不在已安装目录中_返回仅含自身()
    {
        var catalogMock = CreateCatalogMock(new Dictionary<string, ExtensionMetadata>());
        // extension.Id="ext-a"，但目录中没有，所以递归直接跳过
        var ext = new ExtensionMetadata { Id = "ext-a" };
        var resolver = new DependencyResolver(catalogMock.Object);

        var result = resolver.ResolveDependencies(ext);
        Assert.Single(result);
        Assert.Equal("ext-a", result[0]);
    }

    [Fact]
    public void ResolveDependencies_无依赖扩展_返回仅含自身()
    {
        var meta = new ExtensionMetadata { Id = "ext-a" };
        var catalog = new Dictionary<string, ExtensionMetadata>
        {
            ["ext-a"] = meta
        };
        var catalogMock = CreateCatalogMock(catalog);
        var resolver = new DependencyResolver(catalogMock.Object);

        var result = resolver.ResolveDependencies(meta);
        Assert.Single(result);
        Assert.Equal("ext-a", result[0]);
    }

    [Fact]
    public void ResolveDependencies_单层依赖_依赖先于自身()
    {
        var dep = new ExtensionMetadata { Id = "dep-b" };
        var ext = new ExtensionMetadata { Id = "ext-a", Dependencies = "dep-b" };
        var catalog = new Dictionary<string, ExtensionMetadata>
        {
            ["ext-a"] = ext,
            ["dep-b"] = dep
        };
        var catalogMock = CreateCatalogMock(catalog);
        var resolver = new DependencyResolver(catalogMock.Object);

        var result = resolver.ResolveDependencies(ext);
        Assert.Equal(2, result.Count);
        Assert.Equal("dep-b", result[0]); // 依赖先于扩展
        Assert.Equal("ext-a", result[1]);
    }

    [Fact]
    public void ResolveDependencies_多层依赖链_正确拓扑序()
    {
        var depC = new ExtensionMetadata { Id = "dep-c" };
        var depB = new ExtensionMetadata { Id = "dep-b", Dependencies = "dep-c" };
        var extA = new ExtensionMetadata { Id = "ext-a", Dependencies = "dep-b" };
        var catalog = new Dictionary<string, ExtensionMetadata>
        {
            ["ext-a"] = extA,
            ["dep-b"] = depB,
            ["dep-c"] = depC
        };
        var catalogMock = CreateCatalogMock(catalog);
        var resolver = new DependencyResolver(catalogMock.Object);

        var result = resolver.ResolveDependencies(extA);
        Assert.Equal(3, result.Count);
        Assert.Equal("dep-c", result[0]);
        Assert.Equal("dep-b", result[1]);
        Assert.Equal("ext-a", result[2]);
    }

    [Fact]
    public void ResolveDependencies_多个直接依赖_全部解析()
    {
        var dep1 = new ExtensionMetadata { Id = "dep-1" };
        var dep2 = new ExtensionMetadata { Id = "dep-2" };
        var ext = new ExtensionMetadata { Id = "ext-x", Dependencies = "dep-1,dep-2" };
        var catalog = new Dictionary<string, ExtensionMetadata>
        {
            ["ext-x"] = ext,
            ["dep-1"] = dep1,
            ["dep-2"] = dep2
        };
        var catalogMock = CreateCatalogMock(catalog);
        var resolver = new DependencyResolver(catalogMock.Object);

        var result = resolver.ResolveDependencies(ext);
        Assert.Equal(3, result.Count);
        Assert.Contains("dep-1", result);
        Assert.Contains("dep-2", result);
        Assert.Equal("ext-x", result[^1]); // 自身在最后
    }

    [Fact]
    public void ResolveDependencies_共享依赖_只出现一次()
    {
        var sharedDep = new ExtensionMetadata { Id = "shared" };
        var depA = new ExtensionMetadata { Id = "dep-a", Dependencies = "shared" };
        var depB = new ExtensionMetadata { Id = "dep-b", Dependencies = "shared" };
        var ext = new ExtensionMetadata { Id = "root", Dependencies = "dep-a,dep-b" };
        var catalog = new Dictionary<string, ExtensionMetadata>
        {
            ["root"] = ext,
            ["dep-a"] = depA,
            ["dep-b"] = depB,
            ["shared"] = sharedDep
        };
        var catalogMock = CreateCatalogMock(catalog);
        var resolver = new DependencyResolver(catalogMock.Object);

        var result = resolver.ResolveDependencies(ext);
        Assert.Equal(4, result.Count);
        Assert.Single(result.Where(id => id == "shared"));
    }

    [Fact]
    public void ResolveDependencies_循环依赖_抛出InvalidOperationException()
    {
        var depB = new ExtensionMetadata { Id = "dep-b", Dependencies = "dep-a" };
        var depA = new ExtensionMetadata { Id = "dep-a", Dependencies = "dep-b" };
        var ext = new ExtensionMetadata { Id = "root", Dependencies = "dep-a" };
        var catalog = new Dictionary<string, ExtensionMetadata>
        {
            ["root"] = ext,
            ["dep-a"] = depA,
            ["dep-b"] = depB
        };
        var catalogMock = CreateCatalogMock(catalog);
        var resolver = new DependencyResolver(catalogMock.Object);

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.ResolveDependencies(ext));
        Assert.Contains("Circular dependency", ex.Message);
    }

    [Fact]
    public void ResolveDependencies_自循环依赖_抛出异常()
    {
        var ext = new ExtensionMetadata { Id = "self-loop", Dependencies = "self-loop" };
        var catalog = new Dictionary<string, ExtensionMetadata> { ["self-loop"] = ext };
        var catalogMock = CreateCatalogMock(catalog);
        var resolver = new DependencyResolver(catalogMock.Object);

        Assert.Throws<InvalidOperationException>(() => resolver.ResolveDependencies(ext));
    }

    // ===== GetMissingDependencies 测试 =====

    [Fact]
    public void GetMissingDependencies_全部已安装_返回空列表()
    {
        var dep = new ExtensionMetadata { Id = "dep-1" };
        var ext = new ExtensionMetadata { Id = "ext-a", Dependencies = "dep-1" };
        var catalog = new Dictionary<string, ExtensionMetadata>
        {
            ["ext-a"] = ext,
            ["dep-1"] = dep
        };
        var catalogMock = CreateCatalogMock(catalog);
        var resolver = new DependencyResolver(catalogMock.Object);

        var missing = resolver.GetMissingDependencies(ext);
        Assert.Empty(missing);
    }

    [Fact]
    public void GetMissingDependencies_部分缺失_返回缺失ID()
    {
        var ext = new ExtensionMetadata { Id = "ext-a", Dependencies = "missing-dep" };
        var catalog = new Dictionary<string, ExtensionMetadata> { ["ext-a"] = ext };
        var catalogMock = CreateCatalogMock(catalog);
        var resolver = new DependencyResolver(catalogMock.Object);

        var missing = resolver.GetMissingDependencies(ext);
        Assert.Single(missing);
        Assert.Equal("missing-dep", missing[0]);
    }

    [Fact]
    public void GetMissingDependencies_全部依赖缺失_返回全部依赖()
    {
        var depB = new ExtensionMetadata { Id = "dep-b" }; // dep-b 已安装，但 dep-b 依赖 dep-c 缺失
        // 不将 dep-b 加入 catalog，让它查询返回 null_但GetMissingDependencies调用ResolveDependencies时需要catalog数据
        var ext = new ExtensionMetadata { Id = "ext-a", Dependencies = "dep-b" };
        // dep-b 不在catalog中
        var catalog = new Dictionary<string, ExtensionMetadata> { ["ext-a"] = ext };
        var catalogMock = CreateCatalogMock(catalog);
        var resolver = new DependencyResolver(catalogMock.Object);

        var missing = resolver.GetMissingDependencies(ext);
        Assert.Single(missing);
        Assert.Equal("dep-b", missing[0]);
    }

    [Fact]
    public void GetMissingDependencies_无依赖_返回空列表()
    {
        var ext = new ExtensionMetadata { Id = "ext-a" };
        var catalogMock = CreateCatalogMock(new Dictionary<string, ExtensionMetadata>());
        var resolver = new DependencyResolver(catalogMock.Object);

        Assert.Empty(resolver.GetMissingDependencies(ext));
    }

    [Fact]
    public void ResolveDependencies_依赖在catalog中找不到_跳过该依赖()
    {
        var ext = new ExtensionMetadata { Id = "root", Dependencies = "unknown-dep" };
        var catalog = new Dictionary<string, ExtensionMetadata> { ["root"] = ext };
        var catalogMock = CreateCatalogMock(catalog);
        var resolver = new DependencyResolver(catalogMock.Object);

        var result = resolver.ResolveDependencies(ext);
        Assert.Single(result);
        Assert.Equal("root", result[0]);
    }
}
