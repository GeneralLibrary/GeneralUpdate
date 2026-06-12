using System.Diagnostics;
using System.Threading.Tasks;

namespace GeneralUpdate.Core;

/// <summary>Process lifecycle utilities replacing raw Process.Kill().</summary>
public static class GracefulExit
{
    /// <summary>
    /// Attempt a graceful shutdown via CloseMainWindow(), fall back to Kill() after timeout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Process.CloseMainWindow"/> only works for GUI processes with a main window.
    /// For console applications or headless processes, it returns <c>false</c> immediately.
    /// In that case the method waits <paramref name="timeoutMs"/> and then falls back to
    /// <see cref="Process.Kill"/> as a last resort.
    /// </para>
    /// <para>
    /// When <c>CloseMainWindow</c> succeeds (returns <c>true</c>), the close message was
    /// sent to the main window's message queue. The process is given <paramref name="timeoutMs"/>
    /// to exit before falling back to <c>Kill</c>.
    /// </para>
    /// </remarks>
    public static async Task ShutdownAsync(Process? process, int timeoutMs = 3000)
    {
        if (process == null || process.HasExited) return;
        if (process.CloseMainWindow())
        {
            // CloseMainWindow sent a WM_CLOSE — give the process time to shut down.
            await Task.Delay(timeoutMs).ConfigureAwait(false);
        }
        else
        {
            // Process has no main window (e.g. console app) — still give it time.
            await Task.Delay(timeoutMs).ConfigureAwait(false);
        }
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
