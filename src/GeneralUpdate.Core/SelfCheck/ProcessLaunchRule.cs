using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.SelfCheck;

/// <summary>
///     Core self-check rule: "can the application start and survive".
/// </summary>
/// <remarks>
///     <para>
///         The rule observes one process for the configured window (<c>SelfCheckStartupWindowSeconds</c>,
///         default 30s) and renders the verdict:
///     </para>
///     <list type="bullet">
///         <item>process exits with code 0 → pass (CLI-style main programs end normally; recorded in the detail).</item>
///         <item>process exits with a non-zero code → fail (crashed on startup).</item>
///         <item>process still alive when the window elapses → pass under the default lenient policy.</item>
///         <item>strict mode (<c>RequireAck</c>): a verify-fail file fails immediately; the window
///             elapsing without a verify-ok file fails as "not confirmed = unusable".</item>
///     </list>
///     <para>
///         The window uses a <c>HasExited</c> polling loop rather than <c>Process.WaitForExitAsync</c>
///         so the rule also works on <c>netstandard2.0</c> (the polyfill recommended by the design doc).
///         A failed verdict shuts the residual process down via <see cref="GracefulExit.ShutdownAsync"/>
///         before returning, so the caller never leaks a half-started application.
///     </para>
/// </remarks>
public sealed class ProcessLaunchRule : ISelfCheckRule
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly Process? _process;
    private readonly TimeSpan _window;
    private readonly bool _requireAck;

    /// <inheritdoc />
    public string Name => "ProcessLaunch";

    /// <summary>
    ///     Creates the rule. When <paramref name="process"/> is supplied (the normal updater flow —
    ///     the strategy has already started the application through its own path resolution), the
    ///     rule only observes it. When <c>null</c>, the rule starts <c>InstallPath/AppName</c>
    ///     itself so the rule stays usable in isolation (tests, client-side tooling).
    /// </summary>
    public ProcessLaunchRule(Process? process, TimeSpan window, bool requireAck)
    {
        _process = process;
        _window = window <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : window;
        _requireAck = requireAck;
    }

    /// <inheritdoc />
    public async Task<SelfCheckRuleResult> CheckAsync(SelfCheckContext ctx, CancellationToken ct)
    {
        var process = _process;
        if (process == null)
        {
            var appPath = Path.Combine(ctx.InstallPath ?? string.Empty, ctx.AppName ?? string.Empty);
            if (!File.Exists(appPath))
                return await FailAsync(null, $"executable not found: {appPath}").ConfigureAwait(false);

            try
            {
                process = Process.Start(new ProcessStartInfo(appPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                return await FailAsync(null, $"process start failed: {ex.Message}").ConfigureAwait(false);
            }

            if (process == null)
                return await FailAsync(null, $"process start returned null: {appPath}").ConfigureAwait(false);
        }

        GeneralTracer.Info(
            $"ProcessLaunchRule: observing PID {process.Id} for {_window.TotalSeconds:0}s (requireAck={_requireAck}).");

        var deadline = DateTime.UtcNow + _window;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (process.HasExited)
                return VerdictByExitCode(process);

            if (_requireAck)
            {
                var ack = UpdateVerificationMarker.TryReadAck(ctx.InstallPath, out var reason);
                if (ack == VerificationAckState.Fail)
                    return await FailAsync(process, $"application reported unhealthy: {reason}").ConfigureAwait(false);
                if (ack == VerificationAckState.Ok)
                    return Pass("application acknowledged healthy (verify-ok)");
            }

            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }

        // Window elapsed — settle the final race (the process may have exited while delaying).
        if (process.HasExited)
            return VerdictByExitCode(process);

        if (_requireAck)
            return await FailAsync(process, "observation window elapsed without verify-ok acknowledgement")
                .ConfigureAwait(false);

        return Pass($"process alive after {_window.TotalSeconds:0}s observation window (lenient policy)");
    }

    private static SelfCheckRuleResult VerdictByExitCode(Process process)
    {
        return process.ExitCode == 0
            ? Pass("process exited with code 0 (CLI-style application ended normally)")
            : Fail(process, $"process exited with code {process.ExitCode}");
    }

    private static SelfCheckRuleResult Pass(string detail) =>
        new(nameof(ProcessLaunchRule), true, detail);

    private static SelfCheckRuleResult Fail(Process process, string detail)
    {
        // Reached without an await in scope; kill synchronously so the residual process
        // never outlives the verdict. FailAsync is the preferred (graceful) variant.
        try
        {
            if (!process.HasExited)
                process.Kill();
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"ProcessLaunchRule: residual process cleanup failed: {ex.Message}");
        }

        return new SelfCheckRuleResult(nameof(ProcessLaunchRule), false, detail);
    }

    private static async Task<SelfCheckRuleResult> FailAsync(Process? process, string detail)
    {
        if (process != null)
        {
            try
            {
                await GracefulExit.ShutdownAsync(process).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GeneralTracer.Warn($"ProcessLaunchRule: residual process cleanup failed: {ex.Message}");
            }
        }

        return new SelfCheckRuleResult(nameof(ProcessLaunchRule), false, detail);
    }
}
