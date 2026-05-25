using GeneralUpdate.Drivelution.Abstractions.Models;

namespace DrivelutionTest.Models;

/// <summary>
/// DriverInfo 测试
/// 分支覆盖点:
/// - 默认构造函数：所有属性应为其默认值
/// - 属性设置/获取：所有可写属性
/// - 空字符串、空集合、默认值边界
/// 触发条件：创建 DriverInfo 实例
/// 预期结果：属性值正确返回
/// </summary>
public class DriverInfoTests
{
    [Fact(DisplayName = "DriverInfo_默认构造函数_所有属性为默认值")]
    public void DriverInfo_DefaultConstructor_AllPropertiesHaveDefaultValues()
    {
        // Act
        var info = new DriverInfo();

        // Assert
        Assert.Equal(string.Empty, info.Name);
        Assert.Equal(string.Empty, info.Version);
        Assert.Equal(string.Empty, info.FilePath);
        Assert.Equal(string.Empty, info.TargetOS);
        Assert.Equal(string.Empty, info.Architecture);
        Assert.Equal(string.Empty, info.HardwareId);
        Assert.Equal(string.Empty, info.Hash);
        Assert.Equal("SHA256", info.HashAlgorithm);
        Assert.NotNull(info.TrustedPublishers);
        Assert.Empty(info.TrustedPublishers);
        Assert.Equal(string.Empty, info.Description);
        Assert.Equal(default, info.ReleaseDate);
        Assert.NotNull(info.Metadata);
        Assert.Empty(info.Metadata);
    }

    [Fact(DisplayName = "DriverInfo_设置Name属性_值正确返回")]
    public void DriverInfo_SetName_ReturnsCorrectValue()
    {
        var info = new DriverInfo { Name = "Test Driver" };
        Assert.Equal("Test Driver", info.Name);
    }

    [Fact(DisplayName = "DriverInfo_设置Version属性_值正确返回")]
    public void DriverInfo_SetVersion_ReturnsCorrectValue()
    {
        var info = new DriverInfo { Version = "2.1.0" };
        Assert.Equal("2.1.0", info.Version);
    }

    [Fact(DisplayName = "DriverInfo_设置FilePath属性_值正确返回")]
    public void DriverInfo_SetFilePath_ReturnsCorrectValue()
    {
        var info = new DriverInfo { FilePath = "C:\\drivers\\test.sys" };
        Assert.Equal("C:\\drivers\\test.sys", info.FilePath);
    }

    [Fact(DisplayName = "DriverInfo_设置TargetOS属性_值正确返回")]
    public void DriverInfo_SetTargetOS_ReturnsCorrectValue()
    {
        var info = new DriverInfo { TargetOS = "Windows" };
        Assert.Equal("Windows", info.TargetOS);
    }

    [Fact(DisplayName = "DriverInfo_设置Architecture属性_值正确返回")]
    public void DriverInfo_SetArchitecture_ReturnsCorrectValue()
    {
        var info = new DriverInfo { Architecture = "x64" };
        Assert.Equal("x64", info.Architecture);
    }

    [Fact(DisplayName = "DriverInfo_设置HardwareId属性_值正确返回")]
    public void DriverInfo_SetHardwareId_ReturnsCorrectValue()
    {
        var info = new DriverInfo { HardwareId = "PCI\\VEN_8086" };
        Assert.Equal("PCI\\VEN_8086", info.HardwareId);
    }

    [Fact(DisplayName = "DriverInfo_设置Hash属性_值正确返回")]
    public void DriverInfo_SetHash_ReturnsCorrectValue()
    {
        var info = new DriverInfo { Hash = "abc123" };
        Assert.Equal("abc123", info.Hash);
    }

    [Fact(DisplayName = "DriverInfo_设置HashAlgorithm属性_值正确返回")]
    public void DriverInfo_SetHashAlgorithm_ReturnsCorrectValue()
    {
        var info = new DriverInfo { HashAlgorithm = "MD5" };
        Assert.Equal("MD5", info.HashAlgorithm);
    }

    [Fact(DisplayName = "DriverInfo_TrustedPublishers_可以添加和检索发布者")]
    public void DriverInfo_TrustedPublishers_CanAddAndRetrievePublishers()
    {
        var info = new DriverInfo();
        info.TrustedPublishers.Add("Microsoft");
        info.TrustedPublishers.Add("Intel");

        Assert.Equal(2, info.TrustedPublishers.Count);
        Assert.Contains("Microsoft", info.TrustedPublishers);
        Assert.Contains("Intel", info.TrustedPublishers);
    }

    [Fact(DisplayName = "DriverInfo_设置Description属性_值正确返回")]
    public void DriverInfo_SetDescription_ReturnsCorrectValue()
    {
        var info = new DriverInfo { Description = "Network adapter driver" };
        Assert.Equal("Network adapter driver", info.Description);
    }

    [Fact(DisplayName = "DriverInfo_设置ReleaseDate属性_值正确返回")]
    public void DriverInfo_SetReleaseDate_ReturnsCorrectValue()
    {
        var date = new DateTime(2025, 6, 15);
        var info = new DriverInfo { ReleaseDate = date };
        Assert.Equal(date, info.ReleaseDate);
    }

    [Fact(DisplayName = "DriverInfo_Metadata_可以添加和检索键值对")]
    public void DriverInfo_Metadata_CanAddAndRetrieveKeyValuePairs()
    {
        var info = new DriverInfo();
        info.Metadata["Author"] = "JusterZhu";
        info.Metadata["Platform"] = "Windows";

        Assert.Equal(2, info.Metadata.Count);
        Assert.Equal("JusterZhu", info.Metadata["Author"]);
        Assert.Equal("Windows", info.Metadata["Platform"]);
    }

    [Fact(DisplayName = "DriverInfo_TrustedPublishers_空列表_Count为0")]
    public void DriverInfo_TrustedPublishers_EmptyList_CountIsZero()
    {
        var info = new DriverInfo();
        Assert.Empty(info.TrustedPublishers);
        Assert.Equal(0, info.TrustedPublishers.Count);
    }

    [Fact(DisplayName = "DriverInfo_Name为空字符串时_不会抛出异常")]
    public void DriverInfo_NameIsEmptyString_DoesNotThrow()
    {
        var info = new DriverInfo { Name = "" };
        Assert.Equal(string.Empty, info.Name);
    }
}
