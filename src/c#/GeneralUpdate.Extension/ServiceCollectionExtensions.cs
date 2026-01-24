using Microsoft.Extensions.DependencyInjection;
using System;

namespace GeneralUpdate.Extension
{
    /// <summary>
    /// Provides extension methods for registering extension system services with dependency injection.
    /// Enables seamless integration with frameworks like Prism or generic .NET DI containers.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all extension system services as singletons in the service collection.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="hostVersion">The current host application version.</param>
        /// <param name="installPath">Base path for extension installations.</param>
        /// <param name="downloadPath">Path for downloading extension packages.</param>
        /// <param name="targetPlatform">The current platform (Windows/Linux/macOS).</param>
        /// <param name="downloadTimeout">Download timeout in seconds (default: 300).</param>
        /// <returns>The service collection for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when services or paths are null.</exception>
        public static IServiceCollection AddExtensionSystem(
            this IServiceCollection services,
            Version hostVersion,
            string installPath,
            string downloadPath,
            Metadata.TargetPlatform targetPlatform = Metadata.TargetPlatform.Windows,
            int downloadTimeout = 300)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (hostVersion == null)
                throw new ArgumentNullException(nameof(hostVersion));
            if (string.IsNullOrWhiteSpace(installPath))
                throw new ArgumentNullException(nameof(installPath));
            if (string.IsNullOrWhiteSpace(downloadPath))
                throw new ArgumentNullException(nameof(downloadPath));

            // Register core services
            services.AddSingleton<Core.IExtensionCatalog>(sp =>
                new Core.ExtensionCatalog(installPath));

            services.AddSingleton<Compatibility.ICompatibilityValidator>(sp =>
                new Compatibility.CompatibilityValidator(hostVersion));

            services.AddSingleton<Download.IUpdateQueue, Download.UpdateQueue>();

            // Register the main extension host
            services.AddSingleton<IExtensionHost>(sp =>
                new ExtensionHost(
                    hostVersion,
                    installPath,
                    downloadPath,
                    targetPlatform,
                    downloadTimeout));

            return services;
        }

        /// <summary>
        /// Registers all extension system services with custom factory methods.
        /// Provides maximum flexibility for advanced scenarios.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="hostFactory">Factory method for creating the extension host.</param>
        /// <returns>The service collection for method chaining.</returns>
        public static IServiceCollection AddExtensionSystem(
            this IServiceCollection services,
            Func<IServiceProvider, IExtensionHost> hostFactory)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (hostFactory == null)
                throw new ArgumentNullException(nameof(hostFactory));

            services.AddSingleton(hostFactory);

            return services;
        }
    }
}
