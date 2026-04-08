using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using GeneralUpdate.Bowl.Internal;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Shared;

namespace GeneralUpdate.Bowl.Strategys;

internal class WindowStrategy : AbstractStrategy
{
    private const string WorkModel = "Upgrade";
    private string? _applicationsDirectory;
    private List<Action> _actions = new();
    
    public override void Launch()
    {
        GeneralTracer.Info("WindowStrategy.Launch: initializing actions pipeline.");
        InitializeActions();
        _applicationsDirectory = Path.Combine(_parameter.TargetPath, "Applications", "Windows");
        _parameter.InnerApp = Path.Combine(_applicationsDirectory, GetAppName());
        var dmpFullName = Path.Combine(_parameter.FailDirectory, _parameter.DumpFileName);
        _parameter.InnerArguments = $"-e -ma {_parameter.ProcessNameOrId} {dmpFullName}";
        GeneralTracer.Info($"WindowStrategy.Launch: launching inner app={_parameter.InnerApp}, dumpFile={dmpFullName}.");
        //This method is used to launch scripts in applications.
        base.Launch();
        GeneralTracer.Info("WindowStrategy.Launch: base launch completed, executing final treatment.");
        ExecuteFinalTreatment();
        GeneralTracer.Info("WindowStrategy.Launch: launch lifecycle completed.");
    }

    private string GetAppName()
    {
        var name = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X86 => "procdump.exe",
            Architecture.X64 => "procdump64.exe",
            _ => "procdump64a.exe"
        };
        GeneralTracer.Info($"WindowStrategy.GetAppName: resolved procdump executable={name} for arch={RuntimeInformation.OSArchitecture}.");
        return name;
    }

    private void ExecuteFinalTreatment()
    {
        var dumpFile = Path.Combine(_parameter.FailDirectory, _parameter.DumpFileName);
        GeneralTracer.Info($"WindowStrategy.ExecuteFinalTreatment: checking for dump file at {dumpFile}.");
        if (File.Exists(dumpFile))
        {
            GeneralTracer.Info($"WindowStrategy.ExecuteFinalTreatment: dump file found, executing {_actions.Count} remediation action(s).");
            foreach (var action in _actions)
            {
                action.Invoke();
            }
            GeneralTracer.Info("WindowStrategy.ExecuteFinalTreatment: all remediation actions completed.");
        }
        else
        {
            GeneralTracer.Info("WindowStrategy.ExecuteFinalTreatment: no dump file found, monitored process exited normally.");
        }
    }

    private void InitializeActions()
    {
        _actions.Add(CreateCrash);
        _actions.Add(Export);
        _actions.Add(Restore);
        _actions.Add(SetEnvironment);
        GeneralTracer.Debug("WindowStrategy.InitializeActions: registered actions: CreateCrash, Export, Restore, SetEnvironment.");
    }

    /// <summary>
    /// Export the crash output information from procdump.exe and the monitoring parameters of Bowl.
    /// </summary>
    private void CreateCrash()
    {
        GeneralTracer.Info("WindowStrategy.CreateCrash: serializing crash report.");
        var crash = new Crash
        {
            Parameter = _parameter,
            ProcdumpOutPutLines = OutputList
        };
        var failJsonPath = Path.Combine(_parameter.FailDirectory, _parameter.FailFileName);
        StorageManager.CreateJson(failJsonPath, crash, CrashJsonContext.Default.Crash);
        GeneralTracer.Info($"WindowStrategy.CreateCrash: crash report written to {failJsonPath}.");
    }

    /// <summary>
    /// Export operating system information, system logs, and system driver information.
    /// </summary>
    private void Export()
    {
        GeneralTracer.Info("WindowStrategy.Export: exporting OS and system diagnostic information.");
        var batPath = Path.Combine(_applicationsDirectory, "export.bat");
        if (!File.Exists(batPath))
        {
            GeneralTracer.Error($"WindowStrategy.Export: export.bat not found at {batPath}.");
            throw new FileNotFoundException("export.bat not found!");
        }

        Process.Start(batPath, _parameter.FailDirectory);
        GeneralTracer.Info($"WindowStrategy.Export: export.bat started targeting {_parameter.FailDirectory}.");
    }

    /// <summary>
    /// Within the GeneralUpdate upgrade system, restore the specified backup version files to the current working directory.
    /// </summary>
    private void Restore()
    {
        GeneralTracer.Info($"WindowStrategy.Restore: checking work model. CurrentModel={_parameter.WorkModel}, ExpectedModel={WorkModel}.");
        if (string.Equals(_parameter.WorkModel, WorkModel))
        {
            GeneralTracer.Info($"WindowStrategy.Restore: restoring backup from {_parameter.BackupDirectory} to {_parameter.TargetPath}.");
            StorageManager.Restore(_parameter.BackupDirectory, _parameter.TargetPath);
            GeneralTracer.Info("WindowStrategy.Restore: restore completed successfully.");
        }
        else
        {
            GeneralTracer.Info("WindowStrategy.Restore: restore skipped, work model is not Upgrade.");
        }
    }

    /// <summary>
    /// Write the failed update version number to the local environment variable.
    /// </summary>
    private void SetEnvironment()
    {
        if (!string.Equals(_parameter.WorkModel, WorkModel))
        {
            GeneralTracer.Info("WindowStrategy.SetEnvironment: skipped, work model is not Upgrade.");
            return;
        }
     
        /*
         * The `UpgradeFail` environment variable is used to mark an exception version number during updates.
         * If the latest version number obtained via an HTTP request is less than or equal to the exception version number, the update is skipped.
         * Once this version number is set, it will not be removed, and updates will not proceed until a version greater than the exception version number is obtained through the HTTP request.
         */
        Environments.SetEnvironmentVariable("UpgradeFail", _parameter.ExtendedField);
        GeneralTracer.Warn($"WindowStrategy.SetEnvironment: UpgradeFail environment variable set to version={_parameter.ExtendedField}.");
    }
}