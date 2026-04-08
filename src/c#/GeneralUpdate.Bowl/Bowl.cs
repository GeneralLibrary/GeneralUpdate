using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using GeneralUpdate.Bowl.Strategys;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Common.Shared;
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
        GeneralTracer.Info("Bowl.CreateStrategy: detecting current OS platform.");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            GeneralTracer.Info("Bowl.CreateStrategy: Windows platform detected, creating WindowStrategy.");
            _strategy = new WindowStrategy();
        }

        if (_strategy == null)
        {
            GeneralTracer.Fatal("Bowl.CreateStrategy: unsupported operating system, no strategy created.");
            throw new PlatformNotSupportedException("Unsupported operating system");
        }

        GeneralTracer.Info("Bowl.CreateStrategy: strategy created successfully.");
    }

    public static void Launch(MonitorParameter? monitorParameter = null)
    {
        GeneralTracer.Info("Bowl.Launch: starting surveillance launch.");
        try
        {
            monitorParameter ??= CreateParameter();
            GeneralTracer.Info($"Bowl.Launch: monitor parameter resolved. ProcessNameOrId={monitorParameter.ProcessNameOrId}, TargetPath={monitorParameter.TargetPath}");
            CreateStrategy();
            _strategy?.SetParameter(monitorParameter);
            GeneralTracer.Info("Bowl.Launch: strategy parameter set, invoking Launch.");
            _strategy?.Launch();
            GeneralTracer.Info("Bowl.Launch: strategy Launch completed.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("Bowl.Launch: exception occurred during surveillance launch.", ex);
            throw;
        }
    }

    private static MonitorParameter CreateParameter()
    {
        GeneralTracer.Info("Bowl.CreateParameter: reading ProcessInfo from environment variable.");
        var json = Environments.GetEnvironmentVariable("ProcessInfo");
        if (string.IsNullOrWhiteSpace(json))
        {
            GeneralTracer.Fatal("Bowl.CreateParameter: ProcessInfo environment variable is not set.");
            throw new ArgumentNullException("ProcessInfo environment variable not set !");
        }

        var processInfo = JsonSerializer.Deserialize<ProcessInfo>(json, ProcessInfoJsonContext.Default.ProcessInfo);
        if (processInfo == null)
        {
            GeneralTracer.Fatal("Bowl.CreateParameter: failed to deserialize ProcessInfo JSON.");
            throw new ArgumentNullException("ProcessInfo json deserialize fail!");
        }

        GeneralTracer.Info($"Bowl.CreateParameter: ProcessInfo deserialized successfully. AppName={processInfo.AppName}, LastVersion={processInfo.LastVersion}");
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