using GeneralUpdate.Drivelution.Core.Pipeline;

namespace DrivelutionTest.Pipeline;

public class PipelineResultTests
{
    [Fact]
    public void Ok_ReturnsSuccessfulResult()
    {
        var result = PipelineResult.Ok();
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Fail_ReturnsFailedResult()
    {
        var result = PipelineResult.Fail("something went wrong");
        Assert.False(result.Success);
        Assert.Equal("something went wrong", result.ErrorMessage);
    }

    [Fact]
    public void Fail_WithException_StoresException()
    {
        var ex = new InvalidOperationException("test");
        var result = PipelineResult.Fail("error", ex);
        Assert.Same(ex, result.Exception);
    }
}
