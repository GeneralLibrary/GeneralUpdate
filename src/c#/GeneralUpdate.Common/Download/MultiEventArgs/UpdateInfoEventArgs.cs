using System;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Download;

public class UpdateInfoEventArgs(VersionRespDTO info) : EventArgs
{
    public VersionRespDTO Info { get; private set; } = info;

    /// <summary>
    /// Set to <c>true</c> inside a <see cref="GeneralUpdate.Common.Internal.Event.EventManager"/> listener to
    /// indicate that this update should be skipped.  Has no effect when the update is marked as forcibly required.
    /// </summary>
    public bool IsSkip { get; set; }
}
