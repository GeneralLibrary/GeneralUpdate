/// <summary>
/// 测试覆盖点：
/// - 默认值：PageNumber=1, PageSize=10
/// - Status/BeginDate/EndDate/Platform/IsPreRelease 为 null
/// - Id/Name/Version 为 null
/// - 所有属性赋值和读取
/// - PageNumber/PageSize 边界
/// </summary>
namespace GeneralUpdate.Extension.Common.DTOs.Tests;

public class ExtensionQueryDTOTests
{
    [Fact]
    public void 默认构造_Id_Name_Version_为null()
    {
        var dto = new ExtensionQueryDTO();
        Assert.Null(dto.Id);
        Assert.Null(dto.Name);
        Assert.Null(dto.Version);
    }

    [Fact]
    public void 默认构造_PageNumber_为1_PageSize_为10()
    {
        var dto = new ExtensionQueryDTO();
        Assert.Equal(1, dto.PageNumber);
        Assert.Equal(10, dto.PageSize);
    }

    [Fact]
    public void 默认构造_Status_BeginDate_EndDate_为null()
    {
        var dto = new ExtensionQueryDTO();
        Assert.Null(dto.Status);
        Assert.Null(dto.BeginDate);
        Assert.Null(dto.EndDate);
    }

    [Fact]
    public void Status_三态赋值_null_true_false()
    {
        var dto = new ExtensionQueryDTO();
        Assert.Null(dto.Status);
        dto.Status = true;
        Assert.True(dto.Status);
        dto.Status = false;
        Assert.False(dto.Status);
    }

    [Fact]
    public void BeginDate_和EndDate_可赋值()
    {
        var begin = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 12, 31);
        var dto = new ExtensionQueryDTO { BeginDate = begin, EndDate = end };
        Assert.Equal(begin, dto.BeginDate);
        Assert.Equal(end, dto.EndDate);
    }

    [Fact]
    public void Publisher_Category_HostVersion_可赋值()
    {
        var dto = new ExtensionQueryDTO
        {
            Publisher = "test-pub",
            Category = "tools",
            HostVersion = "3.0.0"
        };
        Assert.Equal("test-pub", dto.Publisher);
        Assert.Equal("tools", dto.Category);
        Assert.Equal("3.0.0", dto.HostVersion);
    }

    [Fact]
    public void Platform_可赋值为枚举值()
    {
        var dto = new ExtensionQueryDTO { Platform = TargetPlatform.Windows };
        Assert.Equal(TargetPlatform.Windows, dto.Platform);

        dto.Platform = TargetPlatform.All;
        Assert.Equal(TargetPlatform.All, dto.Platform);
    }

    [Fact]
    public void IsPreRelease_三态赋值()
    {
        var dto = new ExtensionQueryDTO();
        Assert.Null(dto.IsPreRelease);
        dto.IsPreRelease = true;
        Assert.True(dto.IsPreRelease);
        dto.IsPreRelease = false;
        Assert.False(dto.IsPreRelease);
    }

    [Fact]
    public void PageNumber_可设为0()
    {
        var dto = new ExtensionQueryDTO { PageNumber = 0 };
        Assert.Equal(0, dto.PageNumber);
    }

    [Fact]
    public void PageSize_可设为大值()
    {
        var dto = new ExtensionQueryDTO { PageSize = int.MaxValue };
        Assert.Equal(int.MaxValue, dto.PageSize);
    }
}
