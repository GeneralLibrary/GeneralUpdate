using System.Text.Json;
using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Strategys;
using GeneralUpdate.Common.Shared.Object;

namespace Generalupdate.CatBowl;

class Program
{
    static void Main(string[] args)
    {
        var processInfo = TestProcessInfo();
        Bowl.Launch(processInfo);
        Console.Read();
    }
    
    private static MonitorParameter TestProcessInfo()
    {
        var path = @"D:\packet\test.json";
        var json = File.ReadAllText(path);
        var processInfo = JsonSerializer.Deserialize<ProcessInfo>(json);
        return new MonitorParameter
        {
            ProcessNameOrId = processInfo.AppName,
            DumpFileName = $"{processInfo.LastVersion}_fail.dmp",
            FailFileName = $"{processInfo.LastVersion}_fail.json",
            TargetPath = processInfo.InstallPath,
            FailDirectory = Path.Combine(processInfo.InstallPath, "fail", processInfo.LastVersion),
            BackupDirectory = Path.Combine(processInfo.InstallPath, processInfo.LastVersion),
            WorkModel = "Normal"
        };
    }
}