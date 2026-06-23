using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Bowl.Internal;

/// <summary>
/// Exports system diagnostic information (drivers, system logs, OS info).
/// Platform-specific implementations handle the actual export mechanism.
/// </summary>
internal interface ISystemInfoProvider
{
    /// <summary>
    /// Exports system diagnostic data to the specified directory.
    /// </summary>
    /// <param name="outputDirectory">Target directory for diagnostic output files.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ExportAsync(string outputDirectory, CancellationToken ct);
}
