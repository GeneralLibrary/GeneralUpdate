using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Common.Enums;

/// <summary>
/// Represents target platforms for extensions
/// </summary>
[Flags]
public enum TargetPlatform
{
    /// <summary>
    /// No platform specified
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Windows platform
    /// </summary>
    Windows = 1,
    
    /// <summary>
    /// Linux platform
    /// </summary>
    Linux = 2,
    
    /// <summary>
    /// macOS platform
    /// </summary>
    MacOS = 4,
    
    /// <summary>
    /// All platforms
    /// </summary>
    All = Windows | Linux | MacOS
}
