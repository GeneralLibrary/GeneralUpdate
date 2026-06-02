using GeneralUpdate.Bowl.Internal;

public class CrashTests
{
    [Fact]
    public void DefaultConstructor_FieldsAreNullOrDefault()
    {
        var crash = new Crash();
        Assert.Null(crash.ProcessNameOrId);
        Assert.Null(crash.TargetPath);
        Assert.NotNull(crash.ProcdumpOutPutLines); // initialized to empty list
        Assert.Empty(crash.ProcdumpOutPutLines);
    }

    [Fact]
    public void SetProcessNameOrId_ReadsCorrectly()
    {
        var crash = new Crash { ProcessNameOrId = "test.exe" };
        Assert.Equal("test.exe", crash.ProcessNameOrId);
    }

    [Fact]
    public void SetTargetPath_ReadsCorrectly()
    {
        var crash = new Crash { TargetPath = @"C:\app" };
        Assert.Equal(@"C:\app", crash.TargetPath);
    }

    [Fact]
    public void SetWorkModel_ReadsCorrectly()
    {
        var crash = new Crash { WorkModel = "Upgrade" };
        Assert.Equal("Upgrade", crash.WorkModel);
    }

    [Fact]
    public void SetExtendedField_ReadsCorrectly()
    {
        var crash = new Crash { ExtendedField = "2.0.0" };
        Assert.Equal("2.0.0", crash.ExtendedField);
    }

    [Fact]
    public void SetAllFields_ReadsCorrectly()
    {
        var crash = new Crash
        {
            TargetPath = @"C:\app",
            FailDirectory = @"C:\app\fail\1.0",
            BackupDirectory = @"C:\app\1.0",
            ProcessNameOrId = "myapp.exe",
            DumpFileName = "crash.dmp",
            FailFileName = "crash.json",
            WorkModel = "Upgrade",
            ExtendedField = "1.0.0",
            ProcdumpOutPutLines = new List<string> { "line1", "line2" },
        };

        Assert.Equal(@"C:\app", crash.TargetPath);
        Assert.Equal(@"C:\app\fail\1.0", crash.FailDirectory);
        Assert.Equal(@"C:\app\1.0", crash.BackupDirectory);
        Assert.Equal("myapp.exe", crash.ProcessNameOrId);
        Assert.Equal("crash.dmp", crash.DumpFileName);
        Assert.Equal("crash.json", crash.FailFileName);
        Assert.Equal("Upgrade", crash.WorkModel);
        Assert.Equal("1.0.0", crash.ExtendedField);
        Assert.Equal(2, crash.ProcdumpOutPutLines.Count);
    }
}
