using Microsoft.Extensions.DependencyInjection;
using GeneralUpdate.Extension.Core;
using GeneralUpdate.Extension.Catalog;
using GeneralUpdate.Extension.Download;
using GeneralUpdate.Extension.Compatibility;
using GeneralUpdate.Extension.Dependencies;
using GeneralUpdate.Extension.Communication;
using GeneralUpdate.Extension.Common.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace GeneralUpdate.Extension.Core;

/// <summary>
/// Builder for configuring extension host
/// </summary>
public class ExtensionHostBuilder
{
    private readonly IServiceCollection _services;
    private ExtensionHostOptions? _options;

    /// <summary>
    /// Initialize extension host builder
    /// </summary>
    public ExtensionHostBuilder()
    {
        _services = new ServiceCollection();
    }

    /// <summary>
    /// Configure extension host options
    /// </summary>
    /// <param name="configure">Configuration action</param>
    /// <returns>Builder instance for chaining</returns>
    public ExtensionHostBuilder ConfigureOptions(Action<ExtensionHostOptions> configure)
    {
        _options = new ExtensionHostOptions();
        configure(_options);
        return this;
    }

    /// <summary>
    /// Configure extension host with options
    /// </summary>
    /// <param name="options">Extension host options</param>
    /// <returns>Builder instance for chaining</returns>
    public ExtensionHostBuilder WithOptions(ExtensionHostOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>
    /// Configure services
    /// </summary>
    /// <param name="configure">Service configuration action</param>
    /// <returns>Builder instance for chaining</returns>
    public ExtensionHostBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        configure(_services);
        return this;
    }

    /// <summary>
    /// Build the extension host
    /// </summary>
    /// <returns>Configured extension host instance</returns>
    public IExtensionHost Build()
    {
        if (_options == null)
        {
            throw new InvalidOperationException("Options must be configured before building");
        }

        // Register core services (only if not already registered by the user)
        if (!_services.Any(sd => sd.ServiceType == typeof(ExtensionHostOptions)))
            _services.AddSingleton(_options);

        if (!_services.Any(sd => sd.ServiceType == typeof(IExtensionHttpClient)))
            _services.AddSingleton<IExtensionHttpClient>(sp => 
                new ExtensionHttpClient(_options.ServerUrl, _options.Scheme, _options.Token));

        if (!_services.Any(sd => sd.ServiceType == typeof(IVersionCompatibilityChecker)))
            _services.AddSingleton<IVersionCompatibilityChecker, VersionCompatibilityChecker>();

        if (!_services.Any(sd => sd.ServiceType == typeof(IDownloadQueueManager)))
            _services.AddSingleton<IDownloadQueueManager, DownloadQueueManager>();

        if (!_services.Any(sd => sd.ServiceType == typeof(IPlatformMatcher)))
            _services.AddSingleton<IPlatformMatcher, PlatformMatcher>();

        // Platform detection and metadata mapping (overridable for testing)
        if (!_services.Any(sd => sd.ServiceType == typeof(IPlatformServices)))
            _services.AddSingleton<IPlatformServices, RuntimePlatformServices>();

        if (!_services.Any(sd => sd.ServiceType == typeof(IExtensionMetadataMapper)))
            _services.AddSingleton<IExtensionMetadataMapper, DefaultExtensionMetadataMapper>();
        
        var catalogPath = _options.CatalogPath ?? _options.ExtensionsDirectory;
        if (!_services.Any(sd => sd.ServiceType == typeof(IExtensionCatalog)))
            _services.AddSingleton<IExtensionCatalog>(sp => new ExtensionCatalog(catalogPath));
        
        if (!_services.Any(sd => sd.ServiceType == typeof(IDependencyResolver)))
            _services.AddSingleton<IDependencyResolver>(sp => 
                new DependencyResolver(sp.GetRequiredService<IExtensionCatalog>()));

        // Lifecycle hooks (default: no-op, user can override via ConfigureServices)
        if (!_services.Any(sd => sd.ServiceType == typeof(IExtensionLifecycleHooks)))
            _services.AddSingleton<IExtensionLifecycleHooks, DefaultExtensionLifecycleHooks>();
        
        if (!_services.Any(sd => sd.ServiceType == typeof(IExtensionHost)))
            _services.AddSingleton<IExtensionHost, GeneralExtensionHost>();

        var serviceProvider = _services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IExtensionHost>();
    }
}
