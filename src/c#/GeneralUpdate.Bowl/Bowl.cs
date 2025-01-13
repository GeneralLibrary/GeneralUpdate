using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using GeneralUpdate.Bowl.Strategys;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Bowl;

/// <summary>
/// Surveillance Main Program.
/// </summary>
public sealed class Bowl
{
    private static IStrategy? _strategy;

    private Bowl() { }

    private static void CreateStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            _strategy = new WindowStrategy();
        
        if (_strategy == null)
            throw new PlatformNotSupportedException("Unsupported operating system");
    }
    
    public static void Launch(MonitorParameter? monitorParameter = null)
    {
        monitorParameter ??= CreateParameter();
        CreateStrategy();
        _strategy?.SetParameter(monitorParameter);
        _strategy?.Launch();
    }

    private static MonitorParameter CreateParameter()
    {
        var json = Environments.GetEnvironmentVariable("ProcessInfo");
        if(string.IsNullOrWhiteSpace(json))
            throw new ArgumentNullException("ProcessInfo environment variable not set !"); 
        
        var processInfo = JsonSerializer.Deserialize<ProcessInfo>(json, ProcessInfoJsonContext.Default.ProcessInfo);
        if(processInfo == null)
            throw new ArgumentNullException("ProcessInfo json deserialize fail!");
        
        return new MonitorParameter
        {
            ProcessNameOrId = processInfo.AppName,
            DumpFileName = $"{processInfo.LastVersion}_fail.dmp",
            FailFileName = $"{processInfo.LastVersion}_fail.json",
            TargetPath = processInfo.InstallPath,
            FailDirectory = Path.Combine(processInfo.InstallPath, "fail", processInfo.LastVersion),
            BackupDirectory = Path.Combine(processInfo.InstallPath, processInfo.LastVersion),
            ExtendedField = processInfo.LastVersion
        };
    }
}