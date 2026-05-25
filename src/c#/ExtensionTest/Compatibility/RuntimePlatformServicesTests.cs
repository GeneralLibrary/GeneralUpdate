/// <summary>
/// 测试覆盖点：
/// - GetCurrentPlatform() 运行时检测_实际为当前OS平台
/// - 无法直接mock静态RuntimeInformation，但可以验证返回值是有效枚举值
/// - 验证返回值不是 TargetPlatform.None_运行测试的环境必然是Windows/Linux/MacOS之一
/// </summary>
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Common.Enums;

namespace GeneralUpdate.Extension.Compatibility.Tests;

public class RuntimePlatformServicesTests
{
    [Fact]
    public void GetCurrentPlatform_返回有效的平台枚举值()
    {
        var services = new RuntimePlatformServices();
        var platform = services.GetCurrentPlatform();

        // 测试环境必然运行在某个平台上
        Assert.NotEqual(TargetPlatform.None, platform);
        Assert.True(
            platform == TargetPlatform.Windows ||
            platform == TargetPlatform.Linux ||
            platform == TargetPlatform.MacOS);
    }
}
