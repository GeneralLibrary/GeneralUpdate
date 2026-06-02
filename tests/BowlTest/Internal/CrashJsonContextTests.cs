using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GeneralUpdate.Bowl.Internal;

public class CrashJsonContextTests
{
    [Fact]
    public void Default_CrashPropertyNotNull()
    {
        var ctx = CrashJsonContext.Default;
        Assert.NotNull(ctx.Crash);
    }

    [Fact]
    public void Default_CrashPropertyIsJsonTypeInfo()
    {
        var ctx = CrashJsonContext.Default;
        Assert.IsAssignableFrom<JsonTypeInfo<Crash>>(ctx.Crash);
    }

    [Fact]
    public void SerializeFullCrash_GeneratesValidJson()
    {
        var crash = new Crash
        {
            ProcessNameOrId = "test.exe",
            DumpFileName = "v1_fail.dmp",
            FailFileName = "v1_fail.json",
            TargetPath = "/app",
            FailDirectory = "/app/fail/v1",
            BackupDirectory = "/app/v1",
            WorkModel = "Upgrade",
            ExtendedField = "1.0.0",
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
    public void SerializeEmptyCrash_GeneratesValidJson()
    {
        var crash = new Crash();
        var json = JsonSerializer.Serialize(crash, CrashJsonContext.Default.Crash);
        Assert.NotNull(json);
        Assert.NotEmpty(json);
    }

    [Fact]
    public void SerializeEmptyProcdumpOutPutLines_JsonContainsEmptyArray()
    {
        var crash = new Crash
        {
            ProcdumpOutPutLines = new List<string>(),
        };
        var json = JsonSerializer.Serialize(crash, CrashJsonContext.Default.Crash);
        Assert.Contains("[]", json);
    }

    [Fact]
    public void Deserialize_RestoresCrashCorrectly()
    {
        var original = new Crash
        {
            ProcessNameOrId = "myapp",
            WorkModel = "Normal",
            ProcdumpOutPutLines = new List<string> { "output1" },
        };
        var json = JsonSerializer.Serialize(original, CrashJsonContext.Default.Crash);
        var deserialized = JsonSerializer.Deserialize(json, CrashJsonContext.Default.Crash);

        Assert.NotNull(deserialized);
        Assert.Equal("myapp", deserialized!.ProcessNameOrId);
        Assert.Equal("Normal", deserialized.WorkModel);
        Assert.Single(deserialized.ProcdumpOutPutLines!);
        Assert.Equal("output1", deserialized.ProcdumpOutPutLines![0]);
    }
}
