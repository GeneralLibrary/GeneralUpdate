# GeneralUpdate.Extension Architecture

## Folder Structure

The extension system is organized by core functionality responsibilities:

### 📁 Core/
**Purpose**: Main host and builder pattern implementation
- `GeneralExtensionHost.cs` - Main extension host implementation
- `ExtensionHostBuilder.cs` - Builder pattern for fluent configuration
- `IExtensionHost.cs` - Main host interface
- `IExtensionServiceFactory.cs` - Service factory interface
- `ExtensionUpdateEventArgs.cs` - Event arguments for update notifications

### 📁 Catalog/
**Purpose**: Extension catalog management (local storage)
- `ExtensionCatalog.cs` - JSON-persisted local extension storage
- `IExtensionCatalog.cs` - Catalog interface

**Responsibilities**:
- Load/save locally installed extensions
- Query extensions by platform
- Add/update/remove extensions from catalog

### 📁 Download/
**Purpose**: Download queue and task management
- `DownloadQueueManager.cs` - Concurrent download queue manager
- `IDownloadQueueManager.cs` - Download queue interface

**Responsibilities**:
- Queue management with concurrent execution
- Download status tracking and events
- Task cancellation and cleanup

### 📁 Compatibility/
**Purpose**: Version and platform compatibility checking
- `VersionCompatibilityChecker.cs` - Host version compatibility validation
- `PlatformMatcher.cs` - Runtime platform detection
- `IVersionCompatibilityChecker.cs` - Version checker interface
- `IPlatformMatcher.cs` - Platform matcher interface

**Responsibilities**:
- Validate MinHostVersion ≤ host ≤ MaxHostVersion
- Detect current platform (Windows/Linux/macOS)
- Check platform support flags

### 📁 Dependencies/
**Purpose**: Dependency resolution
- `DependencyResolver.cs` - Recursive dependency resolver
- `IDependencyResolver.cs` - Dependency resolver interface

**Responsibilities**:
- Resolve transitive dependencies
- Detect circular dependencies
- Determine correct installation order
- Identify missing dependencies

### 📁 Communication/
**Purpose**: HTTP client for server communication
- `ExtensionHttpClient.cs` - HTTP client with Bearer auth and resume support
- `IExtensionHttpClient.cs` - HTTP client interface

**Responsibilities**:
- Query extensions from server (with filters)
- Download extensions with resume capability (HTTP Range)
- Bearer token authentication

### 📁 Common/
**Purpose**: Shared DTOs, Enums, and Models

#### Common/DTOs/
- `ExtensionDTO.cs` - Extension data transfer object
- `ExtensionQueryDTO.cs` - Query filter DTO
- `HttpResponseDTO.cs` - HTTP response wrapper
- `PagedResultDTO.cs` - Paginated results

#### Common/Enums/
- `ExtensionUpdateStatus.cs` - Update status enum (Queued, Updating, UpdateSuccessful, UpdateFailed)
- `TargetPlatform.cs` - Platform flags enum (None, Windows, Linux, MacOS, All)

#### Common/Models/
- `ExtensionMetadata.cs` - Central extension metadata model
- `ExtensionHostOptions.cs` - Host configuration options
- `DownloadTask.cs` - Download task model
- `DownloadTaskEventArgs.cs` - Download event arguments

### 📁 Examples/
**Purpose**: Usage examples
- `ExtensionHostExample.cs` - Complete usage example

## Design Principles

1. **Separation of Concerns**: Each folder represents a distinct functional area
2. **Single Responsibility**: Each class has a clear, focused purpose
3. **Dependency Injection**: All services have interfaces for testability
4. **Microsoft Patterns**: Follows Microsoft's architectural best practices
5. **SOLID Principles**: Interface segregation, dependency inversion

## Dependencies Flow

```
Core (Host/Builder)
  ↓
├─→ Catalog (Local Storage)
├─→ Download (Queue Management)
├─→ Compatibility (Version/Platform)
├─→ Dependencies (Resolver)
├─→ Communication (HTTP Client)
  ↓
Common (DTOs/Enums/Models)
```

## Key Features by Folder

| Folder | Key Features |
|--------|-------------|
| Core | Host container, Builder pattern, Event system |
| Catalog | JSON persistence, Platform filtering, CRUD operations |
| Download | Concurrent queue, Status events, Cancellation |
| Compatibility | Version validation, Platform detection |
| Dependencies | Circular detection, Installation ordering |
| Communication | Bearer auth, Resume downloads, Server queries |
| Common | Shared data structures |

## Threading Model

- **Catalog**: Lock-based synchronization for JSON operations
- **Download**: ConcurrentQueue + ConcurrentDictionary for thread-safe queue management
- **Compatibility**: Stateless, thread-safe operations
- **Dependencies**: Uses catalog's thread-safe operations
- **Communication**: HttpClient thread-safe by design

## Compatibility

- **Target Framework**: .NET Standard 2.0
- **Compatible With**: .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5.0+, Mono, Xamarin
