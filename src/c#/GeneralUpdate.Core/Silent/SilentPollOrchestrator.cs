using System;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Strategy;

namespace GeneralUpdate.Core.Silent;

/// <summary>
/// Silent update polling orchestrator — periodically invokes a fully-configured
/// <see cref="ClientStrategy"/> on a background interval, and defers the upgrade-process
/// launch to <see cref="AppDomain.ProcessExit"/> so the running application is undisturbed.
/// </summary>
/// <remarks>
/// <para>
/// This orchestrator is a thin scheduling layer. It does NOT re-implement any update logic.
/// All update work (version check, download, backup, Upgrade-package application,
/// Client-package IPC staging) is performed by the injected <see cref="ClientStrategy"/>.
/// </para>
/// <para>
/// Core workflow:
/// </para>
/// <list type="number">
///   <item><description>Registers the <see cref="AppDomain.CurrentDomain.ProcessExit"/> event handler.</description></item>
///   <item><description>Starts a background polling loop at the configured interval.</description></item>
///   <item><description>Each cycle calls <c>ClientStrategy.ExecuteAsync()</c> to perform a full check-and-prepare.</description></item>
///   <item><description>When the strategy reports <c>HasPreparedClientUpdate == true</c>, the loop exits.</description></item>
///   <item><description>On process exit, the upgrade process is launched — IPC was already sent by <c>ClientStrategy.SendProcessIpc()</c>.</description></item>
/// </list>
/// <para>
/// The difference from the standard (immediate) flow is purely timing:
/// <br/>
/// • Standard: download → IPC → launch upgrade → exit<br/>
/// • Silent: download → IPC → (user keeps working) → process exit → launch upgrade → exit
/// </para>
/// </remarks>
public class SilentPollOrchestrator : IDisposable
{
    private readonly ClientStrategy _strategy;
    private readonly GlobalConfigInfo _configInfo;
    private readonly SilentOptions _options;
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;
    private int _prepared;
    private int _updaterStarted;

    /// <summary>
    /// Initializes a new instance of the <see cref="SilentPollOrchestrator"/> class.
    /// </summary>
    /// <param name="strategy">
    /// A fully-configured <see cref="ClientStrategy"/>. Callers should set
    /// <c>strategy.LaunchAfterPrepare = false</c> before passing it in so that
    /// the upgrade process is NOT started at the end of each poll cycle.
    /// </param>
    /// <param name="configInfo">The global configuration.</param>
    /// <param name="options">Polling interval and restart behaviour.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is <c>null</c>.</exception>
    public SilentPollOrchestrator(ClientStrategy strategy, GlobalConfigInfo configInfo, SilentOptions options)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _configInfo = configInfo ?? throw new ArgumentNullException(nameof(configInfo));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Starts the silent polling orchestrator. Registers the <see cref="AppDomain.ProcessExit"/>
    /// handler and launches the background polling loop. Returns immediately.
    /// </summary>
    /// <returns>A task representing the start operation.</returns>
    public Task StartAsync()
    {
        GeneralTracer.Info($"SilentPollOrchestrator: starting. PollInterval={_options.PollInterval.TotalMinutes}min");

        _strategy.Create(_configInfo);

        // Must be set before the first poll cycle so that when ClientStrategy calls
        // SendProcessIpc(), the correct value flows into ProcessInfo / IPC.
        _configInfo.LaunchClientAfterUpdate = _options.LaunchClientAfterUpdate;

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        _cts = new CancellationTokenSource();
        _pollingTask = Task.Run(() => PollLoopAsync(_cts.Token));

        _pollingTask.ContinueWith(task =>
        {
            if (task.Exception != null)
                GeneralTracer.Error("SilentPollOrchestrator: polling exception.", task.Exception);
        }, TaskContinuationOptions.OnlyOnFaulted);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the polling orchestrator. Cancels the background loop and unregisters
    /// the <see cref="AppDomain.ProcessExit"/> handler.
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
    }

    /// <summary>
    /// Background polling loop. Calls <see cref="ClientStrategy.ExecuteAsync"/> on each cycle.
    /// Exits when client packages have been prepared (<c>HasPreparedClientUpdate == true</c>)
    /// or cancellation is requested.
    /// </summary>
    /// <param name="token">A cancellation token for stopping the loop.</param>
    private async Task PollLoopAsync(CancellationToken token)
    {
        GeneralTracer.Info("SilentPollOrchestrator: polling loop started.");
        while (!token.IsCancellationRequested && Volatile.Read(ref _prepared) == 0)
        {
            try
            {
                GeneralTracer.Info("SilentPollOrchestrator: running update check cycle.");
                await _strategy.ExecuteAsync().ConfigureAwait(false);

                if (_strategy.HasPreparedClientUpdate)
                {
                    Interlocked.Exchange(ref _prepared, 1);
                    GeneralTracer.Info("SilentPollOrchestrator: client update prepared, waiting for process exit.");
                }
            }
            catch (Exception ex)
            {
                GeneralTracer.Error("SilentPollOrchestrator: poll cycle failed.", ex);
            }

            if (Volatile.Read(ref _prepared) == 1) break;

            try { await Task.Delay(_options.PollInterval, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Process exit event handler. When client packages were prepared, launches
    /// the upgrade process so it can apply them.
    /// </summary>
    /// <remarks>
    /// The encrypted IPC file was already written by <see cref="ClientStrategy.SendProcessIpc"/>
    /// during the last successful poll cycle. This handler delegates the actual process
    /// launch to <see cref="ClientStrategy.LaunchUpgradeProcessSync"/>, which reuses the
    /// configured OS strategy for path resolution and runs the pre-launch lifecycle hook.
    /// </remarks>
    private void OnProcessExit(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _prepared) != 1 || Interlocked.Exchange(ref _updaterStarted, 1) == 1)
            return;

        try
        {
            // Only launch the upgrade process if there are client packages staged.
            // Upgrade-only updates are already applied in-place; no further action needed.
            if (!_strategy.HasPreparedClientUpdate)
            {
                GeneralTracer.Info("SilentPollOrchestrator: no client packages staged, skipping upgrade launch.");
                return;
            }

            _strategy.LaunchUpgradeProcessSync();
            GeneralTracer.Info("SilentPollOrchestrator: upgrade process launched via ClientStrategy.");
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("SilentPollOrchestrator: OnProcessExit failed.", ex);
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="SilentPollOrchestrator"/>.
    /// </summary>
    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}

/// <summary>
/// Configuration options for silent updates.
/// </summary>
public sealed class SilentOptions
{
    /// <summary>
    /// Gets or sets the polling interval. The default is 1 hour.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets a value indicating whether the client application should be
    /// automatically launched after the upgrade process completes.
    /// </summary>
    /// <value>
    /// <c>true</c> (default): Automatically start the client after the upgrade completes;
    /// <c>false</c>: The caller controls restart timing manually.
    /// </value>
    public bool LaunchClientAfterUpdate { get; set; } = true;
}
