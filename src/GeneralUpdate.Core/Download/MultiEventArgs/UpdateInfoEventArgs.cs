using System;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Download;

public class UpdateInfoEventArgs(VersionRespDTO? info = null) : EventArgs
{
    public VersionRespDTO? Info { get; private set; } = info;
}
