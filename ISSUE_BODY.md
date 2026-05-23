## Summary

Refactor the `GeneralUpdate.Extension` component across 5 areas: security, architecture, error handling, testability, and code quality.

## Changes

### Security Hardening
- **Zip Slip protection**: Replaced vulnerable `ZipFile.ExtractToDirectory` with safe per-entry extraction that validates path traversal before writing
- **SHA256 hash verification**: Added download integrity check — verifies `ExtensionMetadata.Hash` against downloaded file before installation
- **Atomic catalog writes**: Changed `SaveCatalog()` to use temp-file-then-rename pattern, preventing corruption on mid-write crash; added orphaned `.tmp` file cleanup on load

### Architecture Improvements
- **HttpClient injection**: Added constructor overload accepting externally-managed `HttpClient` (compatible with `IHttpClientFactory`) + `IDisposable` support for owned clients
- **Extension lifecycle hooks**: New `IExtensionLifecycleHooks` interface with 8 hook methods (OnBeforeInstall/OnAfterInstall/OnBeforeActivate/OnAfterActivate/OnBeforeDeactivate/OnAfterDeactivate/OnBeforeUninstall/OnAfterUninstall) + `DefaultExtensionLifecycleHooks` no-op base class
- **DI service overrides**: `ExtensionHostBuilder.Build()` now checks if services are already registered before adding defaults, allowing users to customize any service
- **Service registrations**: Auto-registers `IPlatformServices`, `IExtensionMetadataMapper`, and `IExtensionLifecycleHooks` in the DI container

### Error Handling & Reliability
- **Download result classification**: `DownloadResult` with `DownloadErrorType` enum (NetworkError/ClientError/ServerError/HashMismatch/IoError/Cancelled/Unknown) — replaces silent `catch(Exception)` returning false
- **Version sorting fix**: `FindLatestCompatibleVersion` no longer falls back unparseable versions to Version(0,0) — sorts valid versions first, unknown versions last
- **Batch update API**: `UpdateExtensionsAsync(IEnumerable<string>)` on `IExtensionHost`

### Testability
- **Platform detection decoupled**: `IPlatformServices` abstraction + `RuntimePlatformServices` implementation, injected into `PlatformMatcher`
- **Metadata mapper injected**: `IExtensionMetadataMapper` interface + `DefaultExtensionMetadataMapper`, replacing static `ToMetadata()`

### Code Quality
- **DependencyList cache**: `ExtensionMetadata.DependencyList` (IReadOnlyList<string>) computed property eliminates repeated string.Split across consumers (`DependencyResolver`, `GeneralExtensionHost`)

## Files Changed
- 6 new files (4 interfaces, 2 implementations)
- 8 modified files
- Zero breaking changes — all new APIs are additive, existing constructors/methods preserved
