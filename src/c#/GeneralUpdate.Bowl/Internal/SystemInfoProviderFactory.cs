using System.Runtime.InteropServices;

namespace GeneralUpdate.Bowl.Internal;

/// <summary>
/// Factory that selects the correct <see cref="ISystemInfoProvider"/> for the current OS.
/// </summary>
internal static class SystemInfoProviderFactory
{
    public static ISystemInfoProvider Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsSystemInfoProvider();

        // Linux and macOS: no-op for now (could export journalctl / syslog in future)
        return new NoOpSystemInfoProvider();
    }

    private sealed class NoOpSystemInfoProvider : ISystemInfoProvider
    {
        public System.Threading.Tasks.Task ExportAsync(
            string outputDirectory, System.Threading.CancellationToken ct)
        {
            // No system diagnostics export on non-Windows platforms (yet)
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
