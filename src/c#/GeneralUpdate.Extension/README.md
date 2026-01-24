# GeneralUpdate.Extension

A production-ready VS Code-style extension/plugin update system with version compatibility, automatic updates, download queuing, and rollback capabilities.

## 🎯 Key Improvements

This refactored version provides:
- **Elegant, concise naming** throughout
- **Dependency Injection support** for Prism/DI frameworks  
- **Comprehensive XML documentation** on all APIs
- **Descriptive folder structure** (no generic "Models", "Services")

## Quick Start

### With Dependency Injection (Recommended)

```csharp
services.AddExtensionSystem(
    new Version(1, 0, 0),
    installPath: @"C:\Extensions",
    downloadPath: @"C:\Downloads",
    Metadata.TargetPlatform.Windows);

var host = provider.GetRequiredService<IExtensionHost>();
```

### Manual Setup

```csharp
var host = new ExtensionHost(
    new Version(1, 0, 0),
    @"C:\Extensions",
    @"C:\Downloads",
    Metadata.TargetPlatform.Windows);
```

## Architecture

```
Metadata/           # Extension descriptors, platforms, content types
Installation/       # Installed extension state
Core/              # Extension catalog (IExtensionCatalog)
Compatibility/     # Version validation (ICompatibilityValidator)
Download/          # Update queue and downloads (IUpdateQueue)
EventHandlers/     # Event definitions
ExtensionHost.cs   # Main orchestrator (IExtensionHost)
```

## Naming Changes

| Old | New |
|-----|-----|
| ExtensionManager | ExtensionHost |
| ExtensionMetadata | ExtensionDescriptor |
| LocalExtension | InstalledExtension |
| RemoteExtension | AvailableExtension |
| ExtensionPlatform | TargetPlatform |
| ExtensionUpdateStatus | UpdateState |
| ExtensionUpdateQueueItem | UpdateOperation |

For complete documentation, see the full README or code comments.
