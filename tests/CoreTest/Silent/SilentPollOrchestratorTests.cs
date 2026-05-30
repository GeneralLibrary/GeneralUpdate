using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Silent;

namespace CoreTest.Silent;

public class SilentPollOrchestratorTests
{
    private GlobalConfigInfo CreateValidConfig()
    {
        return new GeneralUpdate.Core.Configuration.GlobalConfigInfo
        {
            UpdateUrl = "https://api.example.com/update",
            ClientVersion = "1.0.0",
            AppSecretKey = "secret",
            UpdateAppName = "Update.exe",
            MainAppName = "MainApp",
            InstallPath = Path.GetTempPath()
        };
    }

    [Fact]
    public void Ctor_ConfigInfoNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SilentPollOrchestrator(null, new SilentOptions()));
    }

    [Fact]
    public void Ctor_OptionsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SilentPollOrchestrator(CreateValidConfig(), null));
    }

    [Fact]
    public void Ctor_ValidParameters_CreatesInstance()
    {
        var orchestrator = new SilentPollOrchestrator(
            CreateValidConfig(), new SilentOptions());
        Assert.NotNull(orchestrator);
        orchestrator.Dispose();
    }

    [Fact]
    public void WithHooks_ReturnsSameInstance()
    {
        var orchestrator = new SilentPollOrchestrator(
            CreateValidConfig(), new SilentOptions());
        var result = orchestrator.WithHooks(new GeneralUpdate.Core.Hooks.NoOpUpdateHooks());
        Assert.Same(orchestrator, result);
        orchestrator.Dispose();
    }

    [Fact]
    public void WithReporter_ReturnsSameInstance()
    {
        var orchestrator = new SilentPollOrchestrator(
            CreateValidConfig(), new SilentOptions());
        var result = orchestrator.WithReporter(new GeneralUpdate.Core.Download.Reporting.HttpUpdateReporter());
        Assert.Same(orchestrator, result);
        orchestrator.Dispose();
    }

    [Fact]
    public void WithHooks_Null_Accepted()
    {
        var orchestrator = new SilentPollOrchestrator(
            CreateValidConfig(), new SilentOptions());
        var result = orchestrator.WithHooks(null);
        Assert.Same(orchestrator, result);
        orchestrator.Dispose();
    }

    [Fact]
    public void Stop_WithoutStart_DoesNotThrow()
    {
        var orchestrator = new SilentPollOrchestrator(
            CreateValidConfig(), new SilentOptions());
        var ex = Record.Exception(() => orchestrator.Stop());
        Assert.Null(ex);
        orchestrator.Dispose();
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        var orchestrator = new SilentPollOrchestrator(
            CreateValidConfig(), new SilentOptions());
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
    public void SilentOptions_CustomPollInterval_Stored()
    {
        var options = new SilentOptions { PollInterval = TimeSpan.FromMinutes(30) };
        Assert.Equal(TimeSpan.FromMinutes(30), options.PollInterval);
    }
}
