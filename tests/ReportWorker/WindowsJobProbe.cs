using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ReportWorker;

internal static class WindowsJobProbe
{
    internal static async Task<int> RunAsync(string executable, string marker, bool invalidJob = false)
    {
        using var job = CreateJobObject(IntPtr.Zero, null);
        if (job.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var limits = new ExtendedLimitInformation
        {
            BasicLimitInformation = new BasicLimitInformation { LimitFlags = 0x00002000 }
        };
        if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimitInformation>()))
            throw new Win32Exception(Marshal.GetLastWin32Error());

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
            Marshal.WriteIntPtr(handles, invalidJob ? IntPtr.Zero : job.DangerousGetHandle());
            if (!UpdateProcThreadAttribute(list, 0, new IntPtr(0x0002000d), handles,
                    new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                if (!invalidJob) throw new Win32Exception(error);
                Console.WriteLine($"rejected:{error}");
                return 0;
            }
            var startup = new StartupInfoEx
            {
                StartupInfo = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>() },
                AttributeList = list
            };
            var command = new StringBuilder("\"" + executable + "\" --job-child-probe \"" + marker + "\"");
            if (!CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, false,
                    0x00080000 | 0x08000000 | 0x00000004, IntPtr.Zero, null, ref startup, out var process))
            {
                var error = Marshal.GetLastWin32Error();
                if (!invalidJob) throw new Win32Exception(error);
                Console.WriteLine($"rejected:{error}");
                return 0;
            }
            var resumed = false;
            try
            {
                if (invalidJob) throw new InvalidOperationException("An invalid job unexpectedly allowed process creation.");
                if (!IsProcessInJob(process.Process, job, out var assigned))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!assigned) throw new InvalidOperationException("The child is not in the job.");
                if (ResumeThread(process.Thread) == uint.MaxValue)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                resumed = true;
                Console.WriteLine(process.ProcessId);
                await Console.Out.FlushAsync();
                await Console.In.ReadLineAsync();
                return 0;
            }
            finally
            {
                if (!resumed) TerminateProcess(process.Process, 1);
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
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation
    {
        public BasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
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

    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass,
        ref ExtendedLimitInformation information, uint length);

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
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint flags, IntPtr environment,
        string? currentDirectory, ref StartupInfoEx startup, out ProcessInformation process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(IntPtr process, SafeFileHandle job, [MarshalAs(UnmanagedType.Bool)] out bool result);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);
}
