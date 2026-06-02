using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Windows.Implementation;

namespace DrivelutionTest.Windows.Implementation;

/// <summary>
/// Unit tests for <see cref="WindowsDriverValidator"/> following AAAT pattern.
/// Tests constructor and interface contract.
/// </summary>
public class WindowsDriverValidatorTests
{
    #region Constructor

    [Fact]
    public void Ctor_CreatesInstance()
    {
        var validator = new WindowsDriverValidator();
        Assert.NotNull(validator);
    }

    #endregion

    #region IDriverValidator contract

    [Fact]
    public void Implements_IDriverValidator()
    {
        var validator = new WindowsDriverValidator();
        Assert.IsAssignableFrom<IDriverValidator>(validator);
    }

    #endregion

    #region ValidateIntegrityAsync — with non-existent file

    [Fact]
    public async Task ValidateIntegrityAsync_NonExistentFile_ThrowsException()
    {
        var validator = new WindowsDriverValidator();

        await Assert.ThrowsAnyAsync<Exception>(
            () => validator.ValidateIntegrityAsync(
                "C:\\nonexistent\\path\\driver.inf", "fake-hash"));
    }

    #endregion

    #region ValidateSignatureAsync — with non-existent file

    [Fact]
    public async Task ValidateSignatureAsync_NonExistentFile_ThrowsException()
    {
        var validator = new WindowsDriverValidator();

        await Assert.ThrowsAnyAsync<Exception>(
            () => validator.ValidateSignatureAsync(
                "C:\\nonexistent\\path\\driver.inf",
                new[] { "CN=TestPublisher" }));
    }

    #endregion

    #region ValidateCompatibilityAsync

    [Fact]
    public async Task ValidateCompatibilityAsync_NullDriverInfo_ThrowsException()
    {
        var validator = new WindowsDriverValidator();

        await Assert.ThrowsAnyAsync<Exception>(
            () => validator.ValidateCompatibilityAsync(null!));
    }

    #endregion
}
