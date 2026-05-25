using GeneralUpdate.Bowl.Internal;

/// <summary>
/// 分支覆盖点：
/// LinuxSystem 类：
///   - 构造函数：name 和 version 为正常值
///   - 构造函数：name 为 null
///   - 构造函数：version 为 null
///   - 构造函数：name 和 version 为空字符串
///   - Name 属性 get/set
///   - Version 属性 get/set
///   - Linux 发行版常见名称（ubuntu、rhel 等）
/// </summary>
public class LinuxSystemTests
{
    [Fact]
    public void 构造函数_正常参数_属性正确赋值()
    {
        var system = new LinuxSystem("ubuntu", "22.04");
        Assert.Equal("ubuntu", system.Name);
        Assert.Equal("22.04", system.Version);
    }

    [Fact]
    public void 构造函数_name为null_允许null()
    {
        var system = new LinuxSystem(null!, "1.0");
        Assert.Null(system.Name);
        Assert.Equal("1.0", system.Version);
    }

    [Fact]
    public void 构造函数_version为null_允许null()
    {
        var system = new LinuxSystem("debian", null!);
        Assert.Equal("debian", system.Name);
        Assert.Null(system.Version);
    }

    [Fact]
    public void 构造函数_name和version为空字符串()
    {
        var system = new LinuxSystem(string.Empty, string.Empty);
        Assert.Equal(string.Empty, system.Name);
        Assert.Equal(string.Empty, system.Version);
    }

    [Fact]
    public void 属性可写_set后正确返回()
    {
        var system = new LinuxSystem("initial", "0.0");
        system.Name = "fedora";
        system.Version = "40";
        Assert.Equal("fedora", system.Name);
        Assert.Equal("40", system.Version);
    }

    [Theory]
    [InlineData("ubuntu", "24.04")]
    [InlineData("rhel", "8.10")]
    [InlineData("centos", "9")]
    [InlineData("clearos", "7")]
    [InlineData("fedora", "39")]
    [InlineData("debian", "12")]
    public void 常见发行版_构造函数正常(string name, string version)
    {
        var system = new LinuxSystem(name, version);
        Assert.Equal(name, system.Name);
        Assert.Equal(version, system.Version);
    }

    [Fact]
    public void 两个相同属性的实例_不相等()
    {
        var sys1 = new LinuxSystem("ubuntu", "22.04");
        var sys2 = new LinuxSystem("ubuntu", "22.04");
        // Reference types, not record — should not be equal
        Assert.NotEqual(sys1, sys2);
    }
}
