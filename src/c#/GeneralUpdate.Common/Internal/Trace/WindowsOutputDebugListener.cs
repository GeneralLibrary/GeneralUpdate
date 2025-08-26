using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GeneralUpdate.Common.Internal;

public class WindowsOutputDebugListener : TraceListener
{
    // 声明Win32 API的OutputDebugString函数
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern void OutputDebugString(string lpOutputString);

    // 重写Write方法（处理无换行的输出）
    public override void Write(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            OutputDebugString(message);
        }
    }

    // 重写WriteLine方法（处理带换行的输出）
    public override void WriteLine(string message)
    {
        // 附加换行符，保持与Trace.WriteLine一致的行为
        Write($"{message}{Environment.NewLine}");
    }
}