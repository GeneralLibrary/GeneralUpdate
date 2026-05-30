using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Silent;
using GeneralUpdate.Core.Strategy;

namespace CoreTest.Silent;

public class SilentPollOrchestratorTests
{
    private static GlobalConfigInfo CreateValidConfig()
    {
        return new GlobalConfigInfo
        {
            UpdateUrl = "https://api.example.com/update",
            ClientVersion = "1.0.0",
            AppSecretKey = "secret",
            UpdateAppName = "Update.exe",
            MainAppName = "MainApp",
            InstallPath = Path.GetTempPath()
        };
    }

    private static ClientStrategy CreateValidStrategy()
    {
        return new ClientStrategy { LaunchAfterPrepare = false };
    }

    [Fact]
    public void Ctor_StrategyNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SilentPollOrchestrator(null!, CreateValidConfig(), new SilentOptions()));
    }

    [Fact]
    public void Ctor_ConfigInfoNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SilentPollOrchestrator(CreateValidStrategy(), null!, new SilentOptions()));
    }

    [Fact]
    public void Ctor_OptionsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SilentPollOrchestrator(CreateValidStrategy(), CreateValidConfig(), null!));
    }

    [Fact]
    public void Ctor_ValidParameters_CreatesInstance()
    {
        var orchestrator = new SilentPollOrchestrator(
            CreateValidStrategy(), CreateValidConfig(), new SilentOptions());
        Assert.NotNull(orchestrator);
        orchestrator.Dispose();
    }

    [Fact]
    public void Stop_WithoutStart_DoesNotThrow()
    {
        var orchestrator = new SilentPollOrchestrator(
            CreateValidStrategy(), CreateValidConfig(), new SilentOptions());
        var ex = Record.Exception(() => orchestrator.Stop());
        Assert.Null(ex);
        orchestrator.Dispose();
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        var orchestrator = new SilentPollOrchestrator(
            CreateValidStrategy(), CreateValidConfig(), new SilentOptions());
        var ex = Record.Exception(() => orchestrator.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void SilentOptions_DefaultPollInterval_IsOneHour()
    {
        var options = new SilentOptions();
        Assert.Equal(TimeSpan.FromHours(1), options.PollInterval);
    }

    [Fact]
    public void SilentOptions_DefaultLaunchClientAfterUpdate_IsTrue()
    {
        var options = new SilentOptions();
        Assert.True(options.LaunchClientAfterUpdate);
    }

    [Fact]
    public void SilentOptions_CustomPollInterval_Stored()
    {
        var options = new SilentOptions { PollInterval = TimeSpan.FromMinutes(30) };
        Assert.Equal(TimeSpan.FromMinutes(30), options.PollInterval);
    }
}
