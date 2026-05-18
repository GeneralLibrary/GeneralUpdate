using Microsoft.Extensions.Logging;

namespace Velaris.Sdk;

/// <summary>
/// High-level managed API for the Vela OTA engine.
/// Wraps engine lifecycle (init/shutdown) and FlashPack operations
/// with safe handles and structured logging.
/// </summary>
public sealed class VelaCore : IDisposable
{
    private readonly ILogger<VelaCore> _logger;
    private readonly SafeHandles.EngineSafeHandle _engine;
    private bool _disposed;

    /// <summary>
    /// Initialize the Vela OTA engine.
    /// </summary>
    /// <param name="logger">Logger for structured telemetry.</param>
    /// <exception cref="VelaException">Thrown if engine initialization fails.</exception>
    public VelaCore(ILogger<VelaCore> logger)
    {
        _logger = logger;

        Log.InitializingEngine(logger);
        var handle = Interop.NativeMethods.vela_init();
        VelaException.ThrowIfInvalid(handle, "vela_init");

        _engine = new SafeHandles.EngineSafeHandle(handle);
        Log.EngineInitialized(logger);
    }

    /// <summary>
    /// Open a FlashPack file for reading and validation.
    /// </summary>
    /// <param name="path">Path to the .fpk file.</param>
    /// <returns>A FlashPack reader handle.</returns>
    /// <exception cref="VelaException">Thrown if the file cannot be opened.</exception>
    public FlashPack OpenFlashPack(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Log.OpeningFlashPack(_logger, path);
        var handle = Interop.NativeMethods.vela_fpk_open(path);
        VelaException.ThrowIfInvalid(handle, $"vela_fpk_open({path})");

        Log.FlashPackOpened(_logger, path);
        return new FlashPack(new SafeHandles.FlashPackSafeHandle(handle), _logger);
    }

    /// <summary>
    /// Try to open a FlashPack file, returning null on failure instead of throwing.
    /// </summary>
    public FlashPack? TryOpenFlashPack(string path)
    {
        try
        {
            return OpenFlashPack(path);
        }
        catch (VelaException ex)
        {
            Log.FlashPackOpenFailed(_logger, path, ex);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Log.ShuttingDownEngine(_logger);
        _engine.Dispose();
        Log.EngineShutdown(_logger);
    }
}

/// <summary>
/// Represents an opened FlashPack bundle.
/// Provides operations for reading metadata and verifying integrity.
/// </summary>
public sealed class FlashPack : IDisposable
{
    private readonly SafeHandles.FlashPackSafeHandle _handle;
    private readonly ILogger _logger;
    private bool _disposed;

    internal FlashPack(SafeHandles.FlashPackSafeHandle handle, ILogger logger)
    {
        _handle = handle;
        _logger = logger;
    }

    /// <summary>
    /// Whether this FlashPack is still valid (handle not freed).
    /// </summary>
    public bool IsValid => !_handle.IsInvalid && !_disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _handle.Dispose();
        FlashPackLog.Closed(_logger);
    }
}

// ─── high-performance logging source generators ────────────────

internal static partial class Log
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Initializing Vela OTA engine")]
    public static partial void InitializingEngine(ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Vela OTA engine initialized successfully")]
    public static partial void EngineInitialized(ILogger logger);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information,
        Message = "Shutting down Vela OTA engine")]
    public static partial void ShuttingDownEngine(ILogger logger);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information,
        Message = "Vela OTA engine shut down")]
    public static partial void EngineShutdown(ILogger logger);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Information,
        Message = "Opening FlashPack: {Path}")]
    public static partial void OpeningFlashPack(ILogger logger, string path);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information,
        Message = "FlashPack opened: {Path}")]
    public static partial void FlashPackOpened(ILogger logger, string path);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Error,
        Message = "Failed to open FlashPack: {Path}")]
    public static partial void FlashPackOpenFailed(ILogger logger, string path, Exception ex);
}

internal static partial class FlashPackLog
{
    [LoggerMessage(EventId = 1020, Level = LogLevel.Information,
        Message = "FlashPack handle closed")]
    public static partial void Closed(ILogger logger);
}
