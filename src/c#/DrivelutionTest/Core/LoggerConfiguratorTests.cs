using GeneralUpdate.Drivelution.Core.Logging;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Events;

namespace DrivelutionTest.Core;

public class LoggerConfiguratorTests
{
    [Fact(DisplayName = "LoggerConfigurator_ConfigureLogger_null参数_返回DrivelutionLogger")]
    public void ConfigureLogger_NullOptions_ReturnsLogger()
    {
        var logger = LoggerConfigurator.ConfigureLogger(null);
        Assert.NotNull(logger);
        Assert.IsType<DrivelutionLogger>(logger);
    }

    [Fact(DisplayName = "LoggerConfigurator_ConfigureLogger_有参数_返回DrivelutionLogger")]
    public void ConfigureLogger_WithOptions_ReturnsLogger()
    {
        var logger = LoggerConfigurator.ConfigureLogger(new DrivelutionOptions());
        Assert.NotNull(logger);
        Assert.IsType<DrivelutionLogger>(logger);
    }

    [Fact(DisplayName = "LoggerConfigurator_CreateDefaultLogger_返回DrivelutionLogger")]
    public void CreateDefaultLogger_ReturnsDrivelutionLogger()
    {
        var logger = LoggerConfigurator.CreateDefaultLogger();
        Assert.NotNull(logger);
        Assert.IsType<DrivelutionLogger>(logger);
    }

    [Fact(DisplayName = "LoggerConfigurator_ConfigureLogger_返回实例可使用")]
    public void ConfigureLogger_ReturnsUsableInstance()
    {
        var logger = LoggerConfigurator.ConfigureLogger();
        bool eventRaised = false;
        logger.LogMessage += (_, _) => eventRaised = true;
        logger.Information("test");
        Assert.True(eventRaised);
    }
}
