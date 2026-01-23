# GeneralUpdate Extension Framework

A comprehensive plugin manager framework for WPF/Avalonia desktop applications, inspired by VS Code Extension Mechanism and VSIX design.

## Overview

This framework provides a complete plugin/extension system with support for:
- Multi-language runtime support (C#, Lua, Python, Node.js, native executables)
- Advanced update mechanisms (full, incremental, differential, rollback)
- Enterprise-level features (offline installation, repository mirrors, security policies)
- Strong security and sandboxing

## Architecture

The framework is organized into the following namespaces:

### MyApp.Extensions (Core)
Core models and interfaces for extension management.

**Key Types:**
- `ExtensionManifest` - Extension metadata and configuration
- `ExtensionState` - Extension lifecycle states (Installed, Enabled, Disabled, etc.)
- `SemVersion` - Semantic versioning support
- `ExtensionDependency` - Dependency management
- `ExtensionPermission` - Permission system

**Key Interfaces:**
- `IExtensionManager` - Install, uninstall, enable, disable, update extensions
- `IExtensionLoader` - Load and activate extensions
- `IExtensionRepository` - Query and download extensions
- `IExtensionLifecycle` - Extension lifecycle hooks
- `IExtensionDependencyResolver` - Dependency resolution
- `ISignatureValidator` - Package signature validation
- `IExtensionProcessHost` - Process isolation
- `IExtensionSandbox` - Permission and resource isolation

### MyApp.Extensions.Packaging
VSIX-inspired package specification and structure.

**Key Types:**
- `PackageManifest` - Package metadata with runtime config, dependencies, permissions
- `PackageFileEntry` - File indexing within packages
- `PackageSignature` - Digital signatures and certificates
- `PackageFormatVersion` - Standardized package versioning

**Package Structure Convention:**
```
extension-package/
├── manifest.json
├── assets/
├── runtime/
├── patches/
└── signature/
```

### MyApp.Extensions.Updates
Advanced update mechanisms for extensions.

**Key Types:**
- `UpdateChannel` - Stable/PreRelease/Dev channels
- `UpdateMetadata` - Available updates and compatibility
- `UpdatePackageInfo` - Full/Delta/Diff package details
- `DeltaPatchInfo` - Incremental update information
- `RollbackInfo` - Rollback configuration

**Key Interfaces:**
- `IUpdateService` - Check for updates, download, install, rollback
- `IDeltaUpdateService` - Generate and apply delta patches

### MyApp.Extensions.Runtime
Multi-language runtime support for extensions.

**Key Types:**
- `RuntimeType` - Enum: DotNet, Lua, Python, Node, Exe, Custom
- `RuntimeEnvironmentInfo` - Runtime configuration and environment

**Key Interfaces:**
- `IRuntimeHost` - Start/stop runtime, invoke methods, health checks
- `IRuntimeResolver` - Resolve runtime hosts by type

### MyApp.Extensions.Security
Enterprise-level security and offline support.

**Key Types:**
- `EnterprisePolicy` - Allowed/blocked sources, certificate requirements
- `OfflinePackageIndex` - Local package management

**Key Interfaces:**
- `IRepositoryMirror` - Enterprise repository mirror management
- `IOfflineInstaller` - Offline installation support

### MyApp.Extensions.SDK
Developer-facing APIs for extension authors.

**Key Types:**
- `ExtensionActivationEvent` - Trigger-based activation (onCommand, onLanguage, onView, etc.)

**Key Interfaces:**
- `IExtensionContext` - Extension runtime context and storage
- `IExtensionAPI` - Host application services and commands

## Usage Example

```csharp
// Install an extension
var manager = GetService<IExtensionManager>();
await manager.InstallAsync("path/to/extension.vsix");

// Check for updates
var updateService = GetService<IUpdateService>();
var updateMetadata = await updateService.CheckForUpdatesAsync("extension-id");

// Load and activate extension
var loader = GetService<IExtensionLoader>();
var manifest = await loader.LoadAsync("path/to/extension");
await loader.ActivateAsync(manifest.Id);

// Runtime support
var resolver = GetService<IRuntimeResolver>();
var pythonHost = resolver.Resolve(RuntimeType.Python);
await pythonHost.StartAsync(runtimeEnv);
var result = await pythonHost.InvokeAsync("main", args);
```

## Extension Development

Extensions can be developed in any supported language and must include a `manifest.json`:

```json
{
  "id": "my-extension",
  "name": "My Extension",
  "version": "1.0.0",
  "author": "Author Name",
  "runtime": "python",
  "entrypoint": "main.py",
  "dependencies": [],
  "permissions": [
    {
      "type": "FileSystem",
      "scope": "read",
      "reason": "Read configuration files"
    }
  ],
  "activationEvents": [
    "onCommand:myextension.command",
    "onStartup"
  ]
}
```

## Extension Activation Events

Similar to VS Code, extensions can be activated based on:
- `onStartup` - On application startup
- `onCommand:commandId` - When a command is invoked
- `onLanguage:languageId` - When a language file is opened
- `onView:viewId` - When a view is opened
- `onFileSystem:pattern` - When a file pattern is accessed

## Security

The framework includes comprehensive security features:
- Digital signature validation
- Certificate chain verification
- Permission-based sandbox
- Enterprise policy enforcement
- Trusted source management

## Enterprise Features

- **Repository Mirrors**: Set up internal mirrors for extension repositories
- **Offline Installation**: Deploy extensions without internet access
- **Policy Enforcement**: Control which extensions can be installed
- **Approval Workflows**: Require approval before installation

## License

This framework is part of the GeneralUpdate project and follows the same licensing terms.

## Contributing

Contributions are welcome! Please refer to the main GeneralUpdate repository for contribution guidelines.
