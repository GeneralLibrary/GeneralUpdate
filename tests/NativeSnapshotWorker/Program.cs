using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GeneralUpdate.Core.Security;
using Microsoft.Data.Sqlite;

if (args.Length == 5 && args[0] == "--snapshot-owner-probe")
{
    var image = new WindowsSnapshotFile(args[2], SHA256.HashData(File.ReadAllBytes(args[2])));
    var dll = new WindowsSnapshotFile(args[3], SHA256.HashData(File.ReadAllBytes(args[3])));
    await using var lease = WindowsNativeLaunchLease.Start(
        WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot,
        args[1], image, new[] { dll }, new[] { "--snapshot-probe", args[4], "wait" });
    await Console.In.ReadLineAsync();
    return 0;
}
if (args.Length == 3 && args[0] == "--snapshot-probe")
{
    try
    {
        if (!Native.SetDefaultDllDirectories(0x00000200 | 0x00000800))
            throw new InvalidOperationException($"SetDefaultDllDirectories failed: {Marshal.GetLastWin32Error()}");
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var module = Native.GetModuleHandle("e_sqlite3.dll");
        if (module == IntPtr.Zero) throw new InvalidOperationException("SQLite native module not found.");
        var path = new StringBuilder(32768);
        if (Native.GetModuleFileName(module, path, (uint)path.Capacity) == 0)
            throw new InvalidOperationException($"GetModuleFileName failed: {Marshal.GetLastWin32Error()}");
        File.WriteAllLines(args[1], new[] { path.ToString(), Environment.ProcessId.ToString() });
        File.WriteAllText(args[1] + ".ready", "ready");
        if (args[2] == "wait") await Task.Delay(Timeout.InfiniteTimeSpan);
        return 0;
    }
    catch (Exception ex)
    {
        File.WriteAllText(args[1] + ".error", ex.ToString());
        return 91;
    }
}
if (args.Length == 4 && args[0] == "--handle-list-probe")
{
    File.WriteAllLines(args[3], new[] { Native.HandlePath(args[1]), Native.HandlePath(args[2]) });
    return 0;
}
if (args.Length >= 2 && args[0] == "--argument-probe")
{
    File.WriteAllLines(args[1], args.Skip(2).Select(value => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))));
    return 0;
}
if (args.Length == 4 && args[0] == "--snapshot-conflict-probe")
{
    var errors = new string[2];
    for (var i = 0; i < errors.Length; i++)
    {
        var original = args[i + 1];
        var replacement = original + ".moved";
        if (Native.MoveFileEx(original, replacement, 0))
        {
            if (!Native.MoveFileEx(replacement, original, 0))
                throw new InvalidOperationException($"Failed to restore moved object: {Marshal.GetLastWin32Error()}");
            errors[i] = "0";
        }
        else errors[i] = Marshal.GetLastWin32Error().ToString();
    }
    File.WriteAllLines(args[3], errors);
    return 0;
}
return 2;

internal static class Native
{
    internal static string HandlePath(string value)
    {
        var path = new StringBuilder(32768);
        var length = GetFinalPathNameByHandle(new IntPtr(long.Parse(value)), path, (uint)path.Capacity, 0);
        return length == 0 ? "" : path.ToString();
    }

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(IntPtr handle, StringBuilder path, uint size, uint flags);
    [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MoveFileEx(string existing, string target, uint flags);
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint GetModuleFileName(IntPtr module, StringBuilder path, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetDefaultDllDirectories(uint flags);
}
