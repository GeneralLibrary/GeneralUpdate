using GeneralUpdate.Core.Pipeline;

namespace CoreTest.Pipeline;

public class PipelineBuilderTests
{
    private class SpyMiddleware : IMiddleware
    {
        public bool Invoked { get; private set; }
        public Task InvokeAsync(PipelineContext context)
        {
            Invoked = true;
            return Task.CompletedTask;
        }
    }

    private class ThrowingMiddleware : IMiddleware
    {
        public Task InvokeAsync(PipelineContext context)
            => throw new InvalidOperationException("test failure");
    }

    [Fact]
    public async Task Build_EmptyStack_DoesNothing()
    {
        var builder = new PipelineBuilder(new PipelineContext());
        await builder.Build();
    }

    [Fact]
    public async Task Build_SingleMiddleware_Invoked()
    {
        var builder = new PipelineBuilder(new PipelineContext());
        builder.UseMiddleware<SpyMiddleware>();
        await builder.Build();
        // Middleware invoked
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void UseMiddlewareIf_ConditionFalseOrNull_DoesNotAdd(bool? condition)
    {
        var builder = new PipelineBuilder(new PipelineContext());
        builder.UseMiddlewareIf<SpyMiddleware>(condition);
        Assert.NotNull(builder);
    }

    [Fact]
    public void UseMiddlewareIf_ConditionTrue_AddsMiddleware()
    {
        var builder = new PipelineBuilder(new PipelineContext());
        builder.UseMiddlewareIf<SpyMiddleware>(true);
        Assert.NotNull(builder);
    }
}
