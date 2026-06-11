using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Bowl;

namespace GeneralUpdate.Bowl.Strategies;

/// <summary>
/// Async wrapper for running a child process with stdout/stderr capture and a timeout.
/// </summary>
internal static class ProcessRunner
{
    /// <summary>
    /// Starts the process described by <paramref name="startInfo"/>, captures output lines,
    /// and waits for exit or timeout.
    /// </summary>
    /// <param name="startInfo">Process start configuration.</param>
    /// <param name="timeoutMs">Maximum wait time in milliseconds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exit code and captured output lines.</returns>
    public static async Task<ProcessExitResult> RunAsync(
        ProcessStartInfo startInfo, int timeoutMs, CancellationToken ct = default)
    {
        GeneralTracer.Info($"ProcessRunner.RunAsync: starting '{startInfo.FileName} {startInfo.Arguments}'");

        var outputLines = new List<string>();

        using var process = new Process { StartInfo = startInfo };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                GeneralTracer.Debug($"ProcessRunner: {e.Data}");
                lock (outputLines) outputLines.Add(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                GeneralTracer.Debug($"ProcessRunner(stderr): {e.Data}");
                lock (outputLines) outputLines.Add(e.Data);
            }
        };

        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

        if (!process.Start())
        {
            GeneralTracer.Fatal("ProcessRunner.RunAsync: failed to start process.");
            throw new InvalidOperationException("Failed to start process.");
        }

        GeneralTracer.Info($"ProcessRunner.RunAsync: process started, PID={process.Id}");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for exit or timeout/cancellation.
        // Cancel the delay when the process exits first to avoid a timer leak.
        var delayTask = Task.Delay(timeoutMs, timeoutCts.Token);
        var completedTask = await Task.WhenAny(tcs.Task, delayTask);

        // Cancel the opposing task so timers/resources are reclaimed promptly.
        timeoutCts.Cancel();

        if (completedTask == tcs.Task)
        {
            try { await delayTask; } catch (OperationCanceledException) { /* cancelled — expected */ }
            var exitCode = await tcs.Task;
            GeneralTracer.Info($"ProcessRunner.RunAsync: process exited, ExitCode={exitCode}");
            // Snapshot output under lock to avoid race with in-flight handlers
            IReadOnlyList<string> snapshot;
            lock (outputLines) { snapshot = outputLines.ToArray(); }
            return new ProcessExitResult { ExitCode = exitCode, OutputLines = snapshot };
        }

        // Timeout or cancellation — kill the process
        GeneralTracer.Warn($"ProcessRunner.RunAsync: process timed out after {timeoutMs}ms, killing.");
        try
        {
            process.Kill();
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("ProcessRunner.RunAsync: error killing process.", ex);
        }

        ct.ThrowIfCancellationRequested();
        throw new TimeoutException(
            $"Process '{startInfo.FileName}' did not exit within {timeoutMs}ms.");
    }
}
