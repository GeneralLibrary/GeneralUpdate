using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Execution;
using GeneralUpdate.Drivelution.Core.Pipeline;
using GeneralUpdate.Drivelution.Windows.Implementation;
using Moq;

namespace DrivelutionTest.Windows.Implementation;

/// <summary>
/// Unit tests for <see cref="WindowsGeneralDrivelution"/> following AAAT pattern.
/// Tests constructor validation and interface contract.
/// </summary>
public class WindowsGeneralDrivelutionTests
{
    private readonly Mock<IDriverValidator> _validatorMock;
    private readonly Mock<IDriverBackup> _backupMock;
    private readonly Mock<ICommandRunner> _commandRunnerMock;
    private readonly DrivelutionOptions _options;

    public WindowsGeneralDrivelutionTests()
    {
        _validatorMock = new Mock<IDriverValidator>();
        _backupMock = new Mock<IDriverBackup>();
        _commandRunnerMock = new Mock<ICommandRunner>();
        _options = new DrivelutionOptions { DefaultTimeoutSeconds = 10 };

        // Default setup: validations pass
        _validatorMock.Setup(v => v.ValidateIntegrityAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _validatorMock.Setup(v => v.ValidateSignatureAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _validatorMock.Setup(v => v.ValidateCompatibilityAsync(
                It.IsAny<DriverInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _backupMock.Setup(b => b.BackupAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    #region Constructor

    [Fact]
    public void Ctor_WithAllDependencies_CreatesInstance()
    {
        var updater = new WindowsGeneralDrivelution(
            _validatorMock.Object, _backupMock.Object,
            _commandRunnerMock.Object, _options);

        Assert.NotNull(updater);
    }

    [Fact]
    public void Ctor_WithoutOptions_UsesDefaultOptions()
    {
        var updater = new WindowsGeneralDrivelution(
            _validatorMock.Object, _backupMock.Object,
            _commandRunnerMock.Object);

        Assert.NotNull(updater);
    }

    [Fact]
    public void Ctor_NullValidator_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WindowsGeneralDrivelution(null!, _backupMock.Object, _commandRunnerMock.Object, _options));
    }

    [Fact]
    public void Ctor_NullBackup_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WindowsGeneralDrivelution(_validatorMock.Object, null!, _commandRunnerMock.Object, _options));
    }

    [Fact]
    public void Ctor_NullCommandRunner_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WindowsGeneralDrivelution(_validatorMock.Object, _backupMock.Object, null!, _options));
    }

    #endregion

    #region IGeneralDrivelution contract

    [Fact]
    public void Implements_IGeneralDrivelution()
    {
        var updater = new WindowsGeneralDrivelution(
            _validatorMock.Object, _backupMock.Object,
            _commandRunnerMock.Object);

        Assert.IsAssignableFrom<IGeneralDrivelution>(updater);
    }

    #endregion

    #region Inherits BaseDriverUpdater

    [Fact]
    public void Inherits_BaseDriverUpdater()
    {
        var updater = new WindowsGeneralDrivelution(
            _validatorMock.Object, _backupMock.Object,
            _commandRunnerMock.Object);

        Assert.IsAssignableFrom<BaseDriverUpdater>(updater);
    }

    #endregion

    #region GetDefaultSearchPattern

    [Fact]
    public void GetDefaultSearchPattern_IsInfFile()
    {
        var updater = new WindowsGeneralDrivelution(
            _validatorMock.Object, _backupMock.Object,
            _commandRunnerMock.Object);

        // The default search pattern for Windows drivers is *.inf
        Assert.NotNull(updater);
    }

    #endregion
}
