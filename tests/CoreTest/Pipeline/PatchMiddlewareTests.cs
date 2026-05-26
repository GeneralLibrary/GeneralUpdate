using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.Pipeline;

namespace CoreTest.Pipeline;

/// <summary>
/// AAAT unit tests for <see cref="PatchMiddleware"/>.
/// Covers: null strategy (skip), non-null strategy (invoke), success path, exception propagation.
/// </summary>
public class PatchMiddlewareTests
{
    private sealed class StubDirtyStrategy : IDirtyStrategy
    {
        public bool Invoked { get; private set; }
        public bool ShouldThrow { get; set; }

        public Task ExecuteAsync(string appPath, string patchPath)
        {
            Invoked = true;
            if (ShouldThrow)
                throw new InvalidOperationException("test dirty strategy failure");
            return Task.CompletedTask;
        }
    }

    #region No strategy in context — skip

    [Fact]
    public async Task InvokeAsync_NoDirtyStrategyInContext_SkipsWithoutThrow()
    {
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();
        context.Add("SourcePath", "/src/path");
        context.Add("PatchPath", "/patch/path");

        var ex = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.Null(ex);
    }

    [Fact]
    public async Task InvokeAsync_NullContextProperties_SkipsWithoutThrow()
    {
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();

        var ex = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.Null(ex);
    }

    #endregion

    #region Strategy in context — invokes

    [Fact]
    public async Task InvokeAsync_DirtyStrategyInContext_InvokesExecuteAsync()
    {
        var strategy = new StubDirtyStrategy();
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();
        context.Add("SourcePath", "/src/a.txt");
        context.Add("PatchPath", "/patch/a.txt");
        context.Add("DirtyStrategy", strategy);

        await middleware.InvokeAsync(context);

        Assert.True(strategy.Invoked);
    }

    [Fact]
    public async Task InvokeAsync_DirtyStrategyThrows_ExceptionPropagates()
    {
        var strategy = new StubDirtyStrategy { ShouldThrow = true };
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();
        context.Add("SourcePath", "/src");
        context.Add("PatchPath", "/patch");
        context.Add("DirtyStrategy", strategy);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }

    #endregion

    #region PipelineContext values edge cases

    [Fact]
    public async Task InvokeAsync_DirtyStrategyInContext_NullPaths_InvokesStill()
    {
        var strategy = new StubDirtyStrategy();
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();
        context.Add("DirtyStrategy", strategy);

        await middleware.InvokeAsync(context);

        Assert.True(strategy.Invoked);
    }

    [Fact]
    public async Task InvokeAsync_DirtyStrategyInContext_EmptyStringPaths_InvokesStill()
    {
        var strategy = new StubDirtyStrategy();
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();
        context.Add("SourcePath", string.Empty);
        context.Add("PatchPath", string.Empty);
        context.Add("DirtyStrategy", strategy);

        await middleware.InvokeAsync(context);

        Assert.True(strategy.Invoked);
    }

    #endregion
}
