using System;
using System.Runtime.InteropServices;
using GeneralUpdate.Bowl.Strategys;

namespace GeneralUpdate.Bowl;

/// <summary>
/// Surveillance Main Program.
/// </summary>
public sealed class Bowl
{
    private IStrategy _strategy;

    public Bowl(MonitorParameter parameter = null)
    {
        CreateStrategy();
        _strategy!.SetParameter(parameter);
    }

    private Bowl CreateStrategy()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _strategy = new WindowStrategy();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _strategy = new LinuxStrategy();
        }
        
        if (_strategy == null)
            throw new PlatformNotSupportedException("Unsupported operating system");
        
        return this;
    }

    public Bowl SetParameter(MonitorParameter parameter)
    {
        if(parameter.Verify())
            throw new ArgumentException("Parameter contains illegal values");
        
        _strategy.SetParameter(parameter);
        return this;
    }

    public Bowl Launch()
    {
        _strategy.Launch();
        return this;
    }
}