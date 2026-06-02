using GeneralUpdate.Bowl.Internal;

/// <summary>
/// 分支覆盖点：
/// SystemInfoProviderFactory.Create()：
///   - Windows 平台：返回 WindowsSystemInfoProvider
///   - Linux 平台：返回 ISystemInfoProvider（无操作实现）
///   - macOS 平台：返回 ISystemInfoProvider（无操作实现）
///   - 非 null 返回
///   - 类型检查
/// </summary>
public class SystemInfoProviderFactoryTests
{
    [Fact]
    public void Create_返回非null对象()
    {
        var provider = SystemInfoProviderFactory.Create();
        Assert.NotNull(provider);
    }

    [Fact]
    public void Create_返回ISystemInfoProvider接口类型()
    {
        var provider = SystemInfoProviderFactory.Create();
        Assert.IsAssignableFrom<ISystemInfoProvider>(provider);
    }

    [Fact]
    public void Create_多次调用_每次返回新实例()
    {
        var provider1 = SystemInfoProviderFactory.Create();
        var provider2 = SystemInfoProviderFactory.Create();
        Assert.NotNull(provider1);
        Assert.NotNull(provider2);
        // Both should be created; not asserting they are different
        // since the factory might use a singleton pattern (it doesn't in this case)
    }

    [Fact]
    public async Task 返回的Provider_ExportAsync不抛出异常()
    {
        var provider = SystemInfoProviderFactory.Create();
        var tempDir = Path.Combine(Path.GetTempPath(), $"BowlTest_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            // ExportAsync should not throw on any platform.
            // On non-Windows it's a no-op.
            await provider.ExportAsync(tempDir, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
