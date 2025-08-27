using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GeneralUpdate.Common.Shared;

public class WindowsOutputDebugListener : TraceListener
{
    /// <summary>
    /// Does not affect .NET AOT compilation and runtime on the Windows platform, provided that the following conditions are met:
    ///     The target platform is restricted to Windows (due to the dependency on kernel32.dll);
    ///     The function declaration is static (not dynamically generated).
    /// This syntax is safe in an AOT environment and can properly work with Dbgview.exe to capture logs.
    /// </summary>
    /// <param name="lpOutputString"></param>
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern void OutputDebugString(string lpOutputString);

    public override void Write(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            OutputDebugString(message);
        }
    }

    public override void WriteLine(string message)
    {
        Write($"{message}{Environment.NewLine}");
    }
}