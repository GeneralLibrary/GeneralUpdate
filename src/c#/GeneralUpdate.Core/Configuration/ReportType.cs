namespace GeneralUpdate.Core.Configuration;

/// <summary>
/// Report status type constants for update operation results.
/// </summary>
public class ReportType
{
    /// <summary>No report / default state.</summary>
    public const int None = 0;
    
    /// <summary>Update succeeded.</summary>
    public const int Success = 2;

    /// <summary>Update failed.</summary>
    public const int Failure = 3;
}
