namespace GeneralUpdate.Core.Configuration;

/// <summary>
/// Diff/patch generation mode.
/// </summary>
public enum DiffMode
{
    /// <summary>Process diffs one file at a time.</summary>
    Serial,
    /// <summary>Process diffs in parallel for faster throughput.</summary>
    Parallel
}
