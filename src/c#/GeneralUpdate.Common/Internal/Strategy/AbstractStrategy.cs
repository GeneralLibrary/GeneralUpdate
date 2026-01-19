using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Common.Shared.Object.Enum;
using GeneralUpdate.Common.Shared.Service;

namespace GeneralUpdate.Common.Internal.Strategy
{
    public abstract class AbstractStrategy : IStrategy
    {
        protected const string Patchs = "patchs";
        protected GlobalConfigInfo _configinfo = new();
        
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

                if (!string.IsNullOrEmpty(_configinfo.UpdateLogUrl))
                {
                    OpenBrowser(_configinfo.UpdateLogUrl);
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
            var className = GetType().Name;
            GeneralTracer.Error($"Exception in {className}.ExecuteAsync method.", e);
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

        protected static void OpenBrowser(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                return;
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
                return;
            }
            
            throw new PlatformNotSupportedException("Unsupported OS platform");
        }
        
        protected static void Clear(string path)
        {
            if (Directory.Exists(path))
                StorageManager.DeleteDirectory(path);
        }
    }
}