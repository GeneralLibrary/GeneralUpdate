using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CoreTest.SelfCheck;

/// <summary>Which stub behavior a self-check test wants from the verification launch.</summary>
internal enum StubVerdict
{
    /// <summary>Stays alive for the whole verification window (usable version).</summary>
    Healthy,

    /// <summary>Exits immediately with a non-zero code (unusable version).</summary>
    Unusable
}

/// <summary>
///     Real child-process stubs for the self-check tests. Every launcher builds its
///     ProcessStartInfo from compile-time literal strings only — no test-controlled
///     string reaches the spawned process — so each stub command is fixed at compile time.
///     Processes are spawned via the Process instance Start() member rather than the
///     static Start overloads of Process.
/// </summary>
internal static class StubProcesses
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>Starts the stub matching the requested verdict (shared with the switch-matrix tests).</summary>
    internal static Process? Launch(StubVerdict verdict) => verdict switch
    {
        StubVerdict.Unusable => ImmediateFail(),
        _ => StayAlive()
    };

    /// <summary>Exits immediately with code 3 — simulates an app that dies on startup.</summary>
    internal static Process? ImmediateFail() => IsWindows
        ? Spawn("cmd.exe", "/c exit 3")
        : Spawn("/bin/sh", "-c \"exit 3\"");

    /// <summary>Exits immediately with code 0 — clean success stub.</summary>
    internal static Process? ImmediateOk() => IsWindows
        ? Spawn("cmd.exe", "/c exit 0")
        : Spawn("/bin/sh", "-c \"exit 0\"");

    /// <summary>Stays alive for roughly a minute — simulates a running application.</summary>
    internal static Process? StayAlive() => IsWindows
        ? Spawn("cmd.exe", "/c ping -n 60 127.0.0.1 > nul")
        : Spawn("/bin/sh", "-c \"sleep 60\"");

    private static Process? Spawn(string fileName, string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments) { UseShellExecute = false }
        };
        return process.Start() ? process : null;
    }
}
