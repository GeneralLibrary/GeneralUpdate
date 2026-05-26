using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.Pipeline;

namespace CoreTest.Pipeline;

/// <summary>
/// AAAT unit tests for <see cref="PatchMiddleware"/>.
/// Covers: null differ (skip), non-null differ (invoke), success path, exception propagation.
/// </summary>
public class PatchMiddlewareTests
{
    private sealed class StubDiffer : IBinaryDiffer
    {
        public bool Invoked { get; private set; }
        public bool ShouldThrow { get; set; }

        public Task CleanAsync(
            string oldFilePath, string newFilePath, string patchFilePath,
            CancellationToken cancellationToken = default)
        {
            Invoked = true;
            if (ShouldThrow)
                throw new InvalidOperationException("test differ failure");
            return Task.CompletedTask;
        }

        public Task DirtyAsync(
            string oldFilePath, string newFilePath, string patchFilePath,
            CancellationToken cancellationToken = default)
        {
            Invoked = true;
            if (ShouldThrow)
                throw new InvalidOperationException("test differ failure");
            return Task.CompletedTask;
        }
    }

    #region No differ in context — skip

    [Fact]
    public async Task InvokeAsync_NoDifferInContext_SkipsWithoutThrow()
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

    #region Differ in context — invokes

    [Fact]
    public async Task InvokeAsync_DifferInContext_InvokesDirtyAsync()
    {
        var differ = new StubDiffer();
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();
        context.Add("SourcePath", "/src/a.txt");
        context.Add("PatchPath", "/patch/a.txt");
        context.Add("BinaryDiffer", differ);

        await middleware.InvokeAsync(context);

        Assert.True(differ.Invoked);
    }

    [Fact]
    public async Task InvokeAsync_DifferInContext_ThrowsExceptionPropagates()
    {
        var differ = new StubDiffer { ShouldThrow = true };
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();
        context.Add("SourcePath", "/src");
        context.Add("PatchPath", "/patch");
        context.Add("BinaryDiffer", differ);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }

    #endregion

    #region PipelineContext values edge cases

    [Fact]
    public async Task InvokeAsync_DifferInContext_NullPaths_InvokesStill()
    {
        var differ = new StubDiffer();
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();
        context.Add("BinaryDiffer", differ);

        await middleware.InvokeAsync(context);

        Assert.True(differ.Invoked);
    }

    [Fact]
    public async Task InvokeAsync_DifferInContext_EmptyStringPaths_InvokesStill()
    {
        var differ = new StubDiffer();
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();
        context.Add("SourcePath", string.Empty);
        context.Add("PatchPath", string.Empty);
        context.Add("BinaryDiffer", differ);

        await middleware.InvokeAsync(context);

        Assert.True(differ.Invoked);
    }

    #endregion
}
