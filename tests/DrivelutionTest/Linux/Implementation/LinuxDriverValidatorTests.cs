using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Linux.Implementation;

namespace DrivelutionTest.Linux.Implementation;

/// <summary>
/// Unit tests for <see cref="LinuxDriverValidator"/> following AAAT pattern.
/// Tests constructor and interface contract.
/// </summary>
public class LinuxDriverValidatorTests
{
    #region Constructor

    [Fact]
    public void Ctor_CreatesInstance()
    {
        var validator = new LinuxDriverValidator();
        Assert.NotNull(validator);
    }

    #endregion

    #region IDriverValidator contract

    [Fact]
    public void Implements_IDriverValidator()
    {
        var validator = new LinuxDriverValidator();
        Assert.IsAssignableFrom<IDriverValidator>(validator);
    }

    #endregion

    #region ValidateCompatibilityAsync — with invalid path

    [Fact]
    public async Task ValidateCompatibilityAsync_NullDriverInfo_ThrowsNullReferenceException()
    {
        var validator = new LinuxDriverValidator();

        // CompatibilityChecker will throw when driverInfo is null
        await Assert.ThrowsAnyAsync<Exception>(
            () => validator.ValidateCompatibilityAsync(null!));
    }

    #endregion

    #region ValidateIntegrityAsync — with non-existent file

    [Fact]
    public async Task ValidateIntegrityAsync_NonExistentFile_ThrowsException()
    {
        var validator = new LinuxDriverValidator();

        // HashValidator will throw when file doesn't exist
        await Assert.ThrowsAnyAsync<Exception>(
            () => validator.ValidateIntegrityAsync(
                "/nonexistent/path/driver.ko", "fake-hash"));
    }

    #endregion

    #region ValidateSignatureAsync — with empty trusted publishers

    [Fact]
    public async Task ValidateSignatureAsync_NoSignatureFile_ReturnsTrueIfNoPublishers()
    {
        var validator = new LinuxDriverValidator();

        // Non-existent file has no .sig/.asc companion → returns !trustedPublishers.Any()
        // When trustedPublishers is empty, returns true
        var result = await validator.ValidateSignatureAsync(
            "/nonexistent/path/driver.ko",
            Array.Empty<string>());

        Assert.True(result);
    }

    #endregion
}
