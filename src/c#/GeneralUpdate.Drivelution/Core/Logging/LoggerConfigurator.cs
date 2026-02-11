using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Events;

namespace GeneralUpdate.Drivelution.Core.Logging;

/// <summary>
/// 日志配置器
/// Logger configurator
/// </summary>
public static class LoggerConfigurator
{
    /// <summary>
    /// 配置日志器
    /// Configures logger
    /// </summary>
    /// <param name="options">驱动更新配置选项 / Driver update configuration options</param>
    /// <returns>配置好的日志器 / Configured logger</returns>
    public static IDrivelutionLogger ConfigureLogger(DrivelutionOptions? options = null)
    {
        // Return a new logger instance that raises events instead of writing to files
        return new DrivelutionLogger();
    }

    /// <summary>
    /// 创建默认日志器
    /// Creates default logger
    /// </summary>
    /// <returns>默认配置的日志器 / Logger with default configuration</returns>
    public static IDrivelutionLogger CreateDefaultLogger()
    {
        return new DrivelutionLogger();
    }
}
