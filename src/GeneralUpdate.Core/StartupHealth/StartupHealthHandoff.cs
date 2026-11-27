using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.StartupHealth;

/// <summary>
/// Opt-in durable attempt creation and at-most-once process launch for cooperating hosts.
/// This does not participate in the updater pipeline or restore files.
/// </summary>
public sealed class StartupHealthHandoff
{
    private readonly string _directory;
    private readonly StartupHealthStore _store;
    private readonly StartupHealthProcess _process;

    /// <summary>Uses the same trusted persistent directory as the health store.</summary>
    public StartupHealthHandoff(string directory)
    {
        _store = new StartupHealthStore(directory);
        _directory = Path.GetFullPath(directory);
        _process = new StartupHealthProcess(_directory);
    }

    /// <summary>
    /// Creates the attempt. Persist the unique ID in the owning host's journal before calling;
    /// never reuse an ID even if this method fails or is interrupted.
    /// </summary>
    public void Prepare(Guid attemptId, DateTimeOffset deadline) => _store.Begin(attemptId, deadline);

    /// <summary>
    /// Claims the single launch before starting the process. A crash after the claim leaves
    /// launch uncertain; do not retry it. All launchers must use this method for the guarantee.
    /// </summary>
    public Process Start(Guid attemptId, ProcessStartInfo startInfo)
    {
        if (startInfo == null) throw new ArgumentNullException(nameof(startInfo));
        if (startInfo.UseShellExecute)
            throw new ArgumentException("Shell execution cannot pass the health environment.", nameof(startInfo));
        if (_store.GetStatus(attemptId) != StartupHealthStatus.Pending)
            throw new InvalidOperationException("The startup attempt is already terminal.");

        var claimPath = Path.Combine(_directory, attemptId.ToString("N"), "launch");
        using (var claim = new FileStream(claimPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            claim.Flush(true);
        return _process.Start(attemptId, startInfo);
    }

    /// <summary>Reads the persisted outcome after a host restart; it does not infer process exit.</summary>
    public StartupHealthStatus GetStatus(Guid attemptId) => _store.GetStatus(attemptId);

    /// <summary>Observes the child returned by this host's <see cref="Start"/> call.</summary>
    public Task<StartupHealthStatus> WaitAsync(Guid attemptId, Process process, CancellationToken cancellationToken = default)
        => _process.WaitAsync(attemptId, process, cancellationToken);
}
