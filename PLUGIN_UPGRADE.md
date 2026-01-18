# Plugin Upgrade Functionality

## Overview

GeneralUpdate now supports plugin upgrade functionality, allowing client applications to manage plugins (JavaScript, Lua, Python, WASM, or external executables) with version compatibility checking and selective upgrades, similar to VS Code's extension management.

## Features

1. **Plugin Version Management**: Track and manage multiple plugin versions with compatibility ranges
2. **Automatic Compatibility Checking**: Ensures plugins are compatible with the current client version
3. **Selective Plugin Upgrades**: Users can choose which plugins to upgrade
4. **Automatic Minimum Version Matching**: When the client upgrades, automatically determines the minimum required plugin versions

## Plugin Types

The system supports the following plugin types:

- **JavaScript** (`PluginType = 1`): Plugins using embedded script engine
- **Lua** (`PluginType = 2`): Plugins using Lua script engine  
- **Python** (`PluginType = 3`): Plugins using Python script engine
- **WASM** (`PluginType = 4`): WebAssembly plugins
- **ExternalExecutable** (`PluginType = 5`): External executable programs with protocol communication

## Quick Start

### 1. Configure Plugin Update URL

```csharp
var configInfo = new Configinfo
{
    AppName = "MyApp",
    MainAppName = "MyApp",
    ClientVersion = "1.0.0",
    InstallPath = @"C:\MyApp",
    UpdateUrl = "https://api.example.com/update",
    PluginUpdateUrl = "https://api.example.com/plugin-update", // NEW: Plugin update endpoint
    AppSecretKey = "your-secret-key",
    ProductId = "product123"
};

GeneralClientBootstrap.Instance
    .SetConfig(configInfo)
    .LaunchAsync();
```

### 2. Get Available Plugin Updates

```csharp
using GeneralUpdate.ClientCore;
using GeneralUpdate.Common.Shared.Object;

var configInfo = new GlobalConfigInfo
{
    PluginUpdateUrl = "https://api.example.com/plugin-update",
    ClientVersion = "1.0.0",
    AppSecretKey = "your-secret-key",
    ProductId = "product123",
    Scheme = "Bearer",
    Token = "your-auth-token"
};

// Get all available plugin updates compatible with current client version
var availablePlugins = await PluginBootstrapExtensions.GetAvailablePluginUpdatesAsync(
    configInfo, 
    PlatformType.Windows
);

Console.WriteLine($"Found {availablePlugins.Count} plugin updates");
foreach (var plugin in availablePlugins)
{
    Console.WriteLine($"- {plugin.Name} v{plugin.Version} ({plugin.PluginType})");
}
```

### 3. Selective Plugin Upgrade (VS Code-style)

```csharp
// Let user select which plugins to upgrade
var selectedPluginIds = new List<string> { "plugin-id-1", "plugin-id-3" };

var pluginsToUpgrade = PluginBootstrapExtensions.SelectPluginsForUpgrade(
    availablePlugins,
    selectedPluginIds
);

Console.WriteLine($"User selected {pluginsToUpgrade.Count} plugins to upgrade");
```

### 4. Get Mandatory Plugin Updates

```csharp
// Get only mandatory plugin updates
var mandatoryPlugins = PluginBootstrapExtensions.GetMandatoryPluginUpdates(availablePlugins);

if (mandatoryPlugins.Any())
{
    Console.WriteLine("The following plugins MUST be upgraded:");
    foreach (var plugin in mandatoryPlugins)
    {
        Console.WriteLine($"- {plugin.Name} v{plugin.Version}");
    }
}
```

### 5. Automatic Plugin Version Matching on Client Upgrade

```csharp
// When upgrading client to version 2.0.0, automatically determine required plugin versions
var currentPlugins = new Dictionary<string, string>
{
    { "plugin-id-1", "1.0.0" },
    { "plugin-id-2", "1.5.0" },
    { "plugin-id-3", "2.0.0" }
};

var requiredPluginUpdates = await PluginBootstrapExtensions.GetRequiredPluginUpdatesForClientUpgradeAsync(
    configInfo,
    "2.0.0", // Target client version
    PlatformType.Windows,
    currentPlugins
);

Console.WriteLine($"{requiredPluginUpdates.Count} plugins need to be upgraded for client v2.0.0");
```

### 6. Validate Plugin Compatibility

```csharp
// Check if a specific plugin is compatible with the client version
var plugin = new PluginInfo
{
    PluginId = "my-plugin",
    Version = "1.5.0",
    MinClientVersion = "1.0.0",
    MaxClientVersion = "2.0.0"
};

bool isCompatible = PluginUpdateService.IsPluginCompatible("1.8.0", plugin);
Console.WriteLine($"Plugin is compatible: {isCompatible}"); // Output: true

isCompatible = PluginUpdateService.IsPluginCompatible("2.5.0", plugin);
Console.WriteLine($"Plugin is compatible: {isCompatible}"); // Output: false
```

## Server-Side API Implementation

### Plugin Validation Endpoint

Your server should implement a plugin validation endpoint that returns available plugin updates:

**Request:**
```json
POST https://api.example.com/plugin-update
Content-Type: application/json
Authorization: Bearer your-token

{
  "ClientVersion": "1.0.0",
  "Platform": 1,
  "PluginId": "optional-specific-plugin-id",
  "AppKey": "your-secret-key",
  "ProductId": "product123"
}
```

**Response:**
```json
{
  "code": 200,
  "message": "OK",
  "body": [
    {
      "pluginId": "js-plugin-1",
      "name": "JavaScript Plugin",
      "version": "1.2.0",
      "pluginType": 1,
      "minClientVersion": "1.0.0",
      "maxClientVersion": "2.0.0",
      "url": "https://cdn.example.com/plugins/js-plugin-1-1.2.0.zip",
      "hash": "sha256-hash-here",
      "releaseDate": "2026-01-15T00:00:00Z",
      "size": 1024000,
      "format": "zip",
      "description": "A useful JavaScript plugin",
      "isMandatory": false
    },
    {
      "pluginId": "python-plugin-2",
      "name": "Python Data Processor",
      "version": "2.0.0",
      "pluginType": 3,
      "minClientVersion": "1.5.0",
      "maxClientVersion": "3.0.0",
      "url": "https://cdn.example.com/plugins/python-plugin-2-2.0.0.zip",
      "hash": "sha256-hash-here",
      "releaseDate": "2026-01-10T00:00:00Z",
      "size": 2048000,
      "format": "zip",
      "description": "Enhanced data processing capabilities",
      "isMandatory": true
    }
  ]
}
```

## Complete Usage Example

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.ClientCore;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Common.Shared.Object.Enum;

public class PluginUpgradeExample
{
    public static async Task Main()
    {
        // Step 1: Configure the client
        var configInfo = new GlobalConfigInfo
        {
            PluginUpdateUrl = "https://api.example.com/plugin-update",
            ClientVersion = "1.0.0",
            AppSecretKey = "your-secret-key",
            ProductId = "product123"
        };

        // Step 2: Check for available plugin updates
        var availablePlugins = await PluginBootstrapExtensions.GetAvailablePluginUpdatesAsync(
            configInfo,
            PlatformType.Windows
        );

        if (!availablePlugins.Any())
        {
            Console.WriteLine("No plugin updates available");
            return;
        }

        // Step 3: Separate mandatory and optional updates
        var mandatoryPlugins = PluginBootstrapExtensions.GetMandatoryPluginUpdates(availablePlugins);
        var optionalPlugins = availablePlugins.Except(mandatoryPlugins).ToList();

        // Step 4: Handle mandatory updates
        if (mandatoryPlugins.Any())
        {
            Console.WriteLine("Installing mandatory plugin updates...");
            foreach (var plugin in mandatoryPlugins)
            {
                Console.WriteLine($"Installing {plugin.Name} v{plugin.Version}");
                // Download and install plugin
                await DownloadAndInstallPlugin(plugin);
            }
        }

        // Step 5: Let user choose optional updates
        if (optionalPlugins.Any())
        {
            Console.WriteLine("\nOptional plugin updates:");
            for (int i = 0; i < optionalPlugins.Count; i++)
            {
                var plugin = optionalPlugins[i];
                Console.WriteLine($"{i + 1}. {plugin.Name} v{plugin.Version} - {plugin.Description}");
            }

            Console.Write("\nEnter plugin numbers to install (comma-separated): ");
            var input = Console.ReadLine();
            var selectedIndices = input.Split(',').Select(s => int.Parse(s.Trim()) - 1);

            var selectedPluginIds = selectedIndices
                .Select(i => optionalPlugins[i].PluginId)
                .ToList();

            var selectedPlugins = PluginBootstrapExtensions.SelectPluginsForUpgrade(
                optionalPlugins,
                selectedPluginIds
            );

            foreach (var plugin in selectedPlugins)
            {
                Console.WriteLine($"Installing {plugin.Name} v{plugin.Version}");
                await DownloadAndInstallPlugin(plugin);
            }
        }

        Console.WriteLine("\nPlugin updates completed!");
    }

    private static async Task DownloadAndInstallPlugin(PluginInfo plugin)
    {
        // Implementation for downloading and installing plugin
        // This would use DownloadManager similar to client updates
        await Task.Delay(100); // Placeholder
    }
}
```

## Advanced Scenarios

### Scenario 1: Client Upgrade with Plugin Compatibility Check

```csharp
// Before upgrading client, check if current plugins will be compatible
var targetClientVersion = "2.0.0";
var currentPlugins = new List<PluginInfo>
{
    new PluginInfo 
    { 
        Name = "Plugin A",
        Version = "1.0.0",
        MinClientVersion = "1.0.0",
        MaxClientVersion = "1.9.0" // Won't work with 2.0.0!
    }
};

bool allCompatible = PluginBootstrapExtensions.ValidatePluginCompatibilityForUpgrade(
    targetClientVersion,
    currentPlugins
);

if (!allCompatible)
{
    Console.WriteLine("Warning: Some plugins are not compatible with the new client version!");
    // Prompt user to upgrade plugins first
}
```

### Scenario 2: Determine Plugin Updates Needed for Client Upgrade

```csharp
var currentPluginVersions = new Dictionary<string, string>
{
    { "plugin-a", "1.0.0" },
    { "plugin-b", "1.5.0" }
};

var targetClientVersion = "2.0.0";

// Automatically find minimum required plugin versions for the new client
var requiredUpdates = await PluginBootstrapExtensions.GetRequiredPluginUpdatesForClientUpgradeAsync(
    configInfo,
    targetClientVersion,
    PlatformType.Windows,
    currentPluginVersions
);

Console.WriteLine($"To upgrade to client v{targetClientVersion}, you need to upgrade:");
foreach (var plugin in requiredUpdates)
{
    Console.WriteLine($"- {plugin.Name} to v{plugin.Version}");
}
```

## Data Models

### PluginInfo
```csharp
public class PluginInfo
{
    public string PluginId { get; set; }              // Unique plugin identifier
    public string Name { get; set; }                   // Plugin display name
    public string Version { get; set; }                // Plugin version
    public int PluginType { get; set; }                // 1-5 (JS/Lua/Python/WASM/Exe)
    public string MinClientVersion { get; set; }       // Minimum compatible client version
    public string MaxClientVersion { get; set; }       // Maximum compatible client version
    public string Url { get; set; }                    // Download URL
    public string Hash { get; set; }                   // File integrity hash
    public DateTime ReleaseDate { get; set; }          // Release timestamp
    public long Size { get; set; }                     // File size in bytes
    public string Format { get; set; }                 // Package format (e.g., "zip")
    public string Description { get; set; }            // Plugin description
    public bool IsMandatory { get; set; }              // Whether upgrade is mandatory
}
```

## Best Practices

1. **Always check compatibility** before installing plugins
2. **Handle mandatory updates first** before offering optional updates
3. **Validate plugin compatibility** before upgrading the client
4. **Use version ranges** to define plugin compatibility (MinClientVersion, MaxClientVersion)
5. **Implement rollback mechanisms** for failed plugin installations
6. **Cache plugin metadata** to reduce API calls
7. **Show clear UI** to users about what plugins are being updated and why

## Migration Guide

If you're adding plugin support to an existing GeneralUpdate implementation:

1. Add `PluginUpdateUrl` to your `Configinfo`
2. Implement the plugin validation endpoint on your server
3. Use `PluginBootstrapExtensions` methods to manage plugin updates
4. Update your UI to show plugin update options to users

## Troubleshooting

**Q: Plugins aren't showing up as available**  
A: Ensure your `PluginUpdateUrl` is configured and the server returns plugins compatible with your `ClientVersion`

**Q: Plugin compatibility check fails**  
A: Verify that `MinClientVersion` and `MaxClientVersion` are valid semantic versions

**Q: Mandatory plugins aren't being enforced**  
A: Check that `IsMandatory` is set to `true` in the server response

## API Reference

See the following classes for detailed API documentation:
- `PluginInfo` - Plugin metadata model
- `PluginBootstrapExtensions` - Main API for plugin management
- `PluginUpdateService` - Service methods for plugin operations
- `PluginCompatibilityChecker` - Compatibility validation utilities
