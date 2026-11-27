using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace CoreTest.Ipc;

/// <summary>Experiments with handle inheritance, not a protected updater launcher.</summary>
public sealed class WindowsRestrictedInheritanceExperimentTests
{
    [Fact]
    public void ExplicitHandleListExcludesOtherInheritableHandles()
    {
        if (!OperatingSystem.IsWindows()) return;
        RunExperiment();
    }

    [SupportedOSPlatform("windows")]
    private static void RunExperiment()
    {
        var root = Path.Combine(Path.GetTempPath(), "gu-handle-list-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var allowedPath = Path.Combine(root, "allowed.txt");
            var rejectedPath = Path.Combine(root, "rejected.txt");
            var resultPath = Path.Combine(root, "result.txt");
            File.WriteAllText(allowedPath, "allowed");
            File.WriteAllText(rejectedPath, "rejected");
            var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), InheritHandle = true };
            using var allowed = CreateFile(allowedPath, 0x80000000, 7, ref security, 3, 0x80, IntPtr.Zero);
            if (allowed.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            using var rejected = CreateFile(rejectedPath, 0x80000000, 7, ref security, 3, 0x80, IntPtr.Zero);
            if (rejected.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());

            var script = """
                         $ErrorActionPreference = 'Stop'
                         Add-Type @'
                         using System;
                         using System.Runtime.InteropServices;
                         using System.Text;
                         public static class InheritedHandleProbe {
                             [DllImport("kernel32.dll", EntryPoint="GetFinalPathNameByHandleW", CharSet=CharSet.Unicode, SetLastError=true)]
                             public static extern uint GetPath(IntPtr handle, StringBuilder path, uint size, uint flags);
                         }
                         '@
                         function Get-HandlePath($value) {
                             $path = New-Object Text.StringBuilder 32768
                             $length = [InheritedHandleProbe]::GetPath([IntPtr][long]$value, $path, 32768, 0)
                             if ($length -eq 0) { return '' }
                             return $path.ToString()
                         }
                         $allowed = Get-HandlePath $args[0]
                         $rejected = Get-HandlePath $args[1]
                         [IO.File]::WriteAllLines($args[2], @($allowed, $rejected))
                         """;
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(
                "& { " + script + " } '" + allowed.DangerousGetHandle().ToInt64() + "' '" +
                rejected.DangerousGetHandle().ToInt64() + "' '" + resultPath.Replace("'", "''") + "'"));
            var nativePeer = Environment.GetEnvironmentVariable("GU_WINDOWS_NATIVE_LOCK_PEER");
            var executable = nativePeer ?? Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            var arguments = nativePeer == null
                ? "-NoProfile -NonInteractive -EncodedCommand " + encoded
                : "--handle-list-probe " + allowed.DangerousGetHandle().ToInt64() + " " +
                  rejected.DangerousGetHandle().ToInt64() + " \"" + resultPath + "\"";
            var commandLine = new StringBuilder("\"" + executable + "\" " + arguments);

            var size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            if (size == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            var list = Marshal.AllocHGlobal(size);
            var handles = Marshal.AllocHGlobal(IntPtr.Size);
            var initialized = false;
            try
            {
                if (!InitializeProcThreadAttributeList(list, 1, 0, ref size))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                initialized = true;
                Marshal.WriteIntPtr(handles, allowed.DangerousGetHandle());
                if (!UpdateProcThreadAttribute(list, 0, new IntPtr(0x00020002), handles,
                        new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var startup = new StartupInfoEx
                {
                    StartupInfo = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>() },
                    AttributeList = list
                };
                if (!CreateProcess(executable, commandLine, IntPtr.Zero, IntPtr.Zero, true,
                        0x00080000 | 0x08000000, IntPtr.Zero, root, ref startup, out var process))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                try
                {
                    var wait = WaitForSingleObject(process.Process, 30000);
                    if (wait != 0)
                    {
                        TerminateProcess(process.Process, 1);
                        throw new InvalidOperationException($"Worker did not exit: wait result {wait}.");
                    }
                    if (!GetExitCodeProcess(process.Process, out var exitCode))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    Assert.Equal(0u, exitCode);
                }
                finally
                {
                    CloseHandle(process.Thread);
                    CloseHandle(process.Process);
                }
            }
            finally
            {
                if (initialized) DeleteProcThreadAttributeList(list);
                Marshal.FreeHGlobal(handles);
                Marshal.FreeHGlobal(list);
            }

            var lines = File.ReadAllLines(resultPath);
            Assert.Equal(2, lines.Length);
            Assert.EndsWith(allowedPath, lines[0], StringComparison.OrdinalIgnoreCase);
            Assert.False(lines[1].EndsWith(rejectedPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)] public bool InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string? Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public short ShowWindow, Reserved2;
        public IntPtr ReservedPointer, StandardInput, StandardOutput, StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process, Thread;
        public int ProcessId, ThreadId;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, ref SecurityAttributes security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, int flags, IntPtr attribute, IntPtr value,
        IntPtr size, IntPtr previousValue, IntPtr returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string application, StringBuilder commandLine, IntPtr processAttributes,
        IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint flags, IntPtr environment,
        string currentDirectory, ref StartupInfoEx startup, out ProcessInformation process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
