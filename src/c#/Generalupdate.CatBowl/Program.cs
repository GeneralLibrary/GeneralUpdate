using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Strategys;

namespace Generalupdate.CatBowl;

class Program
{
    static void Main(string[] args)
    {
        var installPath = @"D:\github_project\GeneralUpdate\src\c#\GeneralUpdate.CatBowl\bin\Debug\net8.0\";
        var lastVersion = "1.0.0.3";
        var processInfo = new MonitorParameter
        {
            ProcessNameOrId = "JsonTest.exe",
            DumpFileName = $"{lastVersion}_fail.dmp",
            FailFileName = $"{lastVersion}_fail.json",
            TargetPath = installPath,
            FailDirectory = Path.Combine(installPath, "fail", lastVersion),
            BackupDirectory = Path.Combine(installPath, lastVersion),
            WorkModel = "Normal"
        };
        Bowl.Launch(processInfo);
        Console.Read();
    }
}