using GeneralUpdate.Drivelution.Core.Pipeline;
using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Abstractions;
using Moq;

namespace DrivelutionTest.Pipeline;

/// <summary>
/// DefaultPipelineSteps 测试
/// 分支覆盖点:
/// - CreateValidateStep: 文件不存在 -> Fail, Hash验证失败 -> Fail, 签名验证失败 -> Fail, 兼容性失败 -> Fail
/// - CreateValidateStep: 跳过Hash验证(SkipHashValidation=true), 跳过签名(SkipSignatureValidation=true), 空Hash跳过
/// - CreateValidateStep: TrustedPublishers为空跳过签名验证
/// - CreateValidateStep: 所有验证通过 -> Ok
/// - CreateBackupStep: RequireBackup=true执行备份, RequireBackup=false跳过
/// - CreateBackupStep: 备份成功 -> Ok 并设置BackupPath, 备份失败 -> Fail
/// - CreateInstallStep: 调用核心安装函数, 成功 -> Ok
/// - CreateVerifyStep: 验证通过 -> Ok, 验证失败不阻塞 -> Ok (仅是警告)
/// 触发条件：使用 Mock 的 IDriverValidator 和 IDriverBackup
/// 预期结果：管道步骤正确执行和跳过
/// </summary>
public class DefaultPipelineStepsTests
{
    private static DriverInfo CreateDriver(string filePath = "/test/driver.sys") => new()
    {
        Name = "TestDriver", Version = "1.0.0", FilePath = filePath,
        TargetOS = "Windows", Architecture = "x64",
        Hash = "abc123", HashAlgorithm = "SHA256",
        TrustedPublishers = new List<string> { "Microsoft" }
    };

    private static UpdateStrategy CreateStrategy() => new()
    {
        RequireBackup = true, BackupPath = "/backups",
        SkipHashValidation = false, SkipSignatureValidation = false
    };

    private static PipelineContext CreateContext(DriverInfo? driver = null, UpdateStrategy? strategy = null)
    {
        return new PipelineContext(
            driver ?? CreateDriver(), strategy ?? CreateStrategy(), new UpdateResult());
    }

    [Fact(DisplayName = "CreateValidateStep_文件不存在_返回Fail")]
    public async Task CreateValidateStep_FileNotFound_ReturnsFail()
    {
        var mockValidator = new Mock<IDriverValidator>();
        var context = CreateContext(CreateDriver("/nonexistent/file.sys"));
        var step = DefaultPipelineSteps.CreateValidateStep(mockValidator.Object);
        var result = await step.ExecuteAsync(context, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact(DisplayName = "CreateValidateStep_Hash验证失败_返回Fail")]
    public async Task CreateValidateStep_HashValidationFails_ReturnsFail()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var mockValidator = new Mock<IDriverValidator>();
            mockValidator.Setup(v => v.ValidateIntegrityAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var context = CreateContext(CreateDriver(tempFile));
            var step = DefaultPipelineSteps.CreateValidateStep(mockValidator.Object);
            var result = await step.ExecuteAsync(context, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Equal("Driver hash validation failed", result.ErrorMessage);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact(DisplayName = "CreateValidateStep_SkipHashValidation_跳过哈希验证")]
    public async Task CreateValidateStep_SkipHashValidation_SkipsHashCheck()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var mockValidator = new Mock<IDriverValidator>();
            mockValidator.Setup(v => v.ValidateCompatibilityAsync(It.IsAny<DriverInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockValidator.Setup(v => v.ValidateSignatureAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var strategy = new UpdateStrategy { SkipHashValidation = true, RequireBackup = true, BackupPath = "/backups" };
            var context = CreateContext(CreateDriver(tempFile), strategy);
            var step = DefaultPipelineSteps.CreateValidateStep(mockValidator.Object);
            var result = await step.ExecuteAsync(context, CancellationToken.None);
            Assert.True(result.Success);
            mockValidator.Verify(v => v.ValidateIntegrityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact(DisplayName = "CreateValidateStep_Hash为空跳过哈希验证")]
    public async Task CreateValidateStep_EmptyHash_SkipsHashCheck()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var mockValidator = new Mock<IDriverValidator>();
            mockValidator.Setup(v => v.ValidateCompatibilityAsync(It.IsAny<DriverInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockValidator.Setup(v => v.ValidateSignatureAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var driver = CreateDriver(tempFile);
            driver.Hash = "";
            var context = CreateContext(driver);
            var step = DefaultPipelineSteps.CreateValidateStep(mockValidator.Object);
            var result = await step.ExecuteAsync(context, CancellationToken.None);
            Assert.True(result.Success);
            mockValidator.Verify(v => v.ValidateIntegrityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact(DisplayName = "CreateValidateStep_SkipSignatureValidation_跳过签名验证")]
    public async Task CreateValidateStep_SkipSignatureValidation_SkipsSignatureCheck()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var mockValidator = new Mock<IDriverValidator>();
            mockValidator.Setup(v => v.ValidateIntegrityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockValidator.Setup(v => v.ValidateCompatibilityAsync(It.IsAny<DriverInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var strategy = new UpdateStrategy { SkipSignatureValidation = true, RequireBackup = true, BackupPath = "/backups" };
            var context = CreateContext(CreateDriver(tempFile), strategy);
            var step = DefaultPipelineSteps.CreateValidateStep(mockValidator.Object);
            var result = await step.ExecuteAsync(context, CancellationToken.None);
            Assert.True(result.Success);
            mockValidator.Verify(v => v.ValidateSignatureAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact(DisplayName = "CreateValidateStep_TrustedPublishers为空跳过签名验证")]
    public async Task CreateValidateStep_EmptyTrustedPublishers_SkipsSignatureCheck()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var mockValidator = new Mock<IDriverValidator>();
            mockValidator.Setup(v => v.ValidateIntegrityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockValidator.Setup(v => v.ValidateCompatibilityAsync(It.IsAny<DriverInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var driver = CreateDriver(tempFile);
            driver.TrustedPublishers.Clear();
            var context = CreateContext(driver);
            var step = DefaultPipelineSteps.CreateValidateStep(mockValidator.Object);
            var result = await step.ExecuteAsync(context, CancellationToken.None);
            Assert.True(result.Success);
            mockValidator.Verify(v => v.ValidateSignatureAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact(DisplayName = "CreateValidateStep_签名验证失败_返回Fail")]
    public async Task CreateValidateStep_SignatureFails_ReturnsFail()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var mockValidator = new Mock<IDriverValidator>();
            mockValidator.Setup(v => v.ValidateIntegrityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockValidator.Setup(v => v.ValidateSignatureAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var context = CreateContext(CreateDriver(tempFile));
            var step = DefaultPipelineSteps.CreateValidateStep(mockValidator.Object);
            var result = await step.ExecuteAsync(context, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Equal("Driver signature validation failed", result.ErrorMessage);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact(DisplayName = "CreateValidateStep_兼容性失败_返回Fail")]
    public async Task CreateValidateStep_CompatibilityFails_ReturnsFail()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var mockValidator = new Mock<IDriverValidator>();
            mockValidator.Setup(v => v.ValidateIntegrityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockValidator.Setup(v => v.ValidateSignatureAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockValidator.Setup(v => v.ValidateCompatibilityAsync(It.IsAny<DriverInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var context = CreateContext(CreateDriver(tempFile));
            var step = DefaultPipelineSteps.CreateValidateStep(mockValidator.Object);
            var result = await step.ExecuteAsync(context, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Contains("not compatible", result.ErrorMessage);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact(DisplayName = "CreateValidateStep_全部通过_返回Ok")]
    public async Task CreateValidateStep_AllPass_ReturnsOk()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var mockValidator = new Mock<IDriverValidator>();
            mockValidator.Setup(v => v.ValidateIntegrityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockValidator.Setup(v => v.ValidateSignatureAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            mockValidator.Setup(v => v.ValidateCompatibilityAsync(It.IsAny<DriverInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var context = CreateContext(CreateDriver(tempFile));
            var step = DefaultPipelineSteps.CreateValidateStep(mockValidator.Object);
            var result = await step.ExecuteAsync(context, CancellationToken.None);
            Assert.True(result.Success);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact(DisplayName = "CreateBackupStep_RequireBackup为true_执行备份")]
    public async Task CreateBackupStep_RequireBackupTrue_RunsBackup()
    {
        var mockBackup = new Mock<IDriverBackup>();
        mockBackup.Setup(b => b.BackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var context = CreateContext();
        var step = DefaultPipelineSteps.CreateBackupStep(mockBackup.Object);
        Assert.True(step.ShouldExecute(context));
        var result = await step.ExecuteAsync(context, CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(context.Result.BackupPath);
        Assert.True(context.Bag.ContainsKey("BackupPath"));
    }

    [Fact(DisplayName = "CreateBackupStep_RequireBackup为false_跳过备份")]
    public void CreateBackupStep_RequireBackupFalse_SkipsBackup()
    {
        var mockBackup = new Mock<IDriverBackup>();
        var strategy = new UpdateStrategy { RequireBackup = false, BackupPath = "/backups" };
        var context = CreateContext(strategy: strategy);
        var step = DefaultPipelineSteps.CreateBackupStep(mockBackup.Object);
        Assert.False(step.ShouldExecute(context));
    }

    [Fact(DisplayName = "CreateBackupStep_备份失败_返回Fail")]
    public async Task CreateBackupStep_BackupFails_ReturnsFail()
    {
        var mockBackup = new Mock<IDriverBackup>();
        mockBackup.Setup(b => b.BackupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var context = CreateContext();
        var step = DefaultPipelineSteps.CreateBackupStep(mockBackup.Object);
        var result = await step.ExecuteAsync(context, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal("Failed to create driver backup", result.ErrorMessage);
    }

    [Fact(DisplayName = "CreateInstallStep_调用InstallCore_返回Ok")]
    public async Task CreateInstallStep_CallsInstallCore_ReturnsOk()
    {
        bool installCalled = false;
        var driver = CreateDriver();
        var context = CreateContext(driver);
        var step = DefaultPipelineSteps.CreateInstallStep((d, s, ct) =>
        {
            installCalled = true;
            Assert.Same(driver, d);
            return Task.CompletedTask;
        });
        var result = await step.ExecuteAsync(context, CancellationToken.None);
        Assert.True(result.Success);
        Assert.True(installCalled);
    }

    [Fact(DisplayName = "CreateVerifyStep_验证成功_返回Ok")]
    public async Task CreateVerifyStep_VerificationSuccess_ReturnsOk()
    {
        var driver = CreateDriver();
        var context = CreateContext(driver);
        var step = DefaultPipelineSteps.CreateVerifyStep((d, ct) => Task.FromResult(true));
        var result = await step.ExecuteAsync(context, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact(DisplayName = "CreateVerifyStep_验证失败_仍然返回Ok非阻塞")]
    public async Task CreateVerifyStep_VerificationFails_StillReturnsOk()
    {
        var driver = CreateDriver();
        var context = CreateContext(driver);
        var step = DefaultPipelineSteps.CreateVerifyStep((d, ct) => Task.FromResult(false));
        var result = await step.ExecuteAsync(context, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact(DisplayName = "CreateValidateStep_StepName为Validate")]
    public void CreateValidateStep_StepName_IsValidate()
    {
        var step = DefaultPipelineSteps.CreateValidateStep(new Mock<IDriverValidator>().Object);
        Assert.Equal("Validate", step.StepName);
    }

    [Fact(DisplayName = "CreateBackupStep_StepName为Backup")]
    public void CreateBackupStep_StepName_IsBackup()
    {
        var step = DefaultPipelineSteps.CreateBackupStep(new Mock<IDriverBackup>().Object);
        Assert.Equal("Backup", step.StepName);
    }

    [Fact(DisplayName = "CreateInstallStep_StepName为Install")]
    public void CreateInstallStep_StepName_IsInstall()
    {
        var step = DefaultPipelineSteps.CreateInstallStep((_, _, _) => Task.CompletedTask);
        Assert.Equal("Install", step.StepName);
    }

    [Fact(DisplayName = "CreateVerifyStep_StepName为Verify")]
    public void CreateVerifyStep_StepName_IsVerify()
    {
        var step = DefaultPipelineSteps.CreateVerifyStep((_, _) => Task.FromResult(true));
        Assert.Equal("Verify", step.StepName);
    }
}
