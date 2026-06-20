using System;
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

    /// <summary>Exit the current process gracefully.</summary>
    /// <remarks>
    /// <para>
    /// For external processes, <see cref="ShutdownAsync"/> sends WM_CLOSE then waits.
    /// For self-shutdown (the current process), calling <c>CloseMainWindow</c> + <c>Kill</c>
    /// on oneself is harmful — <c>Kill</c> skips finally blocks, <c>CloseMainWindow</c> is a
    /// no-op for console apps, and the 3-second wait is wasted.
    /// </para>
    /// <para>
    /// Instead, this method signals the process to exit naturally.
    /// Callers must dispose their own resources (tracer, etc.) before calling this method.
    /// </para>
    /// </remarks>
    public static Task CurrentProcessAsync(int timeoutMs = 3000)
    {
        try
        {
            // Signal GUI windows to close. For console/background processes this is
            // a no-op, but the process will exit when the async call stack unwinds.
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                using var p = Process.GetCurrentProcess();
                if (!p.HasExited)
                    p.CloseMainWindow();
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exiting — nothing to do.
        }

        // The process exits naturally when the async call stack completes.
        // Callers should have already disposed critical resources (tracer, etc.).
        return Task.CompletedTask;
    }
}
