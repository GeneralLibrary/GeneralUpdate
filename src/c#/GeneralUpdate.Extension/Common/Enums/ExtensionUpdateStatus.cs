using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Common.Enums;

/// <summary>
/// Extension update status
/// </summary>
public enum ExtensionUpdateStatus
{
    /// <summary>
    /// Queued for update
    /// </summary>
    Queued = 0,
    
    /// <summary>
    /// Currently updating
    /// </summary>
    Updating = 1,
    
    /// <summary>
    /// Update completed successfully
    /// </summary>
    UpdateSuccessful = 2,
    
    /// <summary>
    /// Update failed
    /// </summary>
    UpdateFailed = 3
}
