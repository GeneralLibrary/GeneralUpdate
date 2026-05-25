using GeneralUpdate.Bowl;

/// <summary>
/// 分支覆盖点：
/// Normalize() 方法：
///   - WorkModel 为空字符串 → 默认值 "Upgrade"
///   - WorkModel 为 null → 默认值 "Upgrade"
///   - WorkModel 为有效值 → 保留原值
///   - TimeoutMs = 0 → 默认值 30000
///   - TimeoutMs &lt; 0 → 默认值 30000
///   - TimeoutMs &gt; 0 → 保留原值
///   - DumpType = default(0) → 默认值 DumpType.Full
///   - DumpType = DumpType.Mini → 保留 DumpType.Mini
///   - DumpType = DumpType.Heap → 保留 DumpType.Heap
///   - 所有字段正常传递
/// 结构体构造：
///   - 默认构造全默认值
///   - 使用 init 设置属性后读取
/// </summary>
public class BowlContextTests
{
    [Fact]
    public void 默认构造_所有属性为默认值()
    {
        var ctx = new BowlContext();
        Assert.Null(ctx.ProcessNameOrId);
        Assert.Null(ctx.DumpFileName);
        Assert.Null(ctx.FailFileName);
        Assert.Null(ctx.TargetPath);
        Assert.Null(ctx.FailDirectory);
        Assert.Null(ctx.BackupDirectory);
        Assert.Null(ctx.WorkModel);
        Assert.Null(ctx.ExtendedField);
        Assert.Equal(0, ctx.TimeoutMs);
        Assert.Equal(default(DumpType), ctx.DumpType);
        Assert.False(ctx.AutoRestore);
        Assert.Null(ctx.OnCrash);
    }

    [Fact]
    public void 使用init设置属性_所有属性正确返回()
    {
        var ctx = new BowlContext
        {
            ProcessNameOrId = "test.exe",
            DumpFileName = "v1_fail.dmp",
            FailFileName = "v1_fail.json",
            TargetPath = "C:\\app",
            FailDirectory = "C:\\app\\fail\\v1",
            BackupDirectory = "C:\\app\\v1",
            WorkModel = "Normal",
            ExtendedField = "1.0.0",
            TimeoutMs = 60_000,
            DumpType = DumpType.Heap,
            AutoRestore = true,
            OnCrash = (info, ct) => Task.CompletedTask,
        };

        Assert.Equal("test.exe", ctx.ProcessNameOrId);
        Assert.Equal("v1_fail.dmp", ctx.DumpFileName);
        Assert.Equal("v1_fail.json", ctx.FailFileName);
        Assert.Equal("C:\\app", ctx.TargetPath);
        Assert.Equal("C:\\app\\fail\\v1", ctx.FailDirectory);
        Assert.Equal("C:\\app\\v1", ctx.BackupDirectory);
        Assert.Equal("Normal", ctx.WorkModel);
        Assert.Equal("1.0.0", ctx.ExtendedField);
        Assert.Equal(60_000, ctx.TimeoutMs);
        Assert.Equal(DumpType.Heap, ctx.DumpType);
        Assert.True(ctx.AutoRestore);
        Assert.NotNull(ctx.OnCrash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Normalize_WorkModel为null或空_默认Upgrade(string? workModel)
    {
        var ctx = new BowlContext { WorkModel = workModel };
        var result = ctx.Normalize();
        Assert.Equal("Upgrade", result.WorkModel);
    }

    [Fact]
    public void Normalize_WorkModel为Normal_保留Normal()
    {
        var ctx = new BowlContext { WorkModel = "Normal" };
        var result = ctx.Normalize();
        Assert.Equal("Normal", result.WorkModel);
    }

    [Fact]
    public void Normalize_WorkModel为Upgrade_保留Upgrade()
    {
        var ctx = new BowlContext { WorkModel = "Upgrade" };
        var result = ctx.Normalize();
        Assert.Equal("Upgrade", result.WorkModel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Normalize_TimeoutMs小于等于零_默认30000(int timeoutMs)
    {
        var ctx = new BowlContext { TimeoutMs = timeoutMs };
        var result = ctx.Normalize();
        Assert.Equal(30_000, result.TimeoutMs);
    }

    [Fact]
    public void Normalize_TimeoutMs为正数_保留原值()
    {
        var ctx = new BowlContext { TimeoutMs = 45_000 };
        var result = ctx.Normalize();
        Assert.Equal(45_000, result.TimeoutMs);
    }

    [Fact]
    public void Normalize_TimeoutMs为1_保留1()
    {
        var ctx = new BowlContext { TimeoutMs = 1 };
        var result = ctx.Normalize();
        Assert.Equal(1, result.TimeoutMs);
    }

    [Fact]
    public void Normalize_TimeoutMs为int最大值_保留最大值()
    {
        var ctx = new BowlContext { TimeoutMs = int.MaxValue };
        var result = ctx.Normalize();
        Assert.Equal(int.MaxValue, result.TimeoutMs);
    }

    [Fact]
    public void Normalize_DumpType为default_默认Full()
    {
        var ctx = new BowlContext { DumpType = default };
        var result = ctx.Normalize();
        Assert.Equal(DumpType.Full, result.DumpType);
    }

    [Fact]
    public void Normalize_DumpType为Mini_保留Mini()
    {
        var ctx = new BowlContext { DumpType = DumpType.Mini };
        var result = ctx.Normalize();
        Assert.Equal(DumpType.Mini, result.DumpType);
    }

    [Fact]
    public void Normalize_DumpType为Heap_保留Heap()
    {
        var ctx = new BowlContext { DumpType = DumpType.Heap };
        var result = ctx.Normalize();
        Assert.Equal(DumpType.Heap, result.DumpType);
    }

    [Fact]
    public void Normalize_ProcessNameOrId_正确传递()
    {
        var ctx = new BowlContext { ProcessNameOrId = "myapp" };
        var result = ctx.Normalize();
        Assert.Equal("myapp", result.ProcessNameOrId);
    }

    [Fact]
    public void Normalize_DumpFileName_正确传递()
    {
        var ctx = new BowlContext { DumpFileName = "crash.dmp" };
        var result = ctx.Normalize();
        Assert.Equal("crash.dmp", result.DumpFileName);
    }

    [Fact]
    public void Normalize_FailFileName_正确传递()
    {
        var ctx = new BowlContext { FailFileName = "crash.json" };
        var result = ctx.Normalize();
        Assert.Equal("crash.json", result.FailFileName);
    }

    [Fact]
    public void Normalize_TargetPath_正确传递()
    {
        var ctx = new BowlContext { TargetPath = "C:\\target" };
        var result = ctx.Normalize();
        Assert.Equal("C:\\target", result.TargetPath);
    }

    [Fact]
    public void Normalize_FailDirectory_正确传递()
    {
        var ctx = new BowlContext { FailDirectory = "C:\\fail" };
        var result = ctx.Normalize();
        Assert.Equal("C:\\fail", result.FailDirectory);
    }

    [Fact]
    public void Normalize_BackupDirectory_正确传递()
    {
        var ctx = new BowlContext { BackupDirectory = "C:\\backup" };
        var result = ctx.Normalize();
        Assert.Equal("C:\\backup", result.BackupDirectory);
    }

    [Fact]
    public void Normalize_ExtendedField_正确传递()
    {
        var ctx = new BowlContext { ExtendedField = "2.0.0" };
        var result = ctx.Normalize();
        Assert.Equal("2.0.0", result.ExtendedField);
    }

    [Fact]
    public void Normalize_AutoRestore为true_保留true()
    {
        var ctx = new BowlContext { AutoRestore = true };
        var result = ctx.Normalize();
        Assert.True(result.AutoRestore);
    }

    [Fact]
    public void Normalize_AutoRestore为false_保留false()
    {
        var ctx = new BowlContext { AutoRestore = false };
        var result = ctx.Normalize();
        Assert.False(result.AutoRestore);
    }

    [Fact]
    public void Normalize_OnCrash回调_正确传递()
    {
        static Task handler(CrashInfo i, CancellationToken ct) => Task.CompletedTask;
        var ctx = new BowlContext { OnCrash = handler };
        var result = ctx.Normalize();
        Assert.NotNull(result.OnCrash);
    }

    [Fact]
    public void Normalize_OnCrash为null_保留null()
    {
        var ctx = new BowlContext { OnCrash = null };
        var result = ctx.Normalize();
        Assert.Null(result.OnCrash);
    }
}
