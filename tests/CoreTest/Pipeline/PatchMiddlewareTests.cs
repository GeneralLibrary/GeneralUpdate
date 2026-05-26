using GeneralUpdate.Core.Pipeline;

namespace CoreTest.Pipeline;

/// <summary>
/// Unit tests for <see cref="PatchMiddleware"/>.
/// Covers: missing DiffPipeline (throws), DiffPipeline present (invokes), exception propagation.
/// </summary>
public class PatchMiddlewareTests
{
    #region No DiffPipeline in context — throws

    [Fact]
    public async Task InvokeAsync_NoDiffPipelineInContext_Throws()
    {
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();
        context.Add("SourcePath", "/src/path");
        context.Add("PatchPath", "/patch/path");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));
    }

    [Fact]
    public async Task InvokeAsync_NullContextProperties_Throws()
    {
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));
    }

    #endregion

    #region DiffPipeline in context — invokes

    [Fact]
    public async Task InvokeAsync_DiffPipelineInContext_CompletesSuccessfully()
    {
        var srcPath = NewTempDir();
        var patchPath = NewTempDir();
        try
        {
            var middleware = new PatchMiddleware();
            var context = new PipelineContext();
            context.Add("SourcePath", srcPath);
            context.Add("PatchPath", patchPath);
            context.Add("DiffPipeline", new DiffPipeline());

            var ex = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

            Assert.Null(ex);
        }
        finally
        {
            DeleteDir(srcPath);
            DeleteDir(patchPath);
        }
    }

    [Fact]
    public async Task InvokeAsync_DiffPipelineInContext_NullPaths_CompletesSuccessfully()
    {
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();
        context.Add("DiffPipeline", new DiffPipeline());

        // DiffPipeline.DirtyAsync returns early when directories don't exist
        var ex = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));
        Assert.Null(ex);
    }

    [Fact]
    public async Task InvokeAsync_DiffPipelineInContext_EmptyStringPaths_CompletesSuccessfully()
    {
        var middleware = new PatchMiddleware();
        var context = new PipelineContext();
        context.Add("SourcePath", string.Empty);
        context.Add("PatchPath", string.Empty);
        context.Add("DiffPipeline", new DiffPipeline());

        // DiffPipeline.DirtyAsync returns early when directories don't exist
        var ex = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));
        Assert.Null(ex);
    }

    #endregion

    #region Helpers

    private static string NewTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pm_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDir(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    #endregion
}
