using System;
using GeneralUpdate.Core.Event;
using GeneralUpdate.Core.Models;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Default progress reporter that bridges <see cref="DiffProgress"/> updates
/// to <see cref="EventManager"/> so they surface through
/// <see cref="GeneralUpdateBootstrap.AddListenerProgress"/>.
/// </summary>
public class DiffProgressReporter : IProgress<DiffProgress>
{
    private readonly object _sender;

    public DiffProgressReporter(object sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public void Report(DiffProgress value)
    {
        EventManager.Instance.Dispatch(_sender, new ProgressEventArgs(value));
    }
}
