using Microsoft.Extensions.Logging;
using Velaris.Sdk.Platform;

namespace Velaris.Sdk.Tests.Platform;

public class PlatformTests
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(b => b.AddConsole());

    [Fact]
    public void LinuxStrategy_ReturnsCorrectPlatform() =>
        Assert.Equal(VelaPlatform.Linux, new LinuxStrategy(_loggerFactory.CreateLogger<LinuxStrategy>()).TargetPlatform);

    [Fact]
    public void WindowsStrategy_ReturnsCorrectPlatform() =>
        Assert.Equal(VelaPlatform.WindowsIoT, new WindowsStrategy(_loggerFactory.CreateLogger<WindowsStrategy>()).TargetPlatform);

    [Fact]
    public void Resolver_ExplicitLinux_ReturnsLinuxStrategy() =>
        Assert.IsType<LinuxStrategy>(StrategyResolver.Resolve(VelaPlatform.Linux, _loggerFactory));

    [Fact]
    public void Resolver_ExplicitWindows_ReturnsWindowsStrategy() =>
        Assert.IsType<WindowsStrategy>(StrategyResolver.Resolve(VelaPlatform.WindowsIoT, _loggerFactory));
}
