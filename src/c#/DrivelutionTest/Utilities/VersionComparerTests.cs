using GeneralUpdate.Drivelution.Core.Utilities;

namespace DrivelutionTest.Utilities;

/// <summary>
/// VersionComparer 测试
/// 分支覆盖点:
/// - Compare: 正常版本比较, null/空字符串抛出 ArgumentException
/// - Major/Minor/Patch 不同时的比较
/// - 预发布版本比较: 正式版 > 预发布版
/// - 预发布标识符比较: 数字 vs 数字, 字母 vs 字母, 数字 vs 字母
/// - 较长预发布 > 较短预发布
/// - IsGreaterThan / IsLessThan / IsEqual 辅助方法
/// - IsValidSemVer: 有效版本返回 true, null/空/无效返回 false
/// - 非SemVer格式抛出 FormatException
/// 触发条件：调用各比较和验证方法
/// 预期结果：正确比较和验证
/// </summary>
public class VersionComparerTests
{
    [Theory(DisplayName = "VersionComparer_Compare_相同版本返回0")]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("2.1.3", "2.1.3")]
    [InlineData("0.0.0", "0.0.0")]
    [InlineData("10.20.30", "10.20.30")]
    public void Compare_SameVersions_ReturnsZero(string v1, string v2)
    {
        Assert.Equal(0, VersionComparer.Compare(v1, v2));
    }

    [Theory(DisplayName = "VersionComparer_Compare_v1大于v2返回1")]
    [InlineData("2.0.0", "1.0.0")]
    [InlineData("1.2.0", "1.1.0")]
    [InlineData("1.0.3", "1.0.2")]
    [InlineData("10.0.0", "9.99.99")]
    public void Compare_V1Greater_ReturnsOne(string v1, string v2)
    {
        Assert.Equal(1, VersionComparer.Compare(v1, v2));
    }

    [Theory(DisplayName = "VersionComparer_Compare_v1小于v2返回-1")]
    [InlineData("1.0.0", "2.0.0")]
    [InlineData("1.1.0", "1.2.0")]
    [InlineData("1.0.2", "1.0.3")]
    public void Compare_V1Less_ReturnsMinusOne(string v1, string v2)
    {
        Assert.Equal(-1, VersionComparer.Compare(v1, v2));
    }

    [Theory(DisplayName = "VersionComparer_Compare_正式版大于预发布版")]
    [InlineData("1.0.0", "1.0.0-alpha")]
    [InlineData("1.0.0", "1.0.0-beta.1")]
    public void Compare_ReleaseGreaterThanPreRelease_ReturnsOne(string v1, string v2)
    {
        Assert.True(VersionComparer.Compare(v1, v2) > 0);
    }

    [Theory(DisplayName = "VersionComparer_Compare_预发布版小于正式版")]
    [InlineData("1.0.0-alpha", "1.0.0")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    public void Compare_PreReleaseLessThanRelease_ReturnsMinusOne(string v1, string v2)
    {
        Assert.True(VersionComparer.Compare(v1, v2) < 0);
    }

    [Theory(DisplayName = "VersionComparer_Compare_预发布版本号比较")]
    [InlineData("1.0.0-1", "1.0.0-2")]
    [InlineData("1.0.0-alpha", "1.0.0-beta")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.2")]
    public void Compare_PreReleaseComparison_OrderedCorrectly(string v1, string v2)
    {
        Assert.True(VersionComparer.Compare(v1, v2) < 0);
    }

    [Fact(DisplayName = "VersionComparer_Compare_数字预发布标识小于字母标识")]
    public void Compare_NumericPreReleaseLessThanAlpha()
    {
        Assert.True(VersionComparer.Compare("1.0.0-1", "1.0.0-alpha") < 0);
    }

    [Fact(DisplayName = "VersionComparer_Compare_较长预发布标识更大")]
    public void Compare_LongerPreReleaseIsGreater()
    {
        Assert.True(VersionComparer.Compare("1.0.0-alpha.1.2", "1.0.0-alpha.1") > 0);
    }

    [Theory(DisplayName = "VersionComparer_Compare_null或空字符串抛出ArgumentException")]
    [InlineData(null, "1.0.0")]
    [InlineData("1.0.0", null)]
    [InlineData("", "1.0.0")]
    [InlineData("1.0.0", "")]
    [InlineData("  ", "1.0.0")]
    public void Compare_NullOrEmpty_ThrowsArgumentException(string? v1, string? v2)
    {
        Assert.Throws<ArgumentException>(() => VersionComparer.Compare(v1!, v2!));
    }

    [Fact(DisplayName = "VersionComparer_Compare_无效版本格式抛出FormatException")]
    public void Compare_InvalidFormat_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => VersionComparer.Compare("not.a.version", "1.0.0"));
    }

    [Theory(DisplayName = "VersionComparer_IsGreaterThan_正确判断")]
    [InlineData("2.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "2.0.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    public void IsGreaterThan_CorrectlyDetermined(string v1, string v2, bool expected)
    {
        Assert.Equal(expected, VersionComparer.IsGreaterThan(v1, v2));
    }

    [Theory(DisplayName = "VersionComparer_IsLessThan_正确判断")]
    [InlineData("1.0.0", "2.0.0", true)]
    [InlineData("2.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    public void IsLessThan_CorrectlyDetermined(string v1, string v2, bool expected)
    {
        Assert.Equal(expected, VersionComparer.IsLessThan(v1, v2));
    }

    [Theory(DisplayName = "VersionComparer_IsEqual_正确判断")]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.1", false)]
    public void IsEqual_CorrectlyDetermined(string v1, string v2, bool expected)
    {
        Assert.Equal(expected, VersionComparer.IsEqual(v1, v2));
    }

    [Theory(DisplayName = "VersionComparer_IsValidSemVer_有效版本格式")]
    [InlineData("1.0.0")]
    [InlineData("0.0.0")]
    [InlineData("10.20.30")]
    [InlineData("1.0.0-alpha")]
    [InlineData("1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.beta")]
    [InlineData("1.0.0+build")]
    [InlineData("1.0.0-alpha+build")]
    [InlineData("1.0.0+build.123")]
    public void IsValidSemVer_ValidVersions_ReturnsTrue(string version)
    {
        Assert.True(VersionComparer.IsValidSemVer(version));
    }

    [Theory(DisplayName = "VersionComparer_IsValidSemVer_无效版本格式")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("1.0.0.0")]
    [InlineData("not.a.version")]
    public void IsValidSemVer_InvalidVersions_ReturnsFalse(string? version)
    {
        Assert.False(VersionComparer.IsValidSemVer(version));
    }

    [Theory(DisplayName = "VersionComparer_Compare_带BuildMetadata版本正确比较")]
    [InlineData("1.0.0+build1", "1.0.0+build2")]
    public void Compare_WithBuildMetadata_ComparedCorrectly(string v1, string v2)
    {
        Assert.Equal(0, VersionComparer.Compare(v1, v2));
    }

    [Theory(DisplayName = "VersionComparer_Compare_包含预发布和构建元数据")]
    [InlineData("1.0.0-alpha.1+build", "1.0.0-alpha.2+build")]
    public void Compare_WithBuildAndPreRelease_CorrectOrder(string v1, string v2)
    {
        Assert.True(VersionComparer.Compare(v1, v2) < 0);
    }

    [Fact(DisplayName = "VersionComparer_Compare_Major值极大版本比较")]
    public void Compare_VeryLargeMajorVersion_Works()
    {
        Assert.Equal(1, VersionComparer.Compare("999999.0.0", "999998.0.0"));
    }
}
