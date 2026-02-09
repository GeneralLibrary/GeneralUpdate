using Serilog;
using Serilog.Events;
using GeneralUpdate.Drivelution.Abstractions.Configuration;

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
    public static ILogger ConfigureLogger(DrivelutionOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var loggerConfig = new LoggerConfiguration();

        // Set log level
        var logLevel = ParseLogLevel(options.LogLevel);
        loggerConfig.MinimumLevel.Is(logLevel);

        // Configure console sink
        if (options.EnableConsoleLogging)
        {
            loggerConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        // Configure file sink
        if (options.EnableFileLogging)
        {
            var logPath = string.IsNullOrWhiteSpace(options.LogFilePath)
                ? "./Logs/drivelution-.log"
                : options.LogFilePath;

            loggerConfig.WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10 * 1024 * 1024); // 10MB per file
        }

        // Enrich with context information
        loggerConfig.Enrich.FromLogContext();

        return loggerConfig.CreateLogger();
    }

    /// <summary>
    /// 解析日志级别
    /// Parses log level
    /// </summary>
    /// <param name="logLevel">日志级别字符串 / Log level string</param>
    /// <returns>日志事件级别 / Log event level</returns>
    private static LogEventLevel ParseLogLevel(string logLevel)
    {
        return logLevel?.ToUpperInvariant() switch
        {
            "DEBUG" or "VERBOSE" => LogEventLevel.Debug,
            "INFO" or "INFORMATION" => LogEventLevel.Information,
            "WARN" or "WARNING" => LogEventLevel.Warning,
            "ERROR" => LogEventLevel.Error,
            "FATAL" or "CRITICAL" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }

    /// <summary>
    /// 创建默认日志器
    /// Creates default logger
    /// </summary>
    /// <returns>默认配置的日志器 / Logger with default configuration</returns>
    public static ILogger CreateDefaultLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                "./Logs/drivelution-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 30)
            .Enrich.FromLogContext()
            .CreateLogger();
    }
}
