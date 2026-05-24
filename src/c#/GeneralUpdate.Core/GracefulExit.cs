using System.Diagnostics;
using System.Threading.Tasks;

namespace GeneralUpdate.Core;

/// <summary>Process lifecycle utilities replacing raw Process.Kill().</summary>
public static class GracefulExit
{
    /// <summary>
    /// Attempt a graceful shutdown via CloseMainWindow(), fall back to Kill() after timeout.
    /// </summary>
    public static async Task ShutdownAsync(Process? process, int timeoutMs = 3000)
    {
        if (process == null || process.HasExited) return;
        if (!process.CloseMainWindow())
            await Task.Delay(timeoutMs).ConfigureAwait(false);
        if (!process.HasExited)
            process.Kill(); // Last resort
    }

    /// <summary>Shutdown the current process gracefully.</summary>
    public static async Task CurrentProcessAsync(int timeoutMs = 3000)
    {
        var p = Process.GetCurrentProcess();
        await ShutdownAsync(p, timeoutMs).ConfigureAwait(false);
    }
}
