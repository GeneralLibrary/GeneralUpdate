using System;
using System.Diagnostics;
using System.IO;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategys;

public class LinuxStrategy : AbstractStrategy
{
    protected override PipelineContext CreatePipelineContext(VersionInfo version, string patchPath)
    {
        var context = base.CreatePipelineContext(version, patchPath);
        
        // Driver middleware (Linux-specific)
        if (_configinfo.DriveEnabled == true)
        {
            context.Add("DriverDirectory", _configinfo.DriverDirectory);
        }
        
        return context;
    }

    protected override PipelineBuilder BuildPipeline(PipelineContext context)
    {
        var builder = new PipelineBuilder(context)
            .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled)
            .UseMiddleware<CompressMiddleware>()
            .UseMiddleware<HashMiddleware>();
#if NET10_0_OR_GREATER
        builder.UseMiddlewareIf<DrivelutionMiddleware>(_configinfo.DriveEnabled == true);
#endif
        return builder;
    }

    public override void Execute()
    {
        ExecuteAsync().Wait();
    }

    protected override void OnExecuteComplete()
    {
        StartApp();
    }

    public override void StartApp()
    {
        try
        {
            var mainAppPath = CheckPath(_configinfo.InstallPath, _configinfo.MainAppName);
            if (string.IsNullOrEmpty(mainAppPath))
                throw new Exception($"Can't find the app {mainAppPath}!");

            ExecuteScript();
            Process.Start(mainAppPath);
        }
        catch (Exception e)
        {
            GeneralTracer.Error(
                "The StartApp method in the GeneralUpdate.Core.LinuxStrategy class throws an exception.", e);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
        }
        finally
        {
            GeneralTracer.Dispose();
            Process.GetCurrentProcess().Kill();
        }
    }

    /// <summary>
    /// Executes the user-specified script.
    /// </summary>
    private void ExecuteScript()
    {
        try
        {
            // Check if the script path is valid (_configinfo should come from the base class configuration)
            if (string.IsNullOrEmpty(_configinfo.Script) || !File.Exists(_configinfo.Script))
            {
                GeneralTracer.Info("No valid script path specified, skipping script execution");
                return;
            }

            GeneralTracer.Info($"Starting to execute script: {_configinfo.Script}");

            // Start process to execute Linux script (using bash)
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{_configinfo.Script}\"", // Execute the script
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                GeneralTracer.Error("Failed to start script process");
                return;
            }

            // Read script output logs
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(); // Wait for the script to finish execution

            if (!string.IsNullOrEmpty(output))
                GeneralTracer.Info($"Script output: {output}");

            if (!string.IsNullOrEmpty(error))
                GeneralTracer.Warn($"Script warning: {error}");

            if (process.ExitCode != 0)
                throw new Exception($"Script execution failed, exit code: {process.ExitCode}");

            GeneralTracer.Info("Script executed successfully");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("An exception occurred while executing the script", ex);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(ex, "Script execution failed"));
        }
    }
}