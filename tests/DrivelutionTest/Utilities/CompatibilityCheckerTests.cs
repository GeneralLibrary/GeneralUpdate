using GeneralUpdate.Drivelution.Core.Utilities;
using GeneralUpdate.Drivelution.Abstractions.Models;

namespace DrivelutionTest.Utilities;

/// <summary>
/// Tests for CompatibilityChecker utility class.
/// Validates platform compatibility checking functionality.
/// </summary>
public class CompatibilityCheckerTests
{
    /// <summary>
    /// Tests that GetCurrentOS returns a valid operating system name.
    /// </summary>
    [Fact]
    public void GetCurrentOS_ReturnsValidOSName()
    {
        // Act
        var os = CompatibilityChecker.GetCurrentOS();

        // Assert
        Assert.NotNull(os);
        Assert.Contains(os, new[] { "Windows", "Linux", "MacOS", "Unknown" });
    }

    /// <summary>
    /// Tests that GetCurrentArchitecture returns a valid architecture.
    /// </summary>
    [Fact]
    public void GetCurrentArchitecture_ReturnsValidArchitecture()
    {
        // Act
        var arch = CompatibilityChecker.GetCurrentArchitecture();

        // Assert
        Assert.NotNull(arch);
        Assert.NotEmpty(arch);
        // Common architectures: X64, X86, Arm, Arm64
        Assert.True(arch.Length > 0);
    }

    /// <summary>
    /// Tests that GetSystemVersion returns a valid version string.
    /// </summary>
    [Fact]
    public void GetSystemVersion_ReturnsValidVersionString()
    {
        // Act
        var version = CompatibilityChecker.GetSystemVersion();

        // Assert
        Assert.NotNull(version);
        Assert.NotEmpty(version);
    }

    /// <summary>
    /// Tests that CheckCompatibility returns true for compatible driver.
    /// </summary>
    [Fact]
    public void CheckCompatibility_WithCompatibleDriver_ReturnsTrue()
    {
        // Arrange
        var currentOS = CompatibilityChecker.GetCurrentOS();
        var currentArch = CompatibilityChecker.GetCurrentArchitecture();
        
        var driverInfo = new DriverInfo
        {
            Name = "Test Driver",
            Version = "1.0.0",
            TargetOS = currentOS,
            Architecture = currentArch,
            FilePath = "/test/driver.sys"
        };

        // Act
        var isCompatible = CompatibilityChecker.CheckCompatibility(driverInfo);

        // Assert
        Assert.True(isCompatible);
    }

    /// <summary>
    /// Tests that CheckCompatibility returns false for incompatible OS.
    /// </summary>
    [Fact]
    public void CheckCompatibility_WithIncompatibleOS_ReturnsFalse()
    {
        // Arrange
        var currentOS = CompatibilityChecker.GetCurrentOS();
        var incompatibleOS = currentOS == "Windows" ? "Linux" : "Windows";
        
        var driverInfo = new DriverInfo
        {
            Name = "Test Driver",
            Version = "1.0.0",
            TargetOS = incompatibleOS,
            Architecture = CompatibilityChecker.GetCurrentArchitecture(),
            FilePath = "/test/driver.sys"
        };

        // Act
        var isCompatible = CompatibilityChecker.CheckCompatibility(driverInfo);

        // Assert
        Assert.False(isCompatible);
    }

    /// <summary>
    /// Tests that CheckCompatibility returns false for incompatible architecture.
    /// </summary>
    [Fact]
    public void CheckCompatibility_WithIncompatibleArchitecture_ReturnsFalse()
    {
        // Arrange
        var currentArch = CompatibilityChecker.GetCurrentArchitecture();
        var incompatibleArch = currentArch.Contains("64") ? "X86" : "X64";
        
        var driverInfo = new DriverInfo
        {
            Name = "Test Driver",
            Version = "1.0.0",
            TargetOS = CompatibilityChecker.GetCurrentOS(),
            Architecture = incompatibleArch,
            FilePath = "/test/driver.sys"
        };

        // Act
        var isCompatible = CompatibilityChecker.CheckCompatibility(driverInfo);

        // Assert
        Assert.False(isCompatible);
    }

    /// <summary>
    /// Tests that CheckCompatibility returns true when TargetOS is empty.
    /// </summary>
    [Fact]
    public void CheckCompatibility_WithEmptyTargetOS_ReturnsTrue()
    {
        // Arrange
        var driverInfo = new DriverInfo
        {
            Name = "Test Driver",
            Version = "1.0.0",
            TargetOS = "",
            Architecture = CompatibilityChecker.GetCurrentArchitecture(),
            FilePath = "/test/driver.sys"
        };

        // Act
        var isCompatible = CompatibilityChecker.CheckCompatibility(driverInfo);

        // Assert
        Assert.True(isCompatible);
    }

    /// <summary>
    /// Tests that CheckCompatibility returns true when Architecture is empty.
    /// </summary>
    [Fact]
    public void CheckCompatibility_WithEmptyArchitecture_ReturnsTrue()
    {
        // Arrange
        var driverInfo = new DriverInfo
        {
            Name = "Test Driver",
            Version = "1.0.0",
            TargetOS = CompatibilityChecker.GetCurrentOS(),
            Architecture = "",
            FilePath = "/test/driver.sys"
        };

        // Act
        var isCompatible = CompatibilityChecker.CheckCompatibility(driverInfo);

        // Assert
        Assert.True(isCompatible);
    }

    /// <summary>
    /// Tests that CheckCompatibility throws ArgumentNullException for null driver info.
    /// </summary>
    [Fact]
    public void CheckCompatibility_WithNullDriverInfo_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CompatibilityChecker.CheckCompatibility(null!));
    }

    /// <summary>
    /// Tests that CheckCompatibilityAsync works correctly.
    /// </summary>
    [Fact]
    public async Task CheckCompatibilityAsync_WithCompatibleDriver_ReturnsTrue()
    {
        // Arrange
        var driverInfo = new DriverInfo
        {
            Name = "Test Driver",
            Version = "1.0.0",
            TargetOS = CompatibilityChecker.GetCurrentOS(),
            Architecture = CompatibilityChecker.GetCurrentArchitecture(),
            FilePath = "/test/driver.sys"
        };

        // Act
        var isCompatible = await CompatibilityChecker.CheckCompatibilityAsync(driverInfo);

        // Assert
        Assert.True(isCompatible);
    }

    /// <summary>
    /// Tests that CheckCompatibilityAsync can be cancelled.
    /// </summary>
    [Fact]
    public async Task CheckCompatibilityAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var driverInfo = new DriverInfo
        {
            Name = "Test Driver",
            Version = "1.0.0",
            TargetOS = CompatibilityChecker.GetCurrentOS(),
            Architecture = CompatibilityChecker.GetCurrentArchitecture(),
            FilePath = "/test/driver.sys"
        };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CompatibilityChecker.CheckCompatibilityAsync(driverInfo, cts.Token));
    }

    /// <summary>
    /// Tests that GetCompatibilityReport returns a complete report.
    /// </summary>
    [Fact]
    public void GetCompatibilityReport_ReturnsCompleteReport()
    {
        // Arrange
        var driverInfo = new DriverInfo
        {
            Name = "Test Driver",
            Version = "1.0.0",
            TargetOS = CompatibilityChecker.GetCurrentOS(),
            Architecture = CompatibilityChecker.GetCurrentArchitecture(),
            FilePath = "/test/driver.sys"
        };

        // Act
        var report = CompatibilityChecker.GetCompatibilityReport(driverInfo);

        // Assert
        Assert.NotNull(report);
        Assert.NotNull(report.CurrentOS);
        Assert.NotNull(report.CurrentArchitecture);
        Assert.NotNull(report.SystemVersion);
        Assert.True(report.OSCompatible);
        Assert.True(report.ArchitectureCompatible);
        Assert.True(report.OverallCompatible);
    }

    /// <summary>
    /// Tests that GetCompatibilityReport shows incompatibility correctly.
    /// </summary>
    [Fact]
    public void GetCompatibilityReport_WithIncompatibleDriver_ShowsIncompatibility()
    {
        // Arrange
        var currentOS = CompatibilityChecker.GetCurrentOS();
        var incompatibleOS = currentOS == "Windows" ? "Linux" : "Windows";
        
        var driverInfo = new DriverInfo
        {
            Name = "Test Driver",
            Version = "1.0.0",
            TargetOS = incompatibleOS,
            Architecture = "InvalidArchitecture",
            FilePath = "/test/driver.sys"
        };

        // Act
        var report = CompatibilityChecker.GetCompatibilityReport(driverInfo);

        // Assert
        Assert.NotNull(report);
        Assert.False(report.OSCompatible);
        Assert.False(report.ArchitectureCompatible);
        Assert.False(report.OverallCompatible);
    }

    /// <summary>
    /// Tests that architecture normalization works correctly.
    /// </summary>
    [Fact]
    public void CheckCompatibility_WithArchitectureAliases_RecognizesAliases()
    {
        // Arrange - Test common architecture aliases
        var testCases = new[]
        {
            ("X64", "AMD64"),
            ("X64", "x86_64"),
            ("X86", "i386"),
            ("ARM64", "AARCH64")
        };

        foreach (var (arch1, arch2) in testCases)
        {
            var driverInfo1 = new DriverInfo
            {
                Name = "Test Driver",
                TargetOS = "",
                Architecture = arch1,
                FilePath = "/test/driver.sys"
            };

            var driverInfo2 = new DriverInfo
            {
                Name = "Test Driver",
                TargetOS = "",
                Architecture = arch2,
                FilePath = "/test/driver.sys"
            };

            // Act - Both should be treated the same way
            var result1 = CompatibilityChecker.CheckCompatibility(driverInfo1);
            var result2 = CompatibilityChecker.CheckCompatibility(driverInfo2);

            // Assert - Results should be consistent (both true or both false)
            Assert.Equal(result1, result2);
        }
    }
}
