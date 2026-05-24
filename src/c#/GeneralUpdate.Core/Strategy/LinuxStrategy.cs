using System;
using System.Diagnostics;
using System.IO;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core.Strategy;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Pipeline;

namespace GeneralUpdate.Core.Strategy;

public class LinuxStrategy : AbstractStrategy
{
    protected override PipelineContext CreatePipelineContext(VersionInfo version, string patchPath)
    {
        GeneralTracer.Info($"GeneralUpdate.Core.LinuxStrategy.CreatePipelineContext: building context for version={version.Version}, patchPath={patchPath}, driveEnabled={_configinfo.DriveEnabled}");
        var context = base.CreatePipelineContext(version, patchPath);
        
        // Driver middleware (Linux-specific)
        if (_configinfo.DriveEnabled == true)
        {
            context.Add("DriverDirectory", _configinfo.DriverDirectory);
            GeneralTracer.Info($"GeneralUpdate.Core.LinuxStrategy.CreatePipelineContext: driver update enabled, DriverDirectory={_configinfo.DriverDirectory}");
        }
        
        return context;
    }

    protected override PipelineBuilder BuildPipeline(PipelineContext context)
    {
        GeneralTracer.Info($"GeneralUpdate.Core.LinuxStrategy.BuildPipeline: assembling middleware pipeline. PatchEnabled={_configinfo.PatchEnabled}, DriveEnabled={_configinfo.DriveEnabled}");
        var builder = new PipelineBuilder(context)
            .UseMiddlewareIf<PatchMiddleware>(_configinfo.PatchEnabled)
            .UseMiddleware<CompressMiddleware>()
            .UseMiddleware<HashMiddleware>();
        // DrivelutionMiddleware: add GeneralUpdate.Drivelution project reference to enable
        return builder;
    }

    public override void Execute()
    {
        ExecuteAsync().Wait();
    }

    protected override void OnExecuteComplete()
    {
        GeneralTracer.Info("GeneralUpdate.Core.LinuxStrategy.OnExecuteComplete: all versions processed, starting application.");
        StartApp();
    }

    public override void StartApp()
    {
        try
        {
            var mainAppPath = CheckPath(_configinfo.InstallPath, _configinfo.MainAppName);
            if (string.IsNullOrEmpty(mainAppPath))
                throw new Exception($"Can't find the app {mainAppPath}!");

            GeneralTracer.Info($"GeneralUpdate.Core.LinuxStrategy.StartApp: executing startup script then launching main app={mainAppPath}");
            ExecuteScript();
            Process.Start(mainAppPath);
            GeneralTracer.Info("GeneralUpdate.Core.LinuxStrategy.StartApp: main app launched successfully.");
        }
        catch (Exception e)
        {
            GeneralTracer.Error(
                "The StartApp method in the GeneralUpdate.Core.LinuxStrategy class throws an exception.", e);
            EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
        }
        finally
        {
            GeneralTracer.Info("GeneralUpdate.Core.LinuxStrategy.StartApp: releasing tracer and terminating updater process.");
            GeneralTracer.Dispose();
            GracefulExit.CurrentProcessAsync().GetAwaiter().GetResult();
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
