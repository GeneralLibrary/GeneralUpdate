using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using GeneralUpdate.Bowl.Internal;
using GeneralUpdate.Common.FileBasic;

namespace GeneralUpdate.Bowl.Strategys;

internal class WindowStrategy : AbstractStrategy
{
    private const string WorkModel = "Upgrade";
    private string? _applicationsDirectory;
    private List<Action> _actions = new();
    
    public override void Launch()
    {
        InitializeActions();
        _applicationsDirectory = Path.Combine(_parameter.TargetPath, "Applications", "Windows");
        _parameter.InnerApp = Path.Combine(_applicationsDirectory, GetAppName());
        var dmpFullName = Path.Combine(_parameter.FailDirectory, _parameter.DumpFileName);
        _parameter.InnerArguments = $"-e -ma {_parameter.ProcessNameOrId} {dmpFullName}";
        base.Launch();
        ExecuteFinalTreatment();
    }

    private string GetAppName() => RuntimeInformation.OSArchitecture switch
    {
        Architecture.X86 => "procdump.exe",
        Architecture.X64 => "procdump64.exe",
        _ => "procdump64a.exe"
    };

    private void ExecuteFinalTreatment()
    {
        var dumpFile = Path.Combine(_parameter.FailDirectory, _parameter.DumpFileName);
        if (File.Exists(dumpFile))
        {
            foreach (var action in _actions)
            {
                action.Invoke();
            }
        }
    }

    private void InitializeActions()
    {
        _actions.Add(CreateCrash);
        _actions.Add(Export);
        _actions.Add(Restore);
        _actions.Add(SetEnvironment);
    }

    /// <summary>
    /// Export the crash output information from procdump.exe and the monitoring parameters of Bowl.
    /// </summary>
    private void CreateCrash()
    {
        var crash = new Crash
        {
            Parameter = _parameter,
            ProcdumpOutPutLines = OutputList
        };
        var failJsonPath = Path.Combine(_parameter.FailDirectory, _parameter.FailFileName);
        StorageManager.CreateJson(failJsonPath, crash, CrashJsonContext.Default.Crash);
    }

    /// <summary>
    /// Export operating system information, system logs, and system driver information.
    /// </summary>
    private void Export()
    {
        var batPath = Path.Combine(_applicationsDirectory, "export.bat");
        if(!File.Exists(batPath))
            throw new FileNotFoundException("export.bat not found!");
        
        Process.Start(batPath, _parameter.FailDirectory);
    }

    /// <summary>
    /// Within the GeneralUpdate upgrade system, restore the specified backup version files to the current working directory.
    /// </summary>
    private void Restore()
    {
        if (string.Equals(_parameter.WorkModel, WorkModel))
            StorageManager.Restore(_parameter.BackupDirectory, _parameter.TargetPath);
    }

    /// <summary>
    /// Write the failed update version number to the local environment variable.
    /// </summary>
    private void SetEnvironment()
    {
        if (!string.Equals(_parameter.WorkModel, WorkModel))
            return;
     
        /*
         * The `UpgradeFail` environment variable is used to mark an exception version number during updates.
         * If the latest version number obtained via an HTTP request is less than or equal to the exception version number, the update is skipped.
         * Once this version number is set, it will not be removed, and updates will not proceed until a version greater than the exception version number is obtained through the HTTP request.
         */
        Environment.SetEnvironmentVariable("UpgradeFail", _parameter.ExtendedField, EnvironmentVariableTarget.User);
    }
}