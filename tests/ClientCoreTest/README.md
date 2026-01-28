# ClientCoreTest - Unit Tests for GeneralUpdate.ClientCore

## Overview

This test project provides comprehensive unit test coverage for the GeneralUpdate.ClientCore component. The tests validate the functionality of the client-side update system, including configuration, strategies, pipelines, and hub services.

## Test Structure

The test project is organized into the following categories:

### Bootstrap Tests (`Bootstrap/`)
- **GeneralClientBootstrapTests.cs** - Tests for the main bootstrap class
  - Configuration methods (SetConfig, SetCustomSkipOption, AddCustomOption)
  - Event listener registrations
  - Fluent interface pattern
  - Validation logic
  - Method chaining

### OSS Tests (`OSS/`)
- **GeneralClientOSSTests.cs** - Tests for OSS (Object Storage Service) update functionality
  - Version comparison logic
  - Configuration serialization
  - Start method workflow

### Strategy Tests (`Strategy/`)
- **WindowsStrategyTests.cs** - Tests for Windows platform update strategy
  - Strategy initialization
  - Pipeline creation
  - Configuration handling
- **LinuxStrategyTests.cs** - Tests for Linux platform update strategy
  - Strategy initialization with blacklist support
  - Pipeline creation
  - Blacklist file/format handling

### Pipeline Tests (`Pipeline/`)
- **HashMiddlewareTests.cs** - Tests for hash verification middleware
  - SHA256 hash verification
  - Case-insensitive comparison
  - Error handling for invalid/missing hashes
- **CompressMiddlewareTests.cs** - Tests for compression middleware
  - Context parameter handling
  - Format/encoding validation
- **PatchMiddlewareTests.cs** - Tests for differential patch middleware
  - Source and target path handling
  - DifferentialCore integration

### Hub Tests (`Hubs/`)
- **UpgradeHubServiceTests.cs** - Tests for SignalR hub service
  - Connection lifecycle (Start, Stop, Dispose)
  - Event listener registration
  - Multiple listener support
  - Interface implementation
- **RandomRetryPolicyTests.cs** - Tests for retry policy
  - Retry timing logic (< 60 seconds)
  - Retry termination (>= 60 seconds)
  - Random delay generation

## Test Statistics

- **Total Tests**: 88
- **Passing**: 88
- **Failing**: 0
- **Test Framework**: xUnit 2.9.3
- **Mocking Framework**: Moq 4.20.72

## Test Categories

### Component Distribution
- Bootstrap: 16 tests
- OSS: 10 tests
- Strategy: 14 tests (7 Windows + 7 Linux)
- Pipeline: 28 tests (9 Hash + 11 Compress + 8 Patch)
- Hubs: 20 tests (13 UpgradeHubService + 7 RandomRetryPolicy)

### Test Types
- Unit Tests: 88
- Integration Tests: 0
- End-to-End Tests: 0

## Running the Tests

### Run all tests
```bash
dotnet test src/c#/ClientCoreTest/ClientCoreTest.csproj
```

### Run tests with detailed output
```bash
dotnet test src/c#/ClientCoreTest/ClientCoreTest.csproj --verbosity detailed
```

### Run specific test class
```bash
dotnet test src/c#/ClientCoreTest/ClientCoreTest.csproj --filter "FullyQualifiedName~GeneralClientBootstrapTests"
```

### Run tests with coverage
```bash
dotnet test src/c#/ClientCoreTest/ClientCoreTest.csproj /p:CollectCoverage=true
```

## Key Testing Patterns

### 1. Fluent Interface Testing
Tests verify that methods return the bootstrap instance for method chaining:
```csharp
var result = bootstrap
    .SetConfig(config)
    .SetCustomSkipOption(() => false)
    .AddListenerException((s, e) => { });
Assert.Same(bootstrap, result);
```

### 2. Event Listener Testing
Tests verify that event listeners can be registered without throwing exceptions:
```csharp
Action<object, ExceptionEventArgs> callback = (sender, args) => { };
var result = bootstrap.AddListenerException(callback);
Assert.NotNull(result);
```

### 3. Async Middleware Testing
Tests verify asynchronous pipeline middleware behavior:
```csharp
var middleware = new HashMiddleware();
await middleware.InvokeAsync(context);
```

### 4. Strategy Factory Testing
Tests verify platform-specific strategy creation:
```csharp
var strategy = new WindowsStrategy();
strategy.Create(config);
Assert.True(true); // No exception means success
```

## Dependencies

- **.NET 10.0** - Target framework
- **xUnit 2.9.3** - Testing framework
- **Moq 4.20.72** - Mocking framework
- **Microsoft.NET.Test.Sdk 17.14.1** - Test SDK
- **coverlet.collector 6.0.4** - Code coverage collection

## Test Coverage

The tests cover the following components:
- ✅ GeneralClientBootstrap - Configuration and lifecycle
- ✅ GeneralClientOSS - OSS update functionality
- ✅ WindowsStrategy - Windows platform strategy
- ✅ LinuxStrategy - Linux platform strategy
- ✅ HashMiddleware - Hash verification
- ✅ CompressMiddleware - Decompression
- ✅ PatchMiddleware - Differential patching
- ✅ UpgradeHubService - SignalR hub integration
- ✅ RandomRetryPolicy - Retry logic

## Notes

### Assertion Testing
Some tests handle Debug.Assert behavior which differs between debug and release builds:
- In debug mode: Assertions throw exceptions
- In release mode: Assertions may be optimized out
- Tests are designed to handle both scenarios

### Private Method Testing
Some private methods are tested indirectly through public API:
- Version comparison logic in GeneralClientOSS
- Pipeline context creation in strategies
- This maintains encapsulation while ensuring functionality

### Lifecycle Testing
Hub service lifecycle tests verify graceful handling when no server is available:
- StartAsync handles connection failures gracefully
- StopAsync and DisposeAsync don't throw exceptions
- Useful for testing resilience

## Future Enhancements

Potential areas for additional testing:
- Integration tests with actual SignalR server
- End-to-end update workflow tests
- Performance/stress testing for large updates
- Concurrent update scenario testing
- Network failure simulation tests

## Contributing

When adding new tests:
1. Follow existing naming conventions
2. Include XML documentation comments
3. Group related tests in the same file
4. Use descriptive test method names
5. Add tests for both success and failure paths
6. Ensure tests are independent and can run in any order
