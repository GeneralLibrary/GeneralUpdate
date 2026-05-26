namespace GeneralUpdate.Core.Models;

/// <summary>
/// Progress information for differential pipeline operations.
/// </summary>
public readonly struct DiffProgress
{
    /// <summary>Number of files processed so far.</summary>
    public int Completed { get; }

    /// <summary>Total number of files to process.</summary>
    public int Total { get; }

    /// <summary>Name of the file currently being processed.</summary>
    public string? CurrentFile { get; }

    /// <summary>Percentage complete (0-100).</summary>
    public double Percentage => Total > 0 ? (double)Completed / Total * 100.0 : 100.0;

    /// <summary>Whether the operation is complete.</summary>
    public bool IsComplete => Completed >= Total;

    /// <summary>Error message if the current file failed (null on success).</summary>
    public string? Error { get; }

    public DiffProgress(int completed, int total, string? currentFile, string? error = null)
    {
        Completed = completed;
        Total = total;
        CurrentFile = currentFile;
        Error = error;
    }

    /// <summary>Creates a completion marker.</summary>
    public static DiffProgress Complete(int total) => new DiffProgress(total, total, null);

    public override string ToString()
        => IsComplete
            ? $"Complete: {Completed}/{Total} files"
            : $"[{Percentage:F1}%] {Completed}/{Total} -- {CurrentFile ?? "..."}{(Error != null ? $" (failed: {Error})" : "")}";
}
