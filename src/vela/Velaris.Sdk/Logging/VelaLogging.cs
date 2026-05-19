using Microsoft.Extensions.Logging;

namespace Velaris.Sdk.Logging;

/// <summary>
/// Source-generated structured logging definitions.
/// Uses LoggerMessageAttribute for zero-allocation, AOT-compatible logging.
/// All event IDs are in ranges: 1000 (engine), 2000 (FlashPack), 3000 (health), 4000 (FFI).
/// </summary>
internal static partial class VelaLogging
{
    // ─── Engine lifecycle (1000-1099) ─────────────────────────

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Vela engine initializing")]
    internal static partial void InitializingEngine(this ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Vela engine initialized successfully")]
    internal static partial void EngineInitialized(this ILogger logger);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information,
        Message = "Vela engine shutting down")]
    internal static partial void ShuttingDownEngine(this ILogger logger);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information,
        Message = "Vela engine shutdown complete")]
    internal static partial void EngineShutdown(this ILogger logger);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Error,
        Message = "Vela engine initialization failed")]
    internal static partial void EngineInitFailed(this ILogger logger, Exception error);

    // ─── FlashPack operations (2000-2099) ─────────────────────

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug,
        Message = "Opening FlashPack: {Path}")]
    internal static partial void OpeningFlashPack(this ILogger logger, string path);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information,
        Message = "FlashPack opened: {Path}")]
    internal static partial void FlashPackOpened(this ILogger logger, string path);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning,
        Message = "FlashPack open failed for {Path}: {Error}")]
    internal static partial void FlashPackOpenFailed(this ILogger logger, string path, Exception error);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Information,
        Message = "FlashPack closed")]
    internal static partial void FlashPackClosed(this ILogger logger);

    // ─── Platform strategy (3000-3099) ────────────────────────

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information,
        Message = "Platform strategy resolved: {Platform} ({StrategyType})")]
    internal static partial void PlatformStrategyResolved(this ILogger logger, string platform, string strategyType);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Debug,
        Message = "Platform validation: {Platform} — {Result}")]
    internal static partial void PlatformValidationResult(this ILogger logger, string platform, bool result);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning,
        Message = "Platform not supported: {OsDescription}")]
    internal static partial void PlatformNotSupported(this ILogger logger, string osDescription);

    // ─── DI / Configuration (4000-4099) ───────────────────────

    [LoggerMessage(EventId = 4001, Level = LogLevel.Information,
        Message = "Vela SDK configured: hub={HubUrl}, platform={Platform}, watchdog={WatchdogEnabled}")]
    internal static partial void SdkConfigured(this ILogger logger, string hubUrl, string platform, bool watchdogEnabled);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Debug,
        Message = "Vela services registered in DI container")]
    internal static partial void ServicesRegistered(this ILogger logger);
}
