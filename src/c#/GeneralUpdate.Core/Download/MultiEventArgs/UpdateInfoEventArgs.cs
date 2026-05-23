using System;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Download;

public class UpdateInfoEventArgs(VersionRespDTO info) : EventArgs
{
    public VersionRespDTO Info { get; private set; } = info;
}
