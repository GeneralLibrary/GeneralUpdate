using GeneralUpdate.Drivelution.Abstractions.Models;

namespace DrivelutionTest.Models;

/// <summary>
/// Tests for the new model classes added during the refactoring.
/// </summary>
public class NewModelTests
{
    [Fact]
    public void UpdateProgress_HasExpectedDefaults()
    {
        var progress = new UpdateProgress();
        Assert.Equal(UpdateStatus.NotStarted, progress.CurrentStatus);
        Assert.Equal(string.Empty, progress.StepName);
        Assert.Equal(0, progress.Percentage);
        Assert.Equal(0, progress.StepIndex);
        Assert.Equal(0, progress.TotalSteps);
    }

    [Fact]
    public void UpdateProgress_ToString_IncludesAllFields()
    {
        var progress = new UpdateProgress
        {
            Percentage = 50,
            StepName = "Validate",
            StepIndex = 1,
            TotalSteps = 4,
            Message = "Checking"
        };

        var str = progress.ToString();
        Assert.Contains("50", str);
        Assert.Contains("Validate", str);
        Assert.Contains("2/4", str);
        Assert.Contains("Checking", str);
    }

    [Fact]
    public void BatchUpdateResult_AllSucceeded_WhenAllPass()
    {
        var result = new BatchUpdateResult
        {
            SucceededCount = 3,
            FailedCount = 0
        };
        result.AllSucceeded = result.FailedCount == 0;

        Assert.True(result.AllSucceeded);
    }

    [Fact]
    public void BatchUpdateResult_AllSucceeded_WhenAnyFail()
    {
        var result = new BatchUpdateResult
        {
            SucceededCount = 2,
            FailedCount = 1
        };
        result.AllSucceeded = result.FailedCount == 0;

        Assert.False(result.AllSucceeded);
    }

    [Fact]
    public void DriverUpdateEntry_HoldsResult()
    {
        var updateResult = new UpdateResult { Success = true };
        var entry = new DriverUpdateEntry
        {
            DriverInfo = new DriverInfo { Name = "test" },
            Success = true,
            Result = updateResult
        };

        Assert.Same(updateResult, entry.Result);
        Assert.True(entry.Success);
    }
}
