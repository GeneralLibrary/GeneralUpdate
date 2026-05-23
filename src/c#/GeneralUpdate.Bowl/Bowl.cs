using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Bowl.Internal;
using GeneralUpdate.Bowl.Strategies;
using GeneralUpdate.Bowl.Strategys;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Bowl;

/// <summary>
/// Process crash surveillance daemon.
/// Monitors whether the main application starts normally after an upgrade.
/// If a startup crash is detected, captures a dump, exports diagnostics,
/// and optionally restores the backup version.
/// </summary>
public sealed class Bowl
{
    private readonly IBowlStrategy _strategy;
    private readonly ICrashReporter _crashReporter;
    private readonly ISystemInfoProvider _systemInfoProvider;
    private readonly IEnvironmentProvider _env;

    // ---- Constructors ----

    /// <summary>
    /// Creates a Bowl instance with auto-detected platform strategy and default providers.
    /// </summary>
    public Bowl()
        : this(
            StrategyFactory.Create(),
            new CrashReporter(),
            SystemInfoProviderFactory.Create(),
            new EnvironmentProvider())
    { }

    /// <summary>
    /// DI-friendly constructor for testing. All dependencies are injectable.
    /// </summary>
    internal Bowl(
        IBowlStrategy strategy,
        ICrashReporter crashReporter,
        ISystemInfoProvider systemInfoProvider,
        IEnvironmentProvider env)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _crashReporter = crashReporter ?? throw new ArgumentNullException(nameof(crashReporter));
        _systemInfoProvider = systemInfoProvider ?? throw new ArgumentNullException(nameof(systemInfoProvider));
        _env = env ?? throw new ArgumentNullException(nameof(env));
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

        GeneralTracer.Info($"Bowl.LaunchAsync: monitoring '{context.ProcessNameOrId}' " +
            $"at '{context.TargetPath}', workModel={context.WorkModel}");

        // Phase 1: Prepare child process start info
        var startInfo = _strategy.Prepare(context);
        if (startInfo == null)
        {
            GeneralTracer.Fatal("Bowl.LaunchAsync: platform strategy returned null — unsupported platform.");
            throw new PlatformNotSupportedException(
                $"No Bowl strategy available for the current platform.");
        }

        // Phase 2: Run procdump child process
        ProcessExitResult exitResult;
        try
        {
            exitResult = await ProcessRunner.RunAsync(startInfo, context.TimeoutMs, ct);
        }
        catch (OperationCanceledException)
        {
            GeneralTracer.Warn("Bowl.LaunchAsync: cancelled.");
            throw;
        }
        catch (TimeoutException)
        {
            GeneralTracer.Warn("Bowl.LaunchAsync: child process timed out.");
            // Treat timeout as a non-crash exit (no dump captured)
            return new BowlResult { Success = false, ExitCode = -1, DumpCaptured = false };
        }

        // Phase 3: Check if a dump was produced
        var dumpPath = FindDumpFile(context);
        var dumpCaptured = dumpPath != null;

        if (!dumpCaptured)
        {
            GeneralTracer.Info("Bowl.LaunchAsync: no dump file found, process exited normally.");
            return new BowlResult
            {
                Success = exitResult.ExitCode == 0,
                ExitCode = exitResult.ExitCode,
                DumpCaptured = false,
            };
        }

        // Phase 4: Crash detected — run the full remediation pipeline
        GeneralTracer.Warn($"Bowl.LaunchAsync: crash detected, dump at {dumpPath}.");
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
        catch (Exception ex)
        {
            GeneralTracer.Error("Bowl.HandleCrashAsync: crash report generation failed.", ex);
            // Continue with remaining steps even if report generation fails
        }

        // 2. Export system diagnostics
        try
        {
            await _systemInfoProvider.ExportAsync(context.FailDirectory, ct);
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("Bowl.HandleCrashAsync: system info export failed.", ex);
        }

        // 3. Restore backup if in Upgrade mode with AutoRestore enabled
        if (context.AutoRestore && context.WorkModel == "Upgrade")
        {
            try
            {
                GeneralTracer.Info($"Bowl.HandleCrashAsync: restoring backup from " +
                    $"'{context.BackupDirectory}' to '{context.TargetPath}'.");
                StorageManager.Restore(context.BackupDirectory, context.TargetPath);
                restored = true;
                GeneralTracer.Info("Bowl.HandleCrashAsync: restore completed.");
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("Bowl.HandleCrashAsync: restore failed.", ex);
            }
        }

        // 4. Mark failed version to prevent re-upgrading to it
        if (context.WorkModel == "Upgrade")
        {
            try
            {
                _env.SetVariable("UpgradeFail", context.ExtendedField);
                GeneralTracer.Warn($"Bowl.HandleCrashAsync: UpgradeFail set to '{context.ExtendedField}'.");
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("Bowl.HandleCrashAsync: failed to set UpgradeFail env var.", ex);
            }
        }

        // 5. Platform-specific post-processing
        try
        {
            await _strategy.PostProcessAsync(context, exitResult, ct);
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("Bowl.HandleCrashAsync: post-process failed.", ex);
        }

        // 6. Invoke crash callback
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
            catch (Exception ex)
            {
                GeneralTracer.Error("Bowl.HandleCrashAsync: OnCrash callback failed.", ex);
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

    // ---- Backward Compatibility ----

    /// <summary>
    /// Legacy synchronous entry point. Kept for backward compatibility with existing callers.
    /// New code should use <see cref="LaunchAsync(BowlContext, CancellationToken)"/>.
    /// </summary>
    [Obsolete("Use instance.LaunchAsync(BowlContext, ct) instead. This method will be removed in v10.")]
    public static void Launch(MonitorParameter? monitorParameter = null)
    {
        GeneralTracer.Info("Bowl.Launch(legacy): translating to new async API.");

        BowlContext context;
        if (monitorParameter != null)
        {
            context = MapToContext(monitorParameter);
        }
        else
        {
            var param = CreateParameter();
            context = MapToContext(param);
        }

        var bowl = new Bowl();
        bowl.LaunchAsync(context).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Converts legacy <see cref="MonitorParameter"/> to the new <see cref="BowlContext"/>.
    /// </summary>
    public static BowlContext MapToContext(MonitorParameter p)
    {
        return new BowlContext
        {
            ProcessNameOrId = p.ProcessNameOrId,
            DumpFileName = p.DumpFileName,
            FailFileName = p.FailFileName,
            TargetPath = p.TargetPath,
            FailDirectory = p.FailDirectory,
            BackupDirectory = p.BackupDirectory,
            WorkModel = p.WorkModel,
            ExtendedField = p.ExtendedField,
            TimeoutMs = 30_000,
            DumpType = DumpType.Full,
            AutoRestore = true,
        };
    }

    /// <summary>
    /// Reads <c>ProcessInfo</c> environment variable and builds a legacy <see cref="MonitorParameter"/>.
    /// Shared with the legacy static API for backward compatibility.
    /// </summary>
    internal static MonitorParameter CreateParameter()
    {
        GeneralTracer.Info("Bowl.CreateParameter: reading ProcessInfo from environment variable.");

        var json = Environments.GetEnvironmentVariable("ProcessInfo");
        if (string.IsNullOrWhiteSpace(json))
        {
            GeneralTracer.Fatal("Bowl.CreateParameter: ProcessInfo environment variable is not set.");
            throw new ArgumentNullException(
                "ProcessInfo environment variable not set.");
        }

        var processInfo = JsonSerializer.Deserialize<ProcessInfo>(
            json, ProcessInfoJsonContext.Default.ProcessInfo);
        if (processInfo == null)
        {
            GeneralTracer.Fatal("Bowl.CreateParameter: failed to deserialize ProcessInfo JSON.");
            throw new ArgumentNullException(
                "ProcessInfo JSON deserialization failed.");
        }

        GeneralTracer.Info(
            $"Bowl.CreateParameter: AppName={processInfo.AppName}, Version={processInfo.LastVersion}");

        return new MonitorParameter
        {
            ProcessNameOrId = processInfo.AppName,
            DumpFileName = $"{processInfo.LastVersion}_fail.dmp",
            FailFileName = $"{processInfo.LastVersion}_fail.json",
            TargetPath = processInfo.InstallPath,
            FailDirectory = Path.Combine(processInfo.InstallPath, "fail", processInfo.LastVersion),
            BackupDirectory = Path.Combine(processInfo.InstallPath, processInfo.LastVersion),
            ExtendedField = processInfo.LastVersion,
        };
    }

    // ---- Helpers ----

    private static string? FindDumpFile(BowlContext context)
    {
        var path = Path.Combine(context.FailDirectory, context.DumpFileName);
        return File.Exists(path) ? path : null;
    }
}
