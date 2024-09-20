using System;
using System.Runtime.InteropServices;
using GeneralUpdate.Bowl.Strategys;

namespace GeneralUpdate.Bowl;

/// <summary>
/// Surveillance Main Program.
/// </summary>
public class Bowl
{
    private IStrategy _strategy;

    public Bowl(MonitorParameter parameter = null)
    {
        CreateStrategy();
        _strategy!.SetParameter(parameter);
    }

    private void CreateStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _strategy = new WindowStrategy();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _strategy = new LinuxStrategy();
        }
    }

    public Bowl SetParameter(MonitorParameter parameter)
    {
        if(parameter.Verify())
            throw new ArgumentException("Parameter contains illegal values");
        
        _strategy.SetParameter(parameter);
        return this;
    }

    public void Launch() => _strategy.Launch();
}