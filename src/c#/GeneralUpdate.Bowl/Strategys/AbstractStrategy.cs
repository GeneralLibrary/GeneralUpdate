using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using GeneralUpdate.Common.FileBasic;

namespace GeneralUpdate.Bowl.Strategys;

internal abstract class AbstractStrategy : IStrategy
{
    protected MonitorParameter _parameter;
    protected List<string> OutputList = new ();

    public void SetParameter(MonitorParameter parameter) => _parameter = parameter;
    
    public virtual void Launch()
    {
        Startup(_parameter.InnerApp, _parameter.InnerArguments);
    }
    
    private void Startup(string appName, string arguments)
    {
        if (Directory.Exists(_parameter.FailDirectory))
        {
            StorageManager.DeleteDirectory(_parameter.FailDirectory);
        }
        Directory.CreateDirectory(_parameter.FailDirectory);
        
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
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit(1000 * 10);
    }
    
    private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
    {
        var data = outLine.Data;
        if (!string.IsNullOrEmpty(data))
            OutputList.Add(data);
    }
}