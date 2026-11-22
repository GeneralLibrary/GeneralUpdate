using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.StartupHealth;

/// <summary>
/// Opt-in process launch and observation for a previously created startup attempt.
/// This API does not participate in the update pipeline, report success, or restore files.
/// </summary>
public sealed class StartupHealthProcess
{
    /// <summary>Environment variable containing the trusted health store directory.</summary>
    public const string DirectoryVariable = "GENERALUPDATE_STARTUP_HEALTH_DIRECTORY";

    /// <summary>Environment variable containing the startup attempt ID.</summary>
    public const string AttemptVariable = "GENERALUPDATE_STARTUP_HEALTH_ATTEMPT";

    private readonly StartupHealthStore _store;
    private readonly string _directory;

    /// <summary>Creates a process adapter for the same trusted directory used by the store.</summary>
    public StartupHealthProcess(string directory)
    {
        _store = new StartupHealthStore(directory);
        _directory = System.IO.Path.GetFullPath(directory);
    }

    /// <summary>
    /// Starts a child with the attempt identity in its environment. The caller must persist the
    /// attempt ID before calling this method and must not launch the same attempt twice.
    /// </summary>
    public Process Start(Guid attemptId, ProcessStartInfo startInfo)
    {
        if (startInfo == null) throw new ArgumentNullException(nameof(startInfo));
        if (startInfo.UseShellExecute)
            throw new ArgumentException("Shell execution cannot pass the health environment.", nameof(startInfo));
        if (_store.GetStatus(attemptId) != StartupHealthStatus.Pending)
            throw new InvalidOperationException("The startup attempt is already terminal.");

        startInfo.Environment[DirectoryVariable] = _directory;
        startInfo.Environment[AttemptVariable] = attemptId.ToString("D");
        try
        {
            return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the application.");
        }
        catch (Exception launchError)
        {
            try
            {
                _store.ReportFailure(attemptId, StartupHealthStatus.Failed);
            }
            catch (Exception stateError)
            {
                throw new AggregateException("Process launch and recording its failure both failed.", launchError, stateError);
            }
            throw;
        }
    }

    /// <summary>
    /// Waits for explicit confirmation, failure, timeout, or exit of the process returned by
    /// <see cref="Start"/>. Cancellation stops observation only; it does not stop the child.
    /// </summary>
    public async Task<StartupHealthStatus> WaitAsync(Guid attemptId, Process process, CancellationToken cancellationToken = default)
    {
        if (process == null) throw new ArgumentNullException(nameof(process));
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = _store.GetStatus(attemptId);
            if (status != StartupHealthStatus.Pending) return status;
            if (process.HasExited)
            {
                _store.ReportFailure(attemptId, StartupHealthStatus.ProcessExited);
                return _store.GetStatus(attemptId);
            }
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Called by the child only after its business readiness checks pass. Returns false when
    /// monitoring is not enabled or the attempt already has a terminal outcome.
    /// </summary>
    public static bool ConfirmCurrentProcess()
    {
        var directory = Environment.GetEnvironmentVariable(DirectoryVariable);
        var attempt = Environment.GetEnvironmentVariable(AttemptVariable);
        if (directory == null && attempt == null) return false;
        if (string.IsNullOrWhiteSpace(directory) || !Guid.TryParseExact(attempt, "D", out var id) || id == Guid.Empty)
            throw new InvalidOperationException("Incomplete or invalid startup health environment.");
        return new StartupHealthStore(directory).ConfirmHealthy(id);
    }
}
