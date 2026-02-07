using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Common.Models;

/// <summary>
/// Configuration options for GeneralExtensionHost
/// </summary>
public class ExtensionHostOptions
{
    /// <summary>
    /// Server base URL for extension API
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    public string Scheme { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
    
    /// <summary>
    /// Host application version
    /// </summary>
    public string HostVersion { get; set; } = string.Empty;

    /// <summary>
    /// Directory path for storing extensions
    /// </summary>
    public string ExtensionsDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Optional path to catalog directory. If not specified, defaults to the extensions directory.
    /// Each extension will have its own subdirectory with a manifest.json file.
    /// </summary>
    public string? CatalogPath { get; set; }
}
