using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Differential.Abstractions;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Network;
using GeneralUpdate.Core.Hooks;
using IUpdateReporter = GeneralUpdate.Core.Download.Reporting.IUpdateReporter;

namespace GeneralUpdate.Core.Strategy
{
    public abstract class AbstractStrategy : IStrategy
    {
        private const string Patchs = "patchs";
        protected GlobalConfigInfo _configinfo = new();

        /// <summary>Hooks for pre/post update callbacks.</summary>
        public IUpdateHooks Hooks { get; set; } = new Hooks.NoOpUpdateHooks();

        /// <summary>Reporter for update status reporting.</summary>
        public IUpdateReporter Reporter { get; set; } = new Download.Reporting.NoOpUpdateReporter();

        /// <summary>DiffPipeline for parallel patch application with progress reporting.</summary>
        public DiffPipeline? DiffPipeline { get; set; }

        /// <summary>App to launch in <see cref="StartAppAsync"/>. Set by the upper strategy.</summary>
        public string? LaunchAppName { get; set; }

        /// <summary>Whether to also start the Bowl companion process. Windows only. Set by the upper strategy.</summary>
        public bool LaunchBowl { get; set; }

        /// <summary>
        /// When true, <see cref="StartAppAsync"/> resolves the app from <see cref="GlobalConfigInfo.UpdatePath"/>
        /// first before falling back to <see cref="GlobalConfigInfo.InstallPath"/>.
        /// Set by <see cref="ClientUpdateStrategy"/> when launching the upgrade process.
        /// </summary>
        public bool UseUpdatePath { get; set; }

        public virtual Task StartAppAsync() => throw new NotImplementedException();
        
        public virtual async Task ExecuteAsync()
        {
            try
            {
                var status = ReportType.None;
                var patchPath = StorageManager.GetTempDirectory(Patchs);
                foreach (var version in _configinfo.UpdateVersions)
                {
                    try
                    {
                        var context = CreatePipelineContext(version, patchPath);
                        var pipelineBuilder = BuildPipeline(context);
                        await pipelineBuilder.Build();
                        status = ReportType.Success;
                    }
                    catch (Exception e)
                    {
                        status = ReportType.Failure;
                        HandleExecuteException(e);
                        TryRollback();
                    }
                    finally
                    {
                        await VersionService.Report(_configinfo.ReportUrl
                            , version.RecordId
                            , status
                            , version.AppType
                            , _configinfo.Scheme
                            , _configinfo.Token);

                        // Delete only this version's zip file — other AppType packages
                        // in TempPath may still be needed by a downstream process.
                        DeleteVersionZip(version);
                    }
                }

                Clear(patchPath);
                TryCleanTempPath();
                await OnExecuteCompleteAsync();
            }
            catch (Exception e)
            {
                HandleExecuteException(e);
            }
        }

        public virtual void Create(GlobalConfigInfo parameter) => _configinfo = parameter;

        /// <summary>
        /// Creates the pipeline context with common and platform-specific parameters.
        /// Override this method to add platform-specific context parameters.
        /// </summary>
        protected virtual PipelineContext CreatePipelineContext(VersionInfo version, string patchPath)
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
            // DiffPipeline for parallel patch application with progress reporting
            context.Add("DiffPipeline", DiffPipeline);
            
            return context;
        }

        /// <summary>
        /// Builds the pipeline with middleware components.
        /// Override this method to customize the pipeline for specific platforms.
        /// </summary>
        protected abstract PipelineBuilder BuildPipeline(PipelineContext context);

        /// <summary>
        /// Called after ExecuteAsync completes successfully.
        /// Override this method to add platform-specific post-execution logic.
        /// </summary>
        protected virtual Task OnExecuteCompleteAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles exceptions during execution.
        /// Override this method to customize error handling.
        /// </summary>
        protected virtual void HandleExecuteException(Exception e)
        {
            GeneralTracer.Error($"Strategy execution exception.", e);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
        }

        /// <summary>
        /// Checks if a file exists at the specified path.
        /// </summary>
        protected static string CheckPath(string path, string name)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(name))
                return string.Empty;
            var tempPath = Path.Combine(path, name);
            return File.Exists(tempPath) ? tempPath : string.Empty;
        }

        /// <summary>
        /// Resolves the full path for an executable, optionally checking
        /// <see cref="GlobalConfigInfo.UpdatePath"/> before falling back to InstallPath.
        /// </summary>
        /// <param name="name">The executable name.</param>
        /// <param name="preferUpdatePath">When true, checks UpdatePath first.</param>
        /// <returns>Full path if found, empty string otherwise.</returns>
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
        /// Resolves the target directory for applying a package.
        /// For Upgrade (AppType=2) packages, uses <see cref="GlobalConfigInfo.UpdatePath"/> if configured;
        /// otherwise falls back to <see cref="GlobalConfigInfo.InstallPath"/>.
        /// </summary>
        protected string ResolveTargetPath(VersionInfo version)
        {
            if (version.AppType == 2 && !string.IsNullOrWhiteSpace(_configinfo.UpdatePath))
                return ResolveUpdateDir();

            return _configinfo.InstallPath;
        }

        /// <summary>
        /// Resolves <see cref="GlobalConfigInfo.UpdatePath"/> to an absolute path.
        /// Relative paths are combined with <see cref="GlobalConfigInfo.InstallPath"/>.
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
        /// Only restores if a backup directory exists for the current version.
        /// </summary>
        private void TryRollback()
        {
            try
            {
                var backupDir = _configinfo.BackupDirectory;
                if (!string.IsNullOrWhiteSpace(backupDir) && Directory.Exists(backupDir))
                {
                    GeneralTracer.Warn($"AbstractStrategy.TryRollback: restoring from backup {backupDir} -> {_configinfo.InstallPath}");
                    StorageManager.Restore(backupDir, _configinfo.InstallPath);
                    GeneralTracer.Info("AbstractStrategy.TryRollback: restore completed.");
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
        private void DeleteVersionZip(VersionInfo version)
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
