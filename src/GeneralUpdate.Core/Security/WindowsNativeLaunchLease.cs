// The experiment does not yet verify the Windows system dependency closure.
#if NET8_0_OR_GREATER && WINDOWS_SNAPSHOT_LAUNCH_EXPERIMENT
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace GeneralUpdate.Core.Security;

/// <summary>
/// Owns a snapshot, a process handle and a kill-on-close job. This is not peer
/// authentication and does not provide a communication channel.
/// </summary>
public sealed class WindowsNativeLaunchLease : IAsyncDisposable
{
    private readonly SafeFileHandle _job;
    private readonly SafeFileHandle _process;
    private readonly WindowsImageSnapshotLease _snapshot;
    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private bool _disposed;

    private WindowsNativeLaunchLease(SafeFileHandle job, SafeFileHandle process, WindowsImageSnapshotLease snapshot)
    {
        _job = job;
        _process = process;
        _snapshot = snapshot;
    }

    public string SnapshotDirectory => _snapshot.DirectoryPath;

    /// <summary>
    /// Starts a host-authorized, cooperative Windows Native AOT peer. The hash identifies
    /// the peer and its trusted entry-point contract; this method does not inspect its code.
    /// The peer must limit DLL loading to its authorized snapshot and Windows system
    /// directories before handling sensitive input. Do not pass secrets in arguments.
    /// No handles are inherited. The working directory and executable directory are the
    /// snapshot, not the application installation directory.
    /// </summary>
    public static WindowsNativeLaunchLease Start(bool enabled, string snapshotParent,
        WindowsSnapshotFile image, System.Collections.Generic.IReadOnlyList<WindowsSnapshotFile> dependencies,
        System.Collections.Generic.IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows is required.");
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            throw new PlatformNotSupportedException("Windows 10 version 2004 or later is required.");
        if (arguments == null) throw new ArgumentNullException(nameof(arguments));
        return StartWindows(enabled, snapshotParent, image, dependencies, arguments, cancellationToken);
    }

    [SupportedOSPlatform("windows")]
    private static WindowsNativeLaunchLease StartWindows(bool enabled, string snapshotParent,
        WindowsSnapshotFile image, System.Collections.Generic.IReadOnlyList<WindowsSnapshotFile> dependencies,
        System.Collections.Generic.IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        foreach (var argument in arguments)
            if (argument == null || argument.IndexOf('\0') >= 0)
                throw new ArgumentException("Arguments must not contain null values or NUL characters.", nameof(arguments));
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = WindowsImageSnapshotLease.Create(enabled, snapshotParent, image, dependencies, cancellationToken);
        SafeFileHandle? job = null;
        try
        {
            job = CreateJobObject(IntPtr.Zero, null);
            if (job.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            var limits = new ExtendedLimitInformation
            {
                BasicLimitInformation = new BasicLimitInformation { LimitFlags = 0x2000 }
            };
            if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimitInformation>()))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            if (size == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            var list = Marshal.AllocHGlobal(size);
            var jobPointer = Marshal.AllocHGlobal(IntPtr.Size);
            var initialized = false;
            try
            {
                if (!InitializeProcThreadAttributeList(list, 1, 0, ref size))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                initialized = true;
                Marshal.WriteIntPtr(jobPointer, job.DangerousGetHandle());
                if (!UpdateProcThreadAttribute(list, 0, new IntPtr(0x0002000d), jobPointer,
                        new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var windows = new StringBuilder(32768);
                var length = GetWindowsDirectory(windows, (uint)windows.Capacity);
                if (length == 0 || length >= windows.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error());
                var system = Environment.SystemDirectory;
                if (string.IsNullOrEmpty(system) || !Path.IsPathFullyQualified(system))
                    throw new InvalidOperationException("The Windows system directory is unavailable.");
                var environment = "PATH=" + system + "\0SystemRoot=" + windows + "\0WINDIR=" + windows + "\0\0";
                var environmentPointer = Marshal.StringToHGlobalUni(environment);
                try
                {
                    var command = new StringBuilder(Quote(snapshot.ImagePath));
                    foreach (var argument in arguments) command.Append(' ').Append(Quote(argument));
                    var startup = new StartupInfoEx
                    {
                        StartupInfo = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>() },
                        AttributeList = list
                    };
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!CreateProcess(snapshot.ImagePath, command, IntPtr.Zero, IntPtr.Zero, false,
                            0x00080000 | 0x08000000 | 0x00000004 | 0x00000400, environmentPointer,
                            snapshot.DirectoryPath, ref startup, out var process))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    var resumed = false;
                    using var thread = new SafeFileHandle(process.Thread, true);
                    var processHandle = new SafeFileHandle(process.Process, true);
                    try
                    {
                        if (!IsProcessInJob(processHandle, job, out var assigned))
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        if (!assigned) throw new InvalidOperationException("The created process is not in the job.");
                        cancellationToken.ThrowIfCancellationRequested();
                        if (ResumeThread(thread) == uint.MaxValue)
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        resumed = true;
                        var result = new WindowsNativeLaunchLease(job, processHandle, snapshot);
                        job = null;
                        return result;
                    }
                    finally
                    {
                        if (!resumed)
                        {
                            if (!TerminateProcess(processHandle, 1))
                                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to terminate the suspended process.");
                            if (WaitForSingleObject(processHandle, uint.MaxValue) != 0)
                                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to wait for the suspended process.");
                            processHandle.Dispose();
                        }
                    }
                }
                finally { Marshal.FreeHGlobal(environmentPointer); }
            }
            finally
            {
                if (initialized) DeleteProcThreadAttributeList(list);
                Marshal.FreeHGlobal(jobPointer);
                Marshal.FreeHGlobal(list);
            }
        }
        catch
        {
            job?.Dispose();
            snapshot.Dispose();
            throw;
        }
    }

    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var c in value)
        {
            if (c == '\\') { slashes++; continue; }
            if (c == '"') result.Append('\\', slashes * 2 + 1);
            else result.Append('\\', slashes);
            result.Append(c);
            slashes = 0;
        }
        result.Append('\\', slashes * 2).Append('"');
        return result.ToString();
    }

    /// <summary>Waits for the owned process. Cancellation stops the job and releases the lease.</summary>
    public async Task<uint> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _disposeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(WindowsNativeLaunchLease));
                    var wait = WaitForSingleObject(_process, 0);
                    if (wait == 0)
                    {
                        if (!GetExitCodeProcess(_process, out var code)) throw new Win32Exception(Marshal.GetLastWin32Error());
                        return code;
                    }
                    if (wait != 258) throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                finally { _disposeLock.Release(); }
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Closes the job, waits for the process to exit, and releases snapshot handles.</summary>
    public async ValueTask DisposeAsync()
    {
        await _disposeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            if (!TerminateJobObject(_job, 1))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to terminate the owned job.");
            var result = await Task.Run(() =>
            {
                var wait = WaitForSingleObject(_process, uint.MaxValue);
                return (Wait: wait, Error: wait == 0 ? 0 : Marshal.GetLastWin32Error());
            }).ConfigureAwait(false);
            if (result.Wait != 0) throw new Win32Exception(result.Error, "Unable to wait for the terminated process.");
            while (true)
            {
                if (!QueryInformationJobObject(_job, 1, out var accounting,
                        (uint)Marshal.SizeOf<BasicAccountingInformation>(), IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to inspect the owned job.");
                if (accounting.ActiveProcesses == 0) break;
                await Task.Delay(50).ConfigureAwait(false);
            }
            _job.Dispose();
            _process.Dispose();
            _snapshot.Dispose();
            _disposed = true;
        }
        finally { _disposeLock.Release(); }
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
    private struct BasicAccountingInformation
    {
        public long TotalUserTime, TotalKernelTime, ThisPeriodTotalUserTime, ThisPeriodTotalKernelTime;
        public uint TotalPageFaultCount, TotalProcesses, ActiveProcesses, TotalTerminatedProcesses;
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
    private struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr AttributeList; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation { public IntPtr Process, Thread; public int ProcessId, ThreadId; }

    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref ExtendedLimitInformation information, uint length);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(SafeFileHandle job, int informationClass,
        out BasicAccountingInformation information, uint length, IntPtr returnLength);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, int flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previousValue, IntPtr returnSize);
    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", EntryPoint = "GetWindowsDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetWindowsDirectory(StringBuilder buffer, uint size);
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint flags, IntPtr environment,
        string currentDirectory, ref StartupInfoEx startup, out ProcessInformation process);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(SafeFileHandle process, SafeFileHandle job, [MarshalAs(UnmanagedType.Bool)] out bool result);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(SafeFileHandle thread);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(SafeFileHandle process, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeFileHandle handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(SafeFileHandle process, out uint exitCode);
}
#endif
