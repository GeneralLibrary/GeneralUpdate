using Microsoft.Extensions.DependencyInjection;
using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Core.Execution;
using GeneralUpdate.Drivelution.Windows.Implementation;
using GeneralUpdate.Drivelution.Linux.Implementation;
using GeneralUpdate.Drivelution.MacOS.Implementation;

namespace GeneralUpdate.Drivelution.Core;

/// <summary>
/// Extension methods for registering Drivelution services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Drivelution driver update services for the current platform.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for <see cref="DrivelutionOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// // ASP.NET / Generic Host
    /// builder.Services.AddDrivelution();
    ///
    /// // With options
    /// builder.Services.AddDrivelution(options =>
    /// {
    ///     options.DefaultRetryCount = 5;
    ///     options.DefaultTimeoutSeconds = 600;
    /// });
    ///
    /// // Consumer
    /// public class MyService
    /// {
    ///     public MyService(IGeneralDrivelution updater) { ... }
    /// }
    /// </code>
    /// </example>
    public static IServiceCollection AddDrivelution(
        this IServiceCollection services,
        Action<DrivelutionOptions>? configure = null)
    {
        // Configuration
        var options = new DrivelutionOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        // Infrastructure
        services.AddSingleton<ICommandRunner, CommandRunner>();

        // Platform-specific registrations
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IDriverValidator, WindowsDriverValidator>();
            services.AddSingleton<IDriverBackup, WindowsDriverBackup>();
            services.AddSingleton<IGeneralDrivelution, WindowsGeneralDrivelution>();
        }
        else if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IDriverValidator, LinuxDriverValidator>();
            services.AddSingleton<IDriverBackup, LinuxDriverBackup>();
            services.AddSingleton<IGeneralDrivelution, LinuxGeneralDrivelution>();
        }
        else if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<IDriverValidator, MacOSDriverValidator>();
            services.AddSingleton<IDriverBackup, MacOSDriverBackup>();
            services.AddSingleton<IGeneralDrivelution, MacOsGeneralDrivelution>();
        }

        return services;
    }
}
