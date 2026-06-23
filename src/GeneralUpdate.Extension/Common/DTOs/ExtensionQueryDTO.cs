using GeneralUpdate.Extension.Common.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Common.DTOs;

/// <summary>
/// Extension query data transfer object
/// </summary>
public class ExtensionQueryDTO
{
    public string? Id { get; set; }
    
    /// <summary>
    /// Extension name filter (partial match)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Extension version filter (exact match)
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Extension status filter (null for all, true for enabled, false for disabled)
    /// </summary>
    public bool? Status { get; set; }

    /// <summary>
    /// Upload start date filter
    /// </summary>
    public DateTime? BeginDate { get; set; }

    /// <summary>
    /// Upload end date filter
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Publisher filter (partial match)
    /// </summary>
    public string? Publisher { get; set; }

    /// <summary>
    /// Category filter (will match any extension containing this category)
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Platform filter (will match extensions supporting this platform)
    /// </summary>
    public TargetPlatform? Platform { get; set; }

    /// <summary>
    /// Host version for compatibility checking
    /// Will return only extensions compatible with this version
    /// </summary>
    public string? HostVersion { get; set; }

    /// <summary>
    /// Filter for pre-release versions (null for all, true for pre-release only, false for stable only)
    /// </summary>
    public bool? IsPreRelease { get; set; }

    /// <summary>
    /// Page number (starting from 1)
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Page size (number of items per page)
    /// </summary>
    public int PageSize { get; set; } = 10;
}
