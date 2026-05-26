using GeneralUpdate.Core.Differential;
using GeneralUpdate.Core.Pipeline;

namespace CoreTest.Pipeline;

/// <summary>
/// AAAT unit tests for <see cref="PatchMiddleware"/>.
/// Covers: null differ (skip), non-null differ (invoke), success path, exception propagation, both constructors.
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

    #region Parameterless constructor — null differ (skip)

    [Fact]
    public async Task InvokeAsync_NullDiffer_SkipsWithoutThrow()
    {
        var middleware = new PatchMiddleware(); // paramless ctor = no differ
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

    #region Explicit null differ — also skip

    [Fact]
    public async Task InvokeAsync_ExplicitNullDiffer_SkipsWithoutThrow()
    {
        var middleware = new PatchMiddleware(differ: null!);
        var context = new PipelineContext();
        context.Add("SourcePath", "/src");
        context.Add("PatchPath", "/patch");

        var ex = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.Null(ex);
    }

    #endregion

    #region Non-null differ — invokes

    [Fact]
    public async Task InvokeAsync_ValidDiffer_InvokesDirtyAsync()
    {
        var differ = new StubDiffer();
        var middleware = new PatchMiddleware(differ);
        var context = new PipelineContext();
        context.Add("SourcePath", "/src/a.txt");
        context.Add("PatchPath", "/patch/a.txt");

        await middleware.InvokeAsync(context);

        Assert.True(differ.Invoked);
    }

    [Fact]
    public async Task InvokeAsync_DifferThrows_ExceptionPropagates()
    {
        var differ = new StubDiffer { ShouldThrow = true };
        var middleware = new PatchMiddleware(differ);
        var context = new PipelineContext();
        context.Add("SourcePath", "/src");
        context.Add("PatchPath", "/patch");

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }

    #endregion

    #region PipelineContext values edge cases

    [Fact]
    public async Task InvokeAsync_ValidDiffer_WithNullPaths_InvokesStill()
    {
        var differ = new StubDiffer();
        var middleware = new PatchMiddleware(differ);
        var context = new PipelineContext();

        // SourcePath/PatchPath are null in context — differ is still called with null args
        await middleware.InvokeAsync(context);

        Assert.True(differ.Invoked);
    }

    [Fact]
    public async Task InvokeAsync_ValidDiffer_EmptyStringPaths_InvokesStill()
    {
        var differ = new StubDiffer();
        var middleware = new PatchMiddleware(differ);
        var context = new PipelineContext();
        context.Add("SourcePath", string.Empty);
        context.Add("PatchPath", string.Empty);

        await middleware.InvokeAsync(context);

        Assert.True(differ.Invoked);
    }

    #endregion
}
