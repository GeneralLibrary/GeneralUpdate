using GeneralUpdate.Core.SelfCheck;

namespace CoreTest.SelfCheck;

/// <summary>
///     FilePresenceRule tests: missing / empty / all-present, and the vacuous pass for an
///     empty manifest (a misconfigured registration must not break the session).
/// </summary>
public sealed class FilePresenceRuleTests : IDisposable
{
    private readonly string _installPath;

    public FilePresenceRuleTests()
    {
        _installPath = Path.Combine(Path.GetTempPath(), $"gu-filepresence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_installPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_installPath, true); } catch { /* best effort */ }
    }

    private static readonly SelfCheckContext Ctx = new(
        null!, "app.exe", null, null, null,
        GeneralUpdate.Core.Configuration.AppType.Upgrade, null);

    [Fact]
    public async Task AllCriticalFilesPresent_Passes()
    {
        File.WriteAllText(Path.Combine(_installPath, "app.exe"), "binary");
        Directory.CreateDirectory(Path.Combine(_installPath, "config"));
        File.WriteAllText(Path.Combine(_installPath, "config", "appsettings.json"), "{}");

        var ctx = Ctx with { InstallPath = _installPath };
        var rule = new FilePresenceRule("app.exe", "config/appsettings.json");

        var result = await rule.CheckAsync(ctx, CancellationToken.None);

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task MissingFile_FailsWithRelativePath()
    {
        var ctx = Ctx with { InstallPath = _installPath };
        var rule = new FilePresenceRule("missing.exe");

        var result = await rule.CheckAsync(ctx, CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Contains("missing.exe", result.Detail);
    }

    [Fact]
    public async Task EmptyFile_Fails()
    {
        File.WriteAllText(Path.Combine(_installPath, "empty.exe"), string.Empty);
        var ctx = Ctx with { InstallPath = _installPath };
        var rule = new FilePresenceRule("empty.exe");

        var result = await rule.CheckAsync(ctx, CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Contains("empty", result.Detail);
    }

    [Fact]
    public async Task NoCriticalFilesConfigured_PassesVacuously()
    {
        var ctx = Ctx with { InstallPath = _installPath };
        var rule = new FilePresenceRule();

        var result = await rule.CheckAsync(ctx, CancellationToken.None);

        Assert.True(result.Passed);
    }
}
