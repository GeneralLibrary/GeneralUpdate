using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Internal;

/// <summary>
/// 分支覆盖点：
/// CrashReporter.GenerateReportAsync()：
///   - 正常输入：从 BowlContext 构建 Crash 对象并序列化到文件
///   - 空输出行列表
///   - 含多行输出
///   - FailDirectory 合法路径
///   - 返回路径与预期的 FailDirectory + FailFileName 一致
///   - 传入 CancellationToken（未取消）
///   - 输出行列表为 null（模拟边界，实际不应出现）
/// </summary>
public class CrashReporterTests : IDisposable
{
    private readonly string _tempDir;

    public CrashReporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"BowlTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private BowlContext CreateContext(
        string? failDirectory = null,
        string? failFileName = "crash.json",
        string? processName = "test.exe")
    {
        failDirectory ??= _tempDir;
        return new BowlContext
        {
            ProcessNameOrId = processName,
            DumpFileName = "crash.dmp",
            FailFileName = failFileName,
            TargetPath = _tempDir,
            FailDirectory = failDirectory,
            BackupDirectory = Path.Combine(_tempDir, "backup"),
            WorkModel = "Upgrade",
            ExtendedField = "1.0.0",
        };
    }

    [Fact]
    public async Task 正常生成报告_文件存在且包含上下文信息()
    {
        var reporter = new CrashReporter();
        var ctx = CreateContext();
        var lines = new List<string> { "ProcDump v10.0", "Process attached." };

        var path = await reporter.GenerateReportAsync(ctx, lines, CancellationToken.None);

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.Equal(Path.Combine(_tempDir, "crash.json"), path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("ProcDump v10.0", content);
        Assert.Contains("test.exe", content);
    }

    [Fact]
    public async Task 空输出行_生成报告仍包含参数信息()
    {
        var reporter = new CrashReporter();
        var ctx = CreateContext(processName: "empty_app");
        var lines = new List<string>();

        var path = await reporter.GenerateReportAsync(ctx, lines, CancellationToken.None);

        Assert.True(File.Exists(path));
        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("empty_app", content);
    }

    [Fact]
    public async Task 多条输出行_全部包含在报告中()
    {
        var reporter = new CrashReporter();
        var ctx = CreateContext();
        var lines = new List<string> { "line1", "line2", "line3" };

        var path = await reporter.GenerateReportAsync(ctx, lines, CancellationToken.None);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("line1", content);
        Assert.Contains("line2", content);
        Assert.Contains("line3", content);
    }

    [Fact]
    public async Task 不同上下文_生成正确文件名()
    {
        var reporter = new CrashReporter();
        var ctx = CreateContext(failFileName: "v2_fail.json");

        var path = await reporter.GenerateReportAsync(ctx, new List<string>(), CancellationToken.None);

        Assert.Equal(Path.Combine(_tempDir, "v2_fail.json"), path);
    }

    [Fact]
    public async Task 取消令牌未触发_正常完成()
    {
        var reporter = new CrashReporter();
        var ctx = CreateContext();
        var cts = new CancellationTokenSource();

        var path = await reporter.GenerateReportAsync(ctx, new List<string>(), cts.Token);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task WorkModel为Normal_报告中包含Normal()
    {
        var reporter = new CrashReporter();
        var ctx = CreateContext();
        ctx = ctx.Normalize();
        // Override to Normal
        ctx = new BowlContext
        {
            ProcessNameOrId = "app_normal",
            DumpFileName = "d.dmp",
            FailFileName = "r.json",
            TargetPath = _tempDir,
            FailDirectory = _tempDir,
            BackupDirectory = _tempDir,
            WorkModel = "Normal",
            ExtendedField = "3.0",
            TimeoutMs = 30000,
            DumpType = DumpType.Full,
        };

        var path = await reporter.GenerateReportAsync(ctx, new List<string>(), CancellationToken.None);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("Normal", content);
    }
}
