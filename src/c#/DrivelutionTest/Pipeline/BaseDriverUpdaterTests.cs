using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Pipeline;
using Moq;

namespace DrivelutionTest.Pipeline;

/// <summary>
/// Integration tests for BaseDriverUpdater using a minimal concrete subclass.
/// </summary>
public class BaseDriverUpdaterTests
{
    private readonly Mock<IDriverValidator> _validatorMock;
    private readonly Mock<IDriverBackup> _backupMock;
    private readonly DrivelutionOptions _options;

    public BaseDriverUpdaterTests()
    {
        _validatorMock = new Mock<IDriverValidator>();
        _backupMock = new Mock<IDriverBackup>();
        _options = new DrivelutionOptions { DefaultTimeoutSeconds = 10 };

        // Default: all validations pass
        _validatorMock.Setup(v => v.ValidateIntegrityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _validatorMock.Setup(v => v.ValidateSignatureAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _validatorMock.Setup(v => v.ValidateCompatibilityAsync(It.IsAny<DriverInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _backupMock.Setup(b => b.BackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task UpdateAsync_WithValidInputs_Succeeds()
    {
        using var tempFile = new TempFile();
        var updater = new TestUpdater(_validatorMock.Object, _backupMock.Object, _options, () => Task.CompletedTask);
        var driver = CreateDriver("test", tempFile.Path);
        var strategy = new UpdateStrategy { RequireBackup = false };

        var result = await updater.UpdateAsync(driver, strategy);

        Assert.True(result.Success);
        Assert.Equal(UpdateStatus.Succeeded, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_WithProgress_ReportsProgress()
    {
        using var tempFile = new TempFile();
        var updater = new TestUpdater(_validatorMock.Object, _backupMock.Object, _options, () => Task.CompletedTask);
        var driver = CreateDriver("test", tempFile.Path);
        var strategy = new UpdateStrategy { RequireBackup = false };

        var progressItems = new List<UpdateProgress>();
        var progress = new Progress<UpdateProgress>(p => progressItems.Add(p));

        await updater.UpdateAsync(driver, strategy, progress);

        Assert.NotEmpty(progressItems);
        Assert.Contains(progressItems, p => p.Percentage == 100);
    }

    [Fact]
    public async Task UpdateAsync_WithEvents_RaisesEvents()
    {
        using var tempFile = new TempFile();
        var updater = new TestUpdater(_validatorMock.Object, _backupMock.Object, _options, () => Task.CompletedTask);
        var driver = CreateDriver("test", tempFile.Path);
        var strategy = new UpdateStrategy { RequireBackup = false };

        var stepStarted = new List<string>();
        var stepCompleted = new List<string>();
        UpdateResult? completedResult = null;

        updater.OnStepStarted += s => stepStarted.Add(s);
        updater.OnStepCompleted += s => stepCompleted.Add(s);
        updater.OnUpdateCompleted += r => completedResult = r;

        await updater.UpdateAsync(driver, strategy);

        Assert.NotEmpty(stepStarted);
        Assert.NotEmpty(stepCompleted);
        Assert.NotNull(completedResult);
    }

    [Fact]
    public async Task UpdateAsync_WithTimeout_ReportsTimeout()
    {
        // Set a very short timeout
        var options = new DrivelutionOptions { DefaultTimeoutSeconds = 1 };
        var slowUpdater = new TestUpdater(_validatorMock.Object, _backupMock.Object, options,
            (Func<CancellationToken, Task>)(async ct => await Task.Delay(5000, ct)));

        using var tempFile = new TempFile();
        var driver = CreateDriver("test", tempFile.Path);
        var strategy = new UpdateStrategy { RequireBackup = false, TimeoutSeconds = 1 };

        var result = await slowUpdater.UpdateAsync(driver, strategy);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Timeout, result.Error?.Type);
    }

    [Fact]
    public async Task UpdateAsync_WithBackup_BacksUpDriver()
    {
        using var tempFile = new TempFile();
        var updater = new TestUpdater(_validatorMock.Object, _backupMock.Object, _options, () => Task.CompletedTask);
        var driver = CreateDriver("test", tempFile.Path);
        var strategy = new UpdateStrategy { RequireBackup = true, BackupPath = "./backups" };

        var result = await updater.UpdateAsync(driver, strategy);

        _backupMock.Verify(b => b.BackupAsync(driver.FilePath, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidFile_ReturnsFalse()
    {
        var updater = new TestUpdater(_validatorMock.Object, _backupMock.Object, _options, () => Task.CompletedTask);
        var driver = new DriverInfo { Name = "Test", FilePath = "/nonexistent/file.sys" };

        var result = await updater.ValidateAsync(driver);
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_WithHashMismatch_ReturnsFalse()
    {
        _validatorMock.Setup(v => v.ValidateIntegrityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _validatorMock.Setup(v => v.ValidateCompatibilityAsync(It.IsAny<DriverInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        using var tempFile = new TempFile();
        var updater = new TestUpdater(_validatorMock.Object, _backupMock.Object, _options, () => Task.CompletedTask);
        var driver = CreateDriver("test", tempFile.Path);
        driver.Hash = "bogus-hash";

        var result = await updater.ValidateAsync(driver);
        Assert.False(result);
    }

    [Fact]
    public async Task BackupAsync_ForwardsToBackupService()
    {
        using var tempFile = new TempFile();
        var updater = new TestUpdater(_validatorMock.Object, _backupMock.Object, _options, () => Task.CompletedTask);
        var driver = CreateDriver("test", tempFile.Path);

        await updater.BackupAsync(driver, "/backups/test");

        _backupMock.Verify(b => b.BackupAsync(driver.FilePath, "/backups/test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BatchUpdateAsync_Sequential_ProcessesAllDrivers()
    {
        using var tempFile1 = new TempFile();
        using var tempFile2 = new TempFile();
        using var tempFile3 = new TempFile();
        var updater = new TestUpdater(_validatorMock.Object, _backupMock.Object, _options, () => Task.CompletedTask);
        var drivers = new[]
        {
            CreateDriver("drv1", tempFile1.Path),
            CreateDriver("drv2", tempFile2.Path),
            CreateDriver("drv3", tempFile3.Path)
        };
        var strategy = new UpdateStrategy { RequireBackup = false };

        var result = await updater.BatchUpdateAsync(drivers, strategy, BatchMode.Sequential);

        Assert.True(result.AllSucceeded);
        Assert.Equal(3, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(3, result.Results.Count);
        Assert.True(result.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task BatchUpdateAsync_Parallel_ProcessesAllDrivers()
    {
        using var tempFile1 = new TempFile();
        using var tempFile2 = new TempFile();
        var updater = new TestUpdater(_validatorMock.Object, _backupMock.Object, _options, () => Task.CompletedTask);
        var drivers = new[]
        {
            CreateDriver("drv1", tempFile1.Path),
            CreateDriver("drv2", tempFile2.Path)
        };
        var strategy = new UpdateStrategy { RequireBackup = false };

        var result = await updater.BatchUpdateAsync(drivers, strategy, BatchMode.Parallel);

        Assert.True(result.AllSucceeded);
        Assert.Equal(2, result.SucceededCount);
    }

    [Fact]
    public async Task BatchUpdateAsync_WithProgress_ReportsProgress()
    {
        using var tempFile = new TempFile();
        var updater = new TestUpdater(_validatorMock.Object, _backupMock.Object, _options, () => Task.CompletedTask);
        var drivers = new[] { CreateDriver("drv1", tempFile.Path) };
        var strategy = new UpdateStrategy { RequireBackup = false };
        var progressItems = new List<UpdateProgress>();
        var progress = new Progress<UpdateProgress>(p => progressItems.Add(p));

        await updater.BatchUpdateAsync(drivers, strategy, BatchMode.Sequential, progress);

        Assert.NotEmpty(progressItems);
        Assert.Contains(progressItems, p => p.Percentage == 100);
    }

    private static DriverInfo CreateDriver(string name, string filePath) => new()
    {
        Name = name,
        Version = "1.0.0",
        FilePath = filePath
    };

    /// <summary>
    /// Minimal concrete subclass for testing BaseDriverUpdater.
    /// </summary>
    private class TestUpdater : BaseDriverUpdater
    {
        private readonly Func<CancellationToken, Task> _installAction;

        public TestUpdater(
            IDriverValidator validator,
            IDriverBackup backup,
            DrivelutionOptions? options,
            Func<Task> installAction)
            : this(validator, backup, options, _ => installAction())
        { }

        public TestUpdater(
            IDriverValidator validator,
            IDriverBackup backup,
            DrivelutionOptions? options,
            Func<CancellationToken, Task> installAction)
            : base(validator, backup, options)
        {
            _installAction = installAction;
        }

        protected override Task InstallCoreAsync(DriverInfo driverInfo, UpdateStrategy strategy, CancellationToken cancellationToken)
            => _installAction(cancellationToken);
    }

    /// <summary>
    /// Creates a temp file that is deleted on disposal.
    /// </summary>
    private sealed class TempFile : IDisposable
    {
        public string Path { get; }

        public TempFile()
        {
            Path = System.IO.Path.GetTempFileName();
            File.WriteAllText(Path, "test content");
        }

        public void Dispose()
        {
            try { File.Delete(Path); } catch { }
        }
    }
}
