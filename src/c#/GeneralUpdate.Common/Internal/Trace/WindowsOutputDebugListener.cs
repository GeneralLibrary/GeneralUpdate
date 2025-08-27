using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GeneralUpdate.Common.Internal;

public class WindowsOutputDebugListener : TraceListener
{
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