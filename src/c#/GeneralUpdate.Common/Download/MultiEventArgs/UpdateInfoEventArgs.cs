using System;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Download;

public class UpdateInfoEventArgs(VersionRespDTO info) : EventArgs
{
    public VersionRespDTO Info { get; private set; } = info;
}
