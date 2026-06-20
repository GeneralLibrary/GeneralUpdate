using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Configuration;

using GeneralUpdate.Core.Hooks;
using IUpdateReporter = GeneralUpdate.Core.Download.Reporting.IUpdateReporter;
using UpdateReport = GeneralUpdate.Core.Download.Reporting.UpdateReport;

namespace GeneralUpdate.Core.Strategy
{
    /// <summary>
    /// Abstract base class that defines platform-specific update strategies.
    /// Provides common logic for pipeline execution loops, context construction, and error handling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is a typical application of the Template Method pattern. Subclasses (<see cref="WindowsStrategy"/>, <see cref="LinuxStrategy"/>,
    /// <see cref="MacStrategy"/>) override <see cref="BuildPipeline"/> to provide their own platform-specific middleware chains.
    /// </para>
    /// <para>
    /// <b>Pipeline Execution Loop (<see cref="ExecuteAsync"/>):</b>
    /// <list type="number">
    ///   <item><description>Iterates through the <c>_configinfo.UpdateVersions</c> collection, processing each update version one by one.</description></item>
    ///   <item><description>Calls <see cref="CreatePipelineContext"/> to build the pipeline context, which contains key parameters such as
    ///   the archive path, hash value, format encoding, source path, and patch configuration.</description></item>
    ///   <item><description>Calls <see cref="BuildPipeline"/> (abstract method, implemented by subclasses) to obtain the middleware builder.</description></item>
    ///   <item><description>Executes <c>PipelineBuilder.Build()</c> to run the registered middleware in FIFO order:
    ///   <c>Hash</c> (integrity verification) → <c>Decompress</c> (extract update package) → <c>Patch</c> (apply incremental patches).</description></item>
    ///   <item><description>Reports the update result for the current version via <see cref="Reporter"/>.</description></item>
    ///   <item><description>Deletes the processed archive file.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Error Handling:</b> When an individual version update fails, <see cref="HandleExecuteException"/> records the exception and dispatches events,
    /// then <c>TryRollback</c> attempts to restore from the backup directory. Errors do not interrupt processing of subsequent versions.
    /// After all versions have been processed, the temporary directory is cleaned up and <see cref="OnExecuteCompleteAsync"/> is called.
    /// </para>
    /// </remarks>
    public abstract class AbstractStrategy : IStrategy
    {
        private const string Patchs = "patchs";

        /// <summary>
        /// Global configuration information containing parameters such as update package path, temporary directory, report URL, and version list.
        /// Initialized by the <see cref="Create"/> method and used by the pipeline execution loop.
        /// </summary>
        protected UpdateContext _configinfo = new();

        /// <summary>
        /// Gets or sets the lifecycle hooks. Injected by the bootstrap to execute custom logic before and after updates.
        /// </summary>
        public IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();

        /// <summary>
        /// Gets or sets the update status reporter. Responsible for reporting the processing progress and final result of each version to the server.
        /// </summary>
        public IUpdateReporter Reporter { get; set; } = new Download.Reporting.HttpUpdateReporter();

        /// <summary>
        /// Gets or sets the differential patch pipeline. Supports parallel application of incremental patches and progress reporting.
        /// </summary>
        public DiffPipeline? DiffPipeline { get; set; }

        /// <summary>
        /// Gets or sets the name of the application to launch. Set by the upper-level strategy (such as <see cref="UpdateStrategy"/>)
        /// before calling <see cref="StartAppAsync"/>.
        /// </summary>
        public string? LaunchAppName { get; set; }

        /// <summary>
        /// Gets or sets whether to also launch the Bowl helper process. Only valid on the Windows platform.
        /// Set by the upper-level strategy before calling <see cref="StartAppAsync"/>.
        /// </summary>
        public bool LaunchBowl { get; set; }

        /// <summary>
        /// Gets or sets whether to prefer using the update path. When <c>true</c>, <see cref="StartAppAsync"/>
        /// will first attempt to resolve the application from <see cref="UpdateContext.UpdatePath"/>,
        /// and fall back to <see cref="UpdateContext.InstallPath"/> on failure.
        /// Set by <see cref="ClientStrategy"/> when launching the upgrade process.
        /// </summary>
        public bool UseUpdatePath { get; set; }

        /// <summary>
        /// After <see cref="ExecuteAsync"/> completes, indicates whether every package in the
        /// current batch was applied without error. A per-package failure does not prevent the
        /// loop from continuing, so callers must inspect this flag before treating the batch
        /// as fully successful (e.g. before writing updated version numbers to the manifest).
        /// </summary>
        public bool AllPackagesSucceeded { get; private set; }

        /// <summary>
        /// Starts the main application. Virtual method, overridden by subclasses to provide platform-specific application launch implementations.
        /// </summary>
        /// <remarks>
        /// The default implementation throws <see cref="NotImplementedException"/>. Subclasses should override this method to execute
        /// platform-specific process launch logic (such as setting the working directory, environment variables, etc.).
        /// </remarks>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public virtual Task StartAppAsync() => throw new NotImplementedException();
        
        /// <summary>
        /// Executes the core pipeline loop for updates. Iterates through all pending update versions, sequentially building pipeline contexts,
        /// executing the middleware chain, reporting status, and cleaning up resources.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Pipeline Execution Loop Details:</b>
        /// <list type="number">
        ///   <item><description><b>Iterate Versions:</b> Retrieves <see cref="VersionEntry"/> objects one by one from <c>_configinfo.UpdateVersions</c>.</description></item>
        ///   <item><description><b>Build Context:</b> Calls <see cref="CreatePipelineContext"/> to create a
        ///   <see cref="PipelineContext"/> containing key parameters such as the archive path (composed of <c>TempPath</c> and the version name),
        ///   hash value, compression format, source path, and patch configuration.</description></item>
        ///   <item><description><b>Build Pipeline:</b> Calls <see cref="BuildPipeline"/> (abstract method implemented by subclasses
        ///   with platform-specific logic) to register middleware such as <c>Hash</c> (integrity verification), <c>Decompress</c> (extraction),
        ///   and <c>Patch</c> (incremental patches).</description></item>
        ///   <item><description><b>Execute Pipeline:</b> Calls <c>PipelineBuilder.Build()</c> to execute all registered middleware
        ///   in FIFO order.</description></item>
        ///   <item><description><b>Report Status:</b> Reports the current version's update result (success or failure) to the server
        ///   via <see cref="VersionService.Report"/>.</description></item>
        ///   <item><description><b>Clean Up Resources:</b> Calls <c>DeleteVersionZip</c> to remove the processed archive file for the current version.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <b>Error Handling Strategy:</b> When an individual version update fails, the exception is caught and
        /// <see cref="HandleExecuteException"/> logs the error, <c>TryRollback</c> attempts to restore from the backup,
        /// and processing continues with the next version. After all versions have been processed, regardless of whether any versions failed,
        /// cleanup operations are performed and <see cref="OnExecuteCompleteAsync"/> is called.
        /// </para>
        /// <para>
        /// <b>Resource Cleanup:</b> After the loop ends, the patch temporary directory is deleted, and <c>TempPath</c> is cleaned up
        /// (only deleted when empty to avoid accidentally removing package files for other <c>AppType</c> values).
        /// </para>
        /// </remarks>
        public virtual async Task ExecuteAsync()
        {
            var patchRoot = string.Empty;
            try
            {
                AllPackagesSucceeded = true;
                var status = ReportType.None;
                patchRoot = StorageManager.GetTempDirectory(Patchs);
                foreach (var version in _configinfo.UpdateVersions)
                {
                    try
                    {
                        // Use a version-specific subdirectory under patchRoot so that
                        // chain packages do not overwrite each other's extracted patches.
                        // patchRoot is cleaned as a whole after the loop.
                        // Sanitize: version.Name may be null or empty, and may contain
                        // path separators or "."/".." entries that could cause collisions
                        // or path traversal. Derive a safe key from the version string.
                        var rawName = version.Name ?? "unknown";
                        var safeDir = rawName
                            .Replace(Path.DirectorySeparatorChar, '_')
                            .Replace(Path.AltDirectorySeparatorChar, '_');
                        var versionName = string.IsNullOrWhiteSpace(safeDir) || safeDir is "." or ".."
                            ? $"version_{(version.Version ?? rawName).GetHashCode():X8}"
                            : safeDir;
                        var patchPath = Path.Combine(patchRoot, versionName);
                        var context = CreatePipelineContext(version, patchPath);
                        var pipelineBuilder = BuildPipeline(context);
                        await pipelineBuilder.Build();
                        status = ReportType.Success;
                    }
                    catch (Exception e) when (version.PackageType == (int)PackageType.Chain
                        && !string.IsNullOrEmpty(version.FallbackFullName))
                    {
                        GeneralTracer.Warn($"AbstractStrategy.ExecuteAsync: chain patch failed for {version.Version}, falling back to full package {version.FallbackFullName}. Error: {e.Message}");

                        // Rebuild pipeline context with the fallback full zip.
                        // CompressMiddleware will extract directly to SourcePath,
                        // and platform strategies skip PatchMiddleware for Full packages.
                        var fallbackContext = new PipelineContext();
                        var fallbackZipPath = Path.Combine(_configinfo.TempPath,
                            $"{version.FallbackFullName}{_configinfo.Format.ToExtension()}");
                        fallbackContext.Add("ZipFilePath", fallbackZipPath);
                        fallbackContext.Add("Hash", version.FallbackFullHash);
                        fallbackContext.Add("Format", _configinfo.Format);
                        fallbackContext.Add("Encoding", _configinfo.Encoding);
                        fallbackContext.Add("SourcePath", ResolveTargetPath(version));
                        fallbackContext.Add("PatchPath", Path.Combine(patchRoot, "fallback"));
                        fallbackContext.Add("PatchEnabled", false);
                        fallbackContext.Add("PackageType", (int)PackageType.Full);
                        fallbackContext.Add("DiffPipeline", DiffPipeline);

                        var fallbackBuilder = BuildPipeline(fallbackContext);
                        await fallbackBuilder.Build();
                        status = ReportType.Success;
                    }
                    catch (Exception e)
                    {
                        status = ReportType.Failure;
                        AllPackagesSucceeded = false;
                        HandleExecuteException(e);
                        TryRollback();
                    }
                    finally
                    {
                        await Reporter.ReportAsync(new UpdateReport(version.RecordId, status, version.AppType ?? 1));

                        // Delete only this version's zip file — other AppType packages
                        // in TempPath may still be needed by a downstream process.
                        DeleteVersionZip(version);
                    }
                }

                TryCleanTempPath();
                await OnExecuteCompleteAsync();
            }
            catch (Exception e)
            {
                AllPackagesSucceeded = false;
                HandleExecuteException(e);
            }
            finally
            {
                if (!string.IsNullOrEmpty(patchRoot))
                    Clear(patchRoot);
            }
        }

        /// <summary>
        /// Initializes the strategy instance. Receives global configuration information and stores it for subsequent use.
        /// </summary>
        /// <param name="parameter">Global configuration information containing parameters such as update package path, temporary directory, report URL, and version list.</param>
        public virtual void Create(UpdateContext parameter) => _configinfo = parameter;

        /// <summary>
        /// Creates the pipeline context, populating common parameters and platform-specific parameters.
        /// Subclasses can override this method to add platform-specific context parameters.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The pipeline context (<see cref="PipelineContext"/>) contains the following key-value pairs:
        /// <list type="table">
        ///   <listheader><term>Key</term><description>Description</description></listheader>
        ///   <item><term><c>ZipFilePath</c></term><description>Full path to the archive, composed of <c>TempPath</c>
        ///   and the version name, used by the <c>Decompress</c> middleware to locate the update package.</description></item>
        ///   <item><term><c>Hash</c></term><description>Hash value of the update package, used by the <c>Hash</c> middleware
        ///   to verify file integrity and prevent data corruption or tampering.</description></item>
        ///   <item><term><c>Format</c></term><description>Compression format (e.g., ZIP, GZip), used by the
        ///   <c>Decompress</c> middleware to select the appropriate decompression algorithm.</description></item>
        ///   <item><term><c>Encoding</c></term><description>File encoding format, used to correctly handle file name encoding during decompression.</description></item>
        ///   <item><term><c>SourcePath</c></term><description>Target installation path, determined by <see cref="ResolveTargetPath"/>
        ///   based on the version's application type and configuration.</description></item>
        ///   <item><term><c>PatchPath</c></term><description>Temporary storage path for patch files, used by the <c>Patch</c> middleware.</description></item>
        ///   <item><term><c>PatchEnabled</c></term><description>Whether incremental patching is enabled, controlled by <c>_configinfo.PatchEnabled</c>.</description></item>
        ///   <item><term><c>DiffPipeline</c></term><description>Differential patch pipeline instance, used for parallel patch application and progress reporting.</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <param name="version">The current version information to be processed, containing name, hash, application type, etc.</param>
        /// <param name="patchPath">The temporary storage directory path for patch files.</param>
        /// <returns>The populated pipeline context instance.</returns>
        protected virtual PipelineContext CreatePipelineContext(VersionEntry version, string patchPath)
        {
            var context = new PipelineContext();
            // Common parameters
            context.Add("ZipFilePath", Path.Combine(_configinfo.TempPath, $"{version.Name}{_configinfo.Format.ToExtension()}"));
            // Hash middleware
            context.Add("Hash", version.Hash);
            // Zip middleware
            context.Add("Format", _configinfo.Format);
            context.Add("Encoding", _configinfo.Encoding);
            // Patch middleware
            // For Upgrade packages, apply to UpdatePath if configured; otherwise fall back to InstallPath
            var sourcePath = ResolveTargetPath(version);
            context.Add("SourcePath", sourcePath);
            context.Add("PatchPath", patchPath);
            context.Add("PatchEnabled", _configinfo.PatchEnabled);
            // PackageType: 0=Unspecified, 1=Chain (differential), 2=Full (self-contained).
            // Used by CompressMiddleware and platform strategies to decide decompression target
            // and whether PatchMiddleware is needed.
            context.Add("PackageType", version.PackageType);
            // DiffPipeline for parallel patch application with progress reporting
            context.Add("DiffPipeline", DiffPipeline);
            
            return context;
        }

        /// <summary>
        /// Builds the update pipeline middleware chain. Abstract method; each platform subclass provides its specific middleware registration logic.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Subclasses should create a <see cref="PipelineBuilder"/> instance in this method and register the following middleware
        /// in order (the sequence must not be changed):
        /// <list type="number">
        ///   <item><description><b>Hash Middleware:</b> Verifies the file integrity of the update package to prevent data corruption or tampering.</description></item>
        ///   <item><description><b>Decompress Middleware:</b> Extracts the update package to the target installation directory.</description></item>
        ///   <item><description><b>Patch Middleware:</b> Applies incremental patches, updating only changed files to save bandwidth and disk space.</description></item>
        /// </list>
        /// Each platform can add additional middleware as needed (such as permission settings, symbolic link handling, etc.).
        /// </para>
        /// </remarks>
        /// <param name="context">The pipeline context containing all parameters required for middleware execution.</param>
        /// <returns>The pipeline builder instance with middleware configured.</returns>
        protected abstract PipelineBuilder BuildPipeline(PipelineContext context);

        /// <summary>
        /// Called after <see cref="ExecuteAsync"/> completes successfully. Subclasses can override this method to add platform-specific post-execution logic.
        /// </summary>
        /// <remarks>
        /// This method is called after all versions have been processed and the temporary directories have been cleaned up.
        /// It can be used to execute platform-specific finishing work, such as cleaning temporary files or logging completion.
        /// </remarks>
        /// <returns>A task that represents the asynchronous operation.</returns>
        protected virtual Task OnExecuteCompleteAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles exceptions that occur during pipeline execution. Logs the error and dispatches exception information through the event system.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is called when <see cref="ExecuteAsync"/> catches an exception. It performs the following operations:
        /// <list type="number">
        ///   <item><description>Logs the exception stack trace and message via <see cref="GeneralTracer.Error"/>.</description></item>
        ///   <item><description>Dispatches <see cref="ExceptionEventArgs"/> via <see cref="EventManager"/>
        ///   for upper-layer listeners to handle.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Subclasses can override this method to add custom error handling logic, such as sending alert notifications or recording audit logs.
        /// </para>
        /// </remarks>
        /// <param name="e">The exception caught during pipeline execution.</param>
        protected virtual void HandleExecuteException(Exception e)
        {
            GeneralTracer.Error($"Strategy execution exception.", e);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
        }

        /// <summary>
        /// Checks whether the target file exists under the specified path.
        /// </summary>
        protected static string CheckPath(string path, string name)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(name))
                return string.Empty;
            var tempPath = Path.Combine(path, name);
            return File.Exists(tempPath) ? tempPath : string.Empty;
        }

        /// <summary>
        /// Resolves the full path of the executable. Optionally checks <see cref="UpdateContext.UpdatePath"/> first,
        /// then falls back to <c>InstallPath</c> on failure.
        /// </summary>
        /// <param name="name">The name of the executable file.</param>
        /// <param name="preferUpdatePath">When <c>true</c>, checks <c>UpdatePath</c> first.</param>
        /// <returns>The full path if found; otherwise, an empty string.</returns>
        protected string ResolveAppPath(string name, bool preferUpdatePath = false)
        {
            if (preferUpdatePath && !string.IsNullOrWhiteSpace(_configinfo.UpdatePath))
            {
                var upgradeDir = ResolveUpdateDir();
                var path = CheckPath(upgradeDir, name);
                if (!string.IsNullOrEmpty(path))
                    return path;
            }

            return CheckPath(_configinfo.InstallPath, name);
        }

        /// <summary>
        /// Resolves and starts the target executable using the strategy's platform-specific
        /// path resolution logic. Does not perform any additional work (no hooks, no shutdown).
        /// For use by callers that only need process launch, e.g. <see cref="Silent.SilentPollOrchestrator"/>.
        /// </summary>
        /// <param name="appName">The name of the executable to launch.</param>
        /// <param name="preferUpdatePath">When <c>true</c>, checks <c>UpdatePath</c> first.</param>
        /// <exception cref="FileNotFoundException">Thrown when the executable cannot be resolved.</exception>
        internal void StartProcess(string appName, bool preferUpdatePath = false)
        {
            var appPath = ResolveAppPath(appName, preferUpdatePath);
            if (string.IsNullOrEmpty(appPath))
                throw new FileNotFoundException($"Can't find the app {appName}!");
            GeneralTracer.Info($"AbstractStrategy.StartProcess: launching {appPath}");
            using var process = Process.Start(appPath);
            if (process == null)
                throw new InvalidOperationException($"Failed to start process: {appPath}");
            GeneralTracer.Info($"AbstractStrategy.StartProcess: process started (PID: {process.Id}).");
        }

        /// <summary>
        /// Resolves the target application path for the update package. Determines whether to use the update path or install path
        /// based on the version's application type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Path Resolution Logic:</b>
        /// <list type="bullet">
        ///   <item><description>If the current version has application type Upgrade (<c>AppType == 2</c>) and
        ///   <c>UpdatePath</c> is configured, <c>UpdatePath</c> is used as the target path first.</description></item>
        ///   <item><description>Otherwise, <c>InstallPath</c> is used as the target path.</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <c>UpdatePath</c> can be an absolute or relative path. Relative paths are combined with <c>InstallPath</c> to form the full path.
        /// This design allows upgrade-end update packages to be applied to a different target directory than the client.
        /// </para>
        /// </remarks>
        /// <param name="version">The current version information to be processed, used to determine the application type.</param>
        /// <returns>The full path of the target installation directory.</returns>
        protected string ResolveTargetPath(VersionEntry version)
        {
            if (version.AppType == 2 && !string.IsNullOrWhiteSpace(_configinfo.UpdatePath))
                return ResolveUpdateDir();

            return _configinfo.InstallPath;
        }

        /// <summary>
        /// Resolves <see cref="UpdateContext.UpdatePath"/> to an absolute path.
        /// Relative paths are combined with <see cref="UpdateContext.InstallPath"/>.
        /// </summary>
        private string ResolveUpdateDir()
        {
            return Path.IsPathRooted(_configinfo.UpdatePath)
                ? _configinfo.UpdatePath
                : Path.Combine(_configinfo.InstallPath, _configinfo.UpdatePath);
        }

        // ═══ Safe hooks/reporter wrappers (shared by all strategy subclasses) ═══
        // Note: Each subclass builds its own UpdateContext via BuildUpdateContext().
        // Subclasses should call hooks/reporter through their own context-aware wrappers.
        // The Hooks and Reporter properties are declared here so subclasses inherit them
        // without redeclaring.

        /// <summary>
        /// Attempts to restore from backup when a pipeline execution fails.
        /// Tries the specific backup directory for this update first;
        /// falls back to the most recent backup if the specific one is unavailable.
        /// </summary>
        private void TryRollback()
        {
            try
            {
                var backupDir = _configinfo.BackupDirectory;
                // If the specific backup for this update doesn't exist,
                // fall back to the most recent backup by timestamp.
                if (string.IsNullOrWhiteSpace(backupDir) || !Directory.Exists(backupDir))
                {
                    GeneralTracer.Info($"AbstractStrategy.TryRollback: specific backup not found ({backupDir}), searching for latest.");
                    backupDir = StorageManager.GetLatestBackup(_configinfo.InstallPath);
                }

                if (!string.IsNullOrWhiteSpace(backupDir) && Directory.Exists(backupDir))
                {
                    GeneralTracer.Warn($"AbstractStrategy.TryRollback: restoring from backup {backupDir} -> {_configinfo.InstallPath}");
                    StorageManager.Restore(backupDir, _configinfo.InstallPath);
                    GeneralTracer.Info("AbstractStrategy.TryRollback: restore completed.");
                }
                else
                {
                    GeneralTracer.Warn("AbstractStrategy.TryRollback: no backup found, rollback skipped.");
                }
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("AbstractStrategy.TryRollback: rollback failed.", ex);
            }
        }

        private static void Clear(string path)
        {
            if (Directory.Exists(path))
                StorageManager.DeleteDirectory(path);
        }

        /// <summary>
        /// Deletes the zip file for a processed version from TempPath.
        /// Only removes the specific file — other packages in the same directory
        /// may belong to a different AppType and must be kept for downstream processes.
        /// </summary>
        private void DeleteVersionZip(VersionEntry version)
        {
            if (string.IsNullOrWhiteSpace(_configinfo.TempPath)) return;

            var zipPath = Path.Combine(_configinfo.TempPath, $"{version.Name}{_configinfo.Format.ToExtension()}");
            try
            {
                if (File.Exists(zipPath))
                {
                    File.SetAttributes(zipPath, FileAttributes.Normal);
                    File.Delete(zipPath);
                    GeneralTracer.Info($"AbstractStrategy: deleted processed zip {zipPath}");
                }
            }
            catch (Exception ex)
            {
                GeneralTracer.Warn($"AbstractStrategy: failed to delete zip {zipPath}. {ex.Message}");
            }
        }

        /// <summary>
        /// Removes TempPath if it is empty after all processed zips have been deleted.
        /// The last process in the chain (usually Upgrade) will find an empty directory
        /// and clean it up. Earlier processes skip this because other AppType packages
        /// still remain in the directory.
        /// </summary>
        private void TryCleanTempPath()
        {
            try
            {
                var tempPath = _configinfo.TempPath;
                if (string.IsNullOrWhiteSpace(tempPath) || !Directory.Exists(tempPath)) return;

                if (!Directory.EnumerateFileSystemEntries(tempPath).Any())
                {
                    Directory.Delete(tempPath, false);
                    GeneralTracer.Info($"AbstractStrategy: cleaned empty temp directory {tempPath}");
                }
            }
            catch (Exception ex)
            {
                GeneralTracer.Warn($"AbstractStrategy: failed to clean temp directory. {ex.Message}");
            }
        }
    }
}
