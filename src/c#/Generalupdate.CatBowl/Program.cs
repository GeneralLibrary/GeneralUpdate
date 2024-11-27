using System.Text;
using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Strategys;
using GeneralUpdate.Common.Compress;
using GeneralUpdate.Common.Shared.Object;

namespace Generalupdate.CatBowl;

class Program
{
    static void Main(string[] args)
    {
        /*var installPath = @"D:\github_project\GeneralUpdate\src\c#\GeneralUpdate.CatBowl\bin\Debug\net8.0\";
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
        Bowl.Launch(processInfo);*/
        
        /*var source = @"D:\packet\release";
        var target = @"D:\packet\1.zip";
        CompressProvider.Compress(Format.ZIP,source,target, false, Encoding.UTF8);
        CompressProvider.Decompress(Format.ZIP,target,source, Encoding.UTF8);
        Console.WriteLine($"Done {File.Exists(target)}");*/
        
        Console.Read();
    }
}