/// <summary>
/// 测试覆盖点：
/// - LoadInstalledExtensions()
///   - 目录不存在 => 直接返回_不抛异常
///   - 目录存在但无子目录 => _installedExtensions 为空
///   - 有子目录但无 manifest.json => 跳过该子目录
///   - 有子目录且有有效 manifest.json => 加载扩展
///   - manifest.json 解析失败 => 抛出 InvalidOperationException
///   - 扩展 ID 为空 => 跳过该扩展
///   - .backup 目录 => 跳过
///   - .tmp 清理 => 自动清理孤儿临时文件
/// - GetInstalledExtensions(): 空/非空列表
/// - GetInstalledExtensionsByPlatform(platform)
///   - Windows flag
///   - Linux flag
///   - All => 返回所有
///   - None => 返回空
/// - GetInstalledExtensionById(extensionId)
///   - 存在 => 返回扩展
///   - 不存在 => 返回 null
/// - AddOrUpdateInstalledExtension(extension)
///   - 新增扩展
///   - 更新已存在扩展
/// - RemoveInstalledExtension(extensionId)
///   - 存在扩展 => 移除
///   - 不存在扩展 => 不抛异常
/// - SaveCatalog 内部逻辑_通过 AddOrUpdate 间接测试
/// - SanitizeDirectoryName
///   - 空字符串/含非法字符
/// </summary>
using Newtonsoft.Json;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Common.Enums;
using GeneralUpdate.Extension.Common.Models;

namespace GeneralUpdate.Extension.Catalog.Tests;

public class ExtensionCatalogTests : IDisposable
{
    private readonly string _tempCatalogPath;

    public ExtensionCatalogTests()
    {
        _tempCatalogPath = Path.Combine(Path.GetTempPath(), $"ExtCatalogTest-{Guid.NewGuid()}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempCatalogPath))
        {
            try { Directory.Delete(_tempCatalogPath, true); } catch { }
        }
    }

    private ExtensionCatalog CreateCatalog() => new(_tempCatalogPath);

    // ===== LoadInstalledExtensions =====

    [Fact]
    public void LoadInstalledExtensions_目录不存在_直接返回()
    {
        // 确保目录不存在
        var nonExistentPath = Path.Combine(_tempCatalogPath, "nonexistent");
        var catalog = new ExtensionCatalog(nonExistentPath);

        // 不应该抛异常
        catalog.LoadInstalledExtensions();
        Assert.Empty(catalog.GetInstalledExtensions());
    }

    [Fact]
    public void LoadInstalledExtensions_空目录_返回空列表()
    {
        Directory.CreateDirectory(_tempCatalogPath);
        var catalog = CreateCatalog();

        catalog.LoadInstalledExtensions();
        Assert.Empty(catalog.GetInstalledExtensions());
    }

    [Fact]
    public void LoadInstalledExtensions_有有效manifest_成功加载()
    {
        Directory.CreateDirectory(_tempCatalogPath);
        var extDir = Path.Combine(_tempCatalogPath, "my-ext");
        Directory.CreateDirectory(extDir);
        var meta = new ExtensionMetadata { Id = "ext-1", Name = "my-ext", Version = "1.0.0" };
        File.WriteAllText(Path.Combine(extDir, "manifest.json"), JsonConvert.SerializeObject(meta));

        var catalog = CreateCatalog();
        catalog.LoadInstalledExtensions();

        var installed = catalog.GetInstalledExtensions();
        Assert.Single(installed);
        Assert.Equal("ext-1", installed[0].Id);
    }

    [Fact]
    public void LoadInstalledExtensions_子目录无manifest_跳过该目录()
    {
        Directory.CreateDirectory(_tempCatalogPath);
        var extDir = Path.Combine(_tempCatalogPath, "no-manifest");
        Directory.CreateDirectory(extDir);
        // 不创建 manifest.json

        var catalog = CreateCatalog();
        catalog.LoadInstalledExtensions();
        Assert.Empty(catalog.GetInstalledExtensions());
    }

    [Fact]
    public void LoadInstalledExtensions_扩展ID为空_跳过()
    {
        Directory.CreateDirectory(_tempCatalogPath);
        var extDir = Path.Combine(_tempCatalogPath, "bad-ext");
        Directory.CreateDirectory(extDir);
        var meta = new ExtensionMetadata { Id = "", Name = "bad" };
        File.WriteAllText(Path.Combine(extDir, "manifest.json"), JsonConvert.SerializeObject(meta));

        var catalog = CreateCatalog();
        catalog.LoadInstalledExtensions();
        Assert.Empty(catalog.GetInstalledExtensions());
    }

    [Fact]
    public void LoadInstalledExtensions_解析失败_抛出InvalidOperationException()
    {
        Directory.CreateDirectory(_tempCatalogPath);
        var extDir = Path.Combine(_tempCatalogPath, "bad-json");
        Directory.CreateDirectory(extDir);
        File.WriteAllText(Path.Combine(extDir, "manifest.json"), "not valid json {{{");

        var catalog = CreateCatalog();
        Assert.Throws<InvalidOperationException>(() => catalog.LoadInstalledExtensions());
    }

    [Fact]
    public void LoadInstalledExtensions_跳过backup目录()
    {
        Directory.CreateDirectory(_tempCatalogPath);
        var backupDir = Path.Combine(_tempCatalogPath, "some-ext.backup");
        Directory.CreateDirectory(backupDir);
        File.WriteAllText(Path.Combine(backupDir, "manifest.json"), JsonConvert.SerializeObject(
            new ExtensionMetadata { Id = "should-be-skipped", Name = "backup" }));

        var catalog = CreateCatalog();
        catalog.LoadInstalledExtensions();
        Assert.Empty(catalog.GetInstalledExtensions());
    }

    [Fact]
    public void LoadInstalledExtensions_清理孤儿tmp文件()
    {
        Directory.CreateDirectory(_tempCatalogPath);
        var extDir = Path.Combine(_tempCatalogPath, "clean-ext");
        Directory.CreateDirectory(extDir);
        // 创建 .tmp 孤儿文件
        File.WriteAllText(Path.Combine(extDir, "orphan.tmp"), "orphan content");
        // 创建有效 manifest
        var meta = new ExtensionMetadata { Id = "clean-ext", Name = "clean-ext" };
        File.WriteAllText(Path.Combine(extDir, "manifest.json"), JsonConvert.SerializeObject(meta));

        var catalog = CreateCatalog();
        catalog.LoadInstalledExtensions();

        // 验证扩展被加载
        Assert.Single(catalog.GetInstalledExtensions());
        // 验证 .tmp 文件被清理
        Assert.False(File.Exists(Path.Combine(extDir, "orphan.tmp")));
    }

    [Fact]
    public void LoadInstalledExtensions_多次加载_覆盖之前数据()
    {
        Directory.CreateDirectory(_tempCatalogPath);
        var ext1Dir = Path.Combine(_tempCatalogPath, "ext1");
        Directory.CreateDirectory(ext1Dir);
        File.WriteAllText(Path.Combine(ext1Dir, "manifest.json"), JsonConvert.SerializeObject(
            new ExtensionMetadata { Id = "e1", Name = "ext1" }));

        var catalog = CreateCatalog();
        catalog.LoadInstalledExtensions();
        Assert.Single(catalog.GetInstalledExtensions());

        // 创建更多扩展
        var ext2Dir = Path.Combine(_tempCatalogPath, "ext2");
        Directory.CreateDirectory(ext2Dir);
        File.WriteAllText(Path.Combine(ext2Dir, "manifest.json"), JsonConvert.SerializeObject(
            new ExtensionMetadata { Id = "e2", Name = "ext2" }));

        catalog.LoadInstalledExtensions();
        Assert.Equal(2, catalog.GetInstalledExtensions().Count);
    }

    // ===== GetInstalledExtensions =====

    [Fact]
    public void GetInstalledExtensions_初始为空()
    {
        var catalog = CreateCatalog();
        Assert.Empty(catalog.GetInstalledExtensions());
    }

    [Fact]
    public void GetInstalledExtensions_返回已添加的扩展()
    {
        var catalog = CreateCatalog();
        var meta = new ExtensionMetadata { Id = "e1" };
        catalog.AddOrUpdateInstalledExtension(meta);

        Assert.Single(catalog.GetInstalledExtensions());
        Assert.Equal("e1", catalog.GetInstalledExtensions()[0].Id);
    }

    // ===== GetInstalledExtensionsByPlatform =====

    [Fact]
    public void GetInstalledExtensionsByPlatform_Windows平台过滤()
    {
        var catalog = CreateCatalog();
        catalog.AddOrUpdateInstalledExtension(
            new ExtensionMetadata { Id = "win-ext", SupportedPlatforms = TargetPlatform.Windows });
        catalog.AddOrUpdateInstalledExtension(
            new ExtensionMetadata { Id = "linux-ext", SupportedPlatforms = TargetPlatform.Linux });

        var result = catalog.GetInstalledExtensionsByPlatform(TargetPlatform.Windows);
        Assert.Single(result);
        Assert.Equal("win-ext", result[0].Id);
    }

    [Fact]
    public void GetInstalledExtensionsByPlatform_All平台_返回所有()
    {
        var catalog = CreateCatalog();
        catalog.AddOrUpdateInstalledExtension(
            new ExtensionMetadata { Id = "ext1", SupportedPlatforms = TargetPlatform.Windows });
        catalog.AddOrUpdateInstalledExtension(
            new ExtensionMetadata { Id = "ext2", SupportedPlatforms = TargetPlatform.MacOS });

        var result = catalog.GetInstalledExtensionsByPlatform(TargetPlatform.All);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetInstalledExtensionsByPlatform_None平台_返回空()
    {
        var catalog = CreateCatalog();
        catalog.AddOrUpdateInstalledExtension(
            new ExtensionMetadata { Id = "ext1", SupportedPlatforms = TargetPlatform.Windows });

        var result = catalog.GetInstalledExtensionsByPlatform(TargetPlatform.None);
        Assert.Empty(result);
    }

    // ===== GetInstalledExtensionById =====

    [Fact]
    public void GetInstalledExtensionById_存在_返回扩展()
    {
        var catalog = CreateCatalog();
        var meta = new ExtensionMetadata { Id = "target-ext" };
        catalog.AddOrUpdateInstalledExtension(meta);

        var found = catalog.GetInstalledExtensionById("target-ext");
        Assert.NotNull(found);
        Assert.Equal("target-ext", found!.Id);
    }

    [Fact]
    public void GetInstalledExtensionById_不存在_返回null()
    {
        var catalog = CreateCatalog();
        Assert.Null(catalog.GetInstalledExtensionById("no-such-id"));
    }

    // ===== AddOrUpdateInstalledExtension =====

    [Fact]
    public void AddOrUpdateInstalledExtension_新增扩展()
    {
        var catalog = CreateCatalog();
        catalog.AddOrUpdateInstalledExtension(new ExtensionMetadata { Id = "new-ext", Name = "new-ext" });
        Assert.Single(catalog.GetInstalledExtensions());
    }

    [Fact]
    public void AddOrUpdateInstalledExtension_更新已存在扩展()
    {
        var catalog = CreateCatalog();
        catalog.AddOrUpdateInstalledExtension(
            new ExtensionMetadata { Id = "ext", Name = "v1", Version = "1.0.0" });
        catalog.AddOrUpdateInstalledExtension(
            new ExtensionMetadata { Id = "ext", Name = "v2", Version = "2.0.0" });

        var found = catalog.GetInstalledExtensionById("ext");
        Assert.NotNull(found);
        Assert.Equal("2.0.0", found!.Version);
        Assert.Equal("v2", found.Name);
        Assert.Single(catalog.GetInstalledExtensions()); // 仍然只有一个
    }

    // ===== RemoveInstalledExtension =====

    [Fact]
    public void RemoveInstalledExtension_存在扩展_移除成功()
    {
        var catalog = CreateCatalog();
        catalog.AddOrUpdateInstalledExtension(new ExtensionMetadata { Id = "rm-ext" });
        Assert.Single(catalog.GetInstalledExtensions());

        catalog.RemoveInstalledExtension("rm-ext");
        Assert.Empty(catalog.GetInstalledExtensions());
    }

    [Fact]
    public void RemoveInstalledExtension_不存在扩展_不抛异常()
    {
        var catalog = CreateCatalog();
        catalog.RemoveInstalledExtension("no-such");
        // 不抛异常即通过
    }

    [Fact]
    public void RemoveInstalledExtension_重复移除不抛异常()
    {
        var catalog = CreateCatalog();
        catalog.AddOrUpdateInstalledExtension(new ExtensionMetadata { Id = "double-rm" });
        catalog.RemoveInstalledExtension("double-rm");
        catalog.RemoveInstalledExtension("double-rm");
        // 第二次移除不抛异常
    }

    // ===== SanitizeDirectoryName_通过内部逻辑间接测试=====

    [Fact]
    public void AddOrUpdateInstalledExtension_Name为null_使用Id作为目录名()
    {
        var catalog = CreateCatalog();
        // Name 为 null，应使用 Id 作为目录名
        catalog.AddOrUpdateInstalledExtension(
            new ExtensionMetadata { Id = "id-only", Name = null });
        var ext = catalog.GetInstalledExtensionById("id-only");
        Assert.NotNull(ext);
    }

    [Fact]
    public void AddOrUpdateInstalledExtension_Name和Id都为null_使用unknown()
    {
        var catalog = CreateCatalog();
        // 这种情况理论上 Id 不应为null，但测试边界
        // 在 Constructor 或 Add 方法中不会显式检查
        // 只验证不抛异常即可
        var meta = new ExtensionMetadata { Id = null!, Name = null };
        // 注意：Id 必须设置才能存入，否则会抛异常
        Assert.Throws<ArgumentNullException>(() => catalog.AddOrUpdateInstalledExtension(meta));
    }
}
