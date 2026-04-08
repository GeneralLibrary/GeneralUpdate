using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Shared;

namespace GeneralUpdate.Bowl.Strategys;

internal abstract class AbstractStrategy : IStrategy
{
    protected MonitorParameter _parameter;
    protected List<string> OutputList = new ();

    public void SetParameter(MonitorParameter parameter) => _parameter = parameter;
    
    public virtual void Launch()
    {
        GeneralTracer.Info($"AbstractStrategy.Launch: starting inner application. App={_parameter.InnerApp}, Args={_parameter.InnerArguments}");
        Startup(_parameter.InnerApp, _parameter.InnerArguments);
        GeneralTracer.Info("AbstractStrategy.Launch: inner application process finished.");
    }
    
    private void Startup(string appName, string arguments)
    {
        GeneralTracer.Info($"AbstractStrategy.Startup: preparing process. FileName={appName}");
        if (Directory.Exists(_parameter.FailDirectory))
        {
            GeneralTracer.Info($"AbstractStrategy.Startup: removing existing fail directory: {_parameter.FailDirectory}");
            StorageManager.DeleteDirectory(_parameter.FailDirectory);
        }
        Directory.CreateDirectory(_parameter.FailDirectory);
        GeneralTracer.Info($"AbstractStrategy.Startup: fail directory created: {_parameter.FailDirectory}");

        var startInfo = new ProcessStartInfo
        {
            FileName = appName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += OutputHandler;
        process.ErrorDataReceived += OutputHandler;
        process.Start();
        GeneralTracer.Info($"AbstractStrategy.Startup: process started. PID={process.Id}");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit(1000 * 10);
        GeneralTracer.Info($"AbstractStrategy.Startup: process exited. ExitCode={process.ExitCode}");
    }
    
    private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
    {
        var data = outLine.Data;
        if (!string.IsNullOrEmpty(data))
        {
            GeneralTracer.Debug($"AbstractStrategy.OutputHandler: {data}");
            OutputList.Add(data);
        }
    }
}