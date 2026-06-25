using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Bowl.Internal;
using GeneralUpdate.Bowl.Strategies;
using GeneralUpdate.Bowl.FileSystem;

namespace GeneralUpdate.Bowl;

/// <summary>
/// Process crash surveillance daemon.
/// Monitors whether the main application starts normally after an upgrade.
/// If a startup crash is detected, captures a dump, exports diagnostics,
/// and optionally restores the backup version.
/// </summary>
public sealed class BowlBootstrap
{
    private readonly IBowlStrategy _strategy;
    private readonly ICrashReporter _crashReporter;
    private readonly ISystemInfoProvider _systemInfoProvider;

    // ---- Constructors ----

    /// <summary>
    /// Creates a BowlBootstrap instance with auto-detected platform strategy and default providers.
    /// </summary>
    public BowlBootstrap()
        : this(
            StrategyFactory.Create(),
            new CrashReporter(),
            SystemInfoProviderFactory.Create())
    { }

    /// <summary>
    /// DI-friendly constructor for testing. All dependencies are injectable.
    /// </summary>
    internal BowlBootstrap(
        IBowlStrategy strategy,
        ICrashReporter crashReporter,
        ISystemInfoProvider systemInfoProvider)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _crashReporter = crashReporter ?? throw new ArgumentNullException(nameof(crashReporter));
        _systemInfoProvider = systemInfoProvider ?? throw new ArgumentNullException(nameof(systemInfoProvider));
    }

    // ---- Public Async API ----

    /// <summary>
    /// Launches crash surveillance asynchronously.
    /// </summary>
    /// <param name="context">Execution context. Use <see cref="BowlContext.Normalize"/> to apply defaults.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating whether the monitored process exited normally.</returns>
    public async Task<BowlResult> LaunchAsync(BowlContext context, CancellationToken ct = default)
    {
        context = context.Normalize();

        GeneralTracer.Info($"BowlBootstrap.LaunchAsync: monitoring '{context.ProcessNameOrId}' " +
            $"at '{context.TargetPath}', workModel={context.WorkModel}");

        // Phase 1: Prepare child process start info
        var startInfo = _strategy.Prepare(context);
        if (startInfo == null)
        {
            // Strategy may return null when tooling is unavailable (e.g. procdump not installed on Linux).
            // This is a graceful degradation, not a platform error.
            GeneralTracer.Warn("BowlBootstrap.LaunchAsync: strategy returned null — monitoring tool unavailable.");
            return new BowlResult { Success = false, ExitCode = -1, DumpCaptured = false };
        }

        // Phase 2: Run procdump child process
        ProcessExitResult exitResult;
        try
        {
            exitResult = await ProcessRunner.RunAsync(startInfo, context.TimeoutMs, ct);
        }
        catch (OperationCanceledException)
        {
            GeneralTracer.Warn("BowlBootstrap.LaunchAsync: cancelled.");
            throw;
        }
        catch (TimeoutException)
        {
            GeneralTracer.Warn("BowlBootstrap.LaunchAsync: child process timed out.");
            // Treat timeout as a non-crash exit (no dump captured)
            return new BowlResult { Success = false, ExitCode = -1, DumpCaptured = false };
        }

        // Phase 3: Check if a dump was produced
        var dumpPath = FindDumpFile(context);
        var dumpCaptured = dumpPath != null;

        if (!dumpCaptured)
        {
            GeneralTracer.Info("BowlBootstrap.LaunchAsync: no dump file found, process exited normally.");
            return new BowlResult
            {
                Success = exitResult.ExitCode == 0,
                ExitCode = exitResult.ExitCode,
                DumpCaptured = false,
            };
        }

        // Phase 4: Crash detected — run the full remediation pipeline
        GeneralTracer.Warn($"BowlBootstrap.LaunchAsync: crash detected, dump at {dumpPath}.");
        return await HandleCrashAsync(context, exitResult, dumpPath!, ct);
    }

    // ---- Crash Remediation Pipeline ----

    private async Task<BowlResult> HandleCrashAsync(
        BowlContext context, ProcessExitResult exitResult, string dumpPath, CancellationToken ct)
    {
        var restored = false;
        string? crashReportPath = null;

        // 1. Generate crash report JSON
        try
        {
            crashReportPath = await _crashReporter.GenerateReportAsync(
                context, exitResult.OutputLines, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("BowlBootstrap.HandleCrashAsync: crash report generation failed.", ex);
            // Continue with remaining steps even if report generation fails
        }

        // 2. Export system diagnostics
        try
        {
            await _systemInfoProvider.ExportAsync(context.FailDirectory, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("BowlBootstrap.HandleCrashAsync: system info export failed.", ex);
        }

        // 3. Restore backup if in Upgrade mode with AutoRestore enabled
        if (context.AutoRestore && context.WorkModel == "Upgrade")
        {
            try
            {
                GeneralTracer.Info($"BowlBootstrap.HandleCrashAsync: restoring backup from " +
                    $"'{context.BackupDirectory}' to '{context.TargetPath}'.");
                StorageHelper.Restore(context.BackupDirectory, context.TargetPath);
                restored = true;
                GeneralTracer.Info("BowlBootstrap.HandleCrashAsync: restore completed.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("BowlBootstrap.HandleCrashAsync: restore failed.", ex);
            }
        }

        // 4. Platform-specific post-processing
        try
        {
            await _strategy.PostProcessAsync(context, exitResult, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("BowlBootstrap.HandleCrashAsync: post-process failed.", ex);
        }

        // 5. Invoke crash callback
        if (context.OnCrash != null)
        {
            try
            {
                var crashInfo = new CrashInfo
                {
                    DumpFilePath = dumpPath,
                    CrashReportPath = crashReportPath ?? string.Empty,
                    Version = context.ExtendedField,
                    ExitCode = exitResult.ExitCode,
                };
                await context.OnCrash(crashInfo, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("BowlBootstrap.HandleCrashAsync: OnCrash callback failed.", ex);
            }
        }

        return new BowlResult
        {
            Success = false,
            ExitCode = exitResult.ExitCode,
            DumpCaptured = true,
            DumpFilePath = dumpPath,
            CrashReportPath = crashReportPath,
            Restored = restored,
        };
    }

    // ---- Helpers ----

    private static string? FindDumpFile(BowlContext context)
    {
        var path = Path.Combine(context.FailDirectory, context.DumpFileName);
        return File.Exists(path) ? path : null;
    }
}
