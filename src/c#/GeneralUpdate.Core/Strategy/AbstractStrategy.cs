using System;
using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.FileSystem;
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

        /// <summary>Optional hooks for pre/post update callbacks.</summary>
        protected IUpdateHooks? Hooks { get; set; }

        /// <summary>Optional reporter for update status reporting.</summary>
        protected IUpdateReporter? Reporter { get; set; }

        /// <summary>Optional binary differ for differential patch updates.</summary>
        public IBinaryDiffer? Differ { get; set; }
        
        public virtual void Execute() => throw new NotImplementedException();
        
        public virtual void StartApp() => throw new NotImplementedException();
        
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
                    }
                }

                Clear(patchPath);
                Clear(_configinfo.TempPath);
                OnExecuteComplete();
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
            context.Add("ZipFilePath", Path.Combine(_configinfo.TempPath, $"{version.Name}{_configinfo.Format}"));
            // Hash middleware
            context.Add("Hash", version.Hash);
            // Zip middleware
            context.Add("Format", _configinfo.Format);
            context.Add("Name", version.Name);
            context.Add("Encoding", _configinfo.Encoding);
            // Patch middleware
            context.Add("SourcePath", _configinfo.InstallPath);
            context.Add("PatchPath", patchPath);
            context.Add("PatchEnabled", _configinfo.PatchEnabled);
            // Binary differ for differential patching
            context.Add("BinaryDiffer", Differ);
            
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
        protected virtual void OnExecuteComplete()
        {
            // Default implementation does nothing
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
    }
}
