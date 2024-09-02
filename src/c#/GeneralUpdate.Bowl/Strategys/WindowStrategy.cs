using System.Runtime.InteropServices;

namespace GeneralUpdate.Bowl.Strategys;

public class WindowStrategy : AbstractStrategy
{
    public override void Launch()
    {
        _parameter.InnerAppName = GetAppName();
        base.Launch();
    }

    private string GetAppName()
    {
        string appName = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X86 => "procdump.exe",
            Architecture.X64 => "procdump64.exe",
            _ => "procdump64a.exe"
        };
        return appName;
    }
}