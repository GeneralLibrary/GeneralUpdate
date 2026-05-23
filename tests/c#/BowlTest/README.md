# BowlTest

Unit tests for the GeneralUpdate.Bowl component.

## Overview

This test project provides comprehensive unit test coverage for the Bowl crash monitoring and recovery component in GeneralUpdate. Bowl is responsible for monitoring whether the main program can start normally after an upgrade, and if a crash is detected, it will restore the backup.

## Test Structure

### Test Organization

```
BowlTest/
├── BowlTests.cs                      - Tests for Bowl main entry point
├── Strategys/
│   ├── MonitorParameterTests.cs      - Tests for MonitorParameter data model
│   ├── WindowStrategyTests.cs        - Tests for Windows-specific strategy
│   └── AbstractStrategyTests.cs      - Tests for base strategy behavior
└── Integration/
    └── BowlIntegrationTests.cs       - End-to-end integration tests
```

## Test Coverage

### BowlTests.cs (7 tests)
- Launch behavior with null/valid parameters
- Environment variable parsing (ProcessInfo)
- Platform detection (Windows vs unsupported platforms)
- Error handling for missing/invalid configuration

### MonitorParameterTests.cs (11 tests)
- Property initialization and assignment
- Default values (WorkModel = "Upgrade")
- All property getters and setters
- Bulk property assignment

### WindowStrategyTests.cs (9 tests)
- Platform-specific procdump executable selection (X86/X64/ARM64)
- SetParameter functionality
- Fail directory creation and cleanup
- File naming conventions (dump and fail files)
- Path construction for backup and fail directories

### AbstractStrategyTests.cs (8 tests)
- Fail directory creation during launch
- Existing directory cleanup
- Process name/ID handling
- Arguments construction for procdump
- Applications directory path construction

### BowlIntegrationTests.cs (7 tests)
- Complete workflow with valid parameters
- Environment variable parsing end-to-end
- WorkModel differences (Normal vs Upgrade)
- Parameter construction from ProcessInfo
- Multiple launches and cleanup
- Version information storage
- JSON serialization/deserialization

## Test Statistics

- **Total Tests**: 42
- **Pass Rate**: 100%
- **Test Framework**: xUnit 2.9.3
- **Mocking Framework**: Moq 4.20.72
- **Target Framework**: .NET 10.0

## Running Tests

### Run all tests:
```bash
dotnet test
```

### Run specific test class:
```bash
dotnet test --filter "FullyQualifiedName~MonitorParameterTests"
```

### Run with detailed output:
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run with code coverage:
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Platform Considerations

Many tests are platform-specific and will only execute on Windows, as Bowl's primary implementation is Windows-focused. Tests automatically skip on non-Windows platforms using:

```csharp
if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    return;
}
```

## Key Testing Patterns

### 1. Arrange-Act-Assert Pattern
All tests follow the AAA pattern for clarity and maintainability.

### 2. Resource Cleanup
Integration tests implement `IDisposable` for automatic cleanup of temporary directories.

### 3. Environment Variable Isolation
Tests that modify environment variables restore original values in finally blocks.

### 4. Reflection for Internal Testing
Some internal classes are tested through reflection or by observing their effects on public behavior.

## Dependencies

The test project references:
- GeneralUpdate.Bowl (project under test)
- xUnit (test framework)
- Moq (mocking framework)
- Microsoft.NET.Test.Sdk (test runner)
- coverlet.collector (code coverage)

## Future Enhancements

Potential areas for additional test coverage:
- LinuxStrategy implementation (when available)
- Export.bat script execution behavior
- StorageManager.Restore integration
- Environment variable setting (UpgradeFail)
- Crash JSON serialization with CrashJsonContext
- Process output capture and parsing

## Contributing

When adding new features to GeneralUpdate.Bowl, please:
1. Add corresponding unit tests
2. Maintain the existing test structure and naming conventions
3. Document tests with XML comments
4. Ensure all tests pass before committing
5. Follow the AAA pattern and existing test styles
