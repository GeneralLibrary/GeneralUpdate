using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GeneralUpdate.Bowl.Internal;
using GeneralUpdate.Bowl.Strategys;

/// <summary>
/// 分支覆盖点：
/// CrashJsonContext 类：
///   - 类型标注为 [JsonSerializable(typeof(Crash))]
///   - 公开 Crash 属性（JsonTypeInfo&lt;Crash&gt;）
///   - 序列化 Crash 对象（含完整数据）
///   - 序列化 Crash 对象（空字段）
/// </summary>
public class CrashJsonContextTests
{
    [Fact]
    public void Default_Crash属性不为null()
    {
        var ctx = CrashJsonContext.Default;
        Assert.NotNull(ctx.Crash);
    }

    [Fact]
    public void Default_Crash属性类型为JsonTypeInfo()
    {
        var ctx = CrashJsonContext.Default;
        Assert.IsAssignableFrom<JsonTypeInfo<Crash>>(ctx.Crash);
    }

    [Fact]
    public void 序列化完整Crash对象_成功生成JSON()
    {
        var crash = new Crash
        {
            Parameter = new MonitorParameter
            {
                ProcessNameOrId = "test.exe",
                DumpFileName = "v1_fail.dmp",
                FailFileName = "v1_fail.json",
                TargetPath = "/app",
                FailDirectory = "/app/fail/v1",
                BackupDirectory = "/app/v1",
                WorkModel = "Upgrade",
                ExtendedField = "1.0.0",
            },
            ProcdumpOutPutLines = new List<string> { "line1", "line2" },
        };

        var json = JsonSerializer.Serialize(crash, CrashJsonContext.Default.Crash);
        Assert.NotNull(json);
        Assert.Contains("test.exe", json);
        Assert.Contains("v1_fail.dmp", json);
        Assert.Contains("line1", json);
        Assert.Contains("line2", json);
    }

    [Fact]
    public void 序列化空字段Crash_生成有效JSON()
    {
        var crash = new Crash();
        var json = JsonSerializer.Serialize(crash, CrashJsonContext.Default.Crash);
        Assert.NotNull(json);
        Assert.NotEmpty(json);
    }

    [Fact]
    public void 序列化空列表ProcdumpOutPutLines_生成JSON包含空数组()
    {
        var crash = new Crash
        {
            ProcdumpOutPutLines = new List<string>(),
        };
        var json = JsonSerializer.Serialize(crash, CrashJsonContext.Default.Crash);
        Assert.Contains("[]", json);
    }

    [Fact]
    public void 反序列化_成功还原Crash对象()
    {
        var original = new Crash
        {
            Parameter = new MonitorParameter
            {
                ProcessNameOrId = "myapp",
                WorkModel = "Normal",
            },
            ProcdumpOutPutLines = new List<string> { "output1" },
        };
        var json = JsonSerializer.Serialize(original, CrashJsonContext.Default.Crash);
        var deserialized = JsonSerializer.Deserialize(json, CrashJsonContext.Default.Crash);

        Assert.NotNull(deserialized);
        Assert.Equal("myapp", deserialized!.Parameter!.ProcessNameOrId);
        Assert.Equal("Normal", deserialized.Parameter!.WorkModel);
        Assert.Single(deserialized.ProcdumpOutPutLines!);
        Assert.Equal("output1", deserialized.ProcdumpOutPutLines![0]);
    }
}
