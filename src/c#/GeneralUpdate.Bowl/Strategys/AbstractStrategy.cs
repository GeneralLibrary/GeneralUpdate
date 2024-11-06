using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace GeneralUpdate.Bowl.Strategys;

public abstract class AbstractStrategy : IStrategy
{
    protected MonitorParameter _parameter;
    
    private readonly IReadOnlyList<string> _sensitiveCharacter = new List<string>
    {
        "Exit",
        "exit",
        "EXIT"
    };
    
    public virtual void Launch()
    {
        Backup();
        Startup(_parameter.ProcessNameOrId, _parameter.InnerArguments);
    }
    
    private void Startup(string appName, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = appName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += OutputHandler;
        process.ErrorDataReceived += OutputHandler;
        process.Start();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
    
    private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
    {
        var data = outLine.Data;
        if (!string.IsNullOrEmpty(data))
        {
            foreach (var sensitive in _sensitiveCharacter)
            {
                if (data.Contains(sensitive)){
                    Restore();
                    Process.Start(_parameter.ProcessNameOrId, _parameter.Arguments);
                    break;
                }
            }
        }
    }

    private void Backup()
    {
        var backupPath = _parameter.Target;
        var sourcePath = _parameter.Source;
        
        if (Directory.Exists(backupPath))
        {
            Directory.Delete(backupPath, true);
        }

        Directory.CreateDirectory(backupPath);

        foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dirPath.Replace(sourcePath, backupPath));
        }

        foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
        {
            File.Copy(newPath, newPath.Replace(sourcePath, backupPath), true);
        }
    }

    private void Restore()
    {
        var restorePath = _parameter.Target;
        var backupPath = _parameter.Source;
        
        if (Directory.Exists(restorePath))
        {
            Directory.Delete(restorePath, true);
        }

        Directory.CreateDirectory(restorePath);

        foreach (string dirPath in Directory.GetDirectories(backupPath, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dirPath.Replace(backupPath, restorePath));
        }

        foreach (string newPath in Directory.GetFiles(backupPath, "*.*", SearchOption.AllDirectories))
        {
            File.Copy(newPath, newPath.Replace(backupPath, restorePath), true);
        }
    }

    public void SetParameter(MonitorParameter parameter) => _parameter = parameter;
}